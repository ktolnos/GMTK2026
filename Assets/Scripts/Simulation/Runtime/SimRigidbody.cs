using System.Collections.Generic;
using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Where a body is, recorded as a pose per tick.
    ///
    /// Every body is dynamic, always. Recording means the solver chooses its velocity; replaying
    /// means we do, aiming at the recorded pose from wherever the body actually is. Kinematic is
    /// infinite mass -- it wins every contact and takes no reaction, so playback could shove
    /// everything and be shoved by nothing.
    ///
    /// Being dynamic is also what makes divergence measurable. See <see cref="Diverged"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimRigidbody : SimComponent<SimRigidbody.State>
    {
        [System.Serializable]
        public struct State
        {
            public Vector2 Position;

            /// Degrees, as Rigidbody2D reports it.
            public float Rotation;

            /// <summary>
            /// The bodies this one was touching, by <see cref="SimBody.Id"/>.
            ///
            /// Static geometry is left out. It cannot change, so a contact with it can never be
            /// evidence that history has.
            /// </summary>
            public HashSet<string> Touching;
        }

        [Tooltip("Transform the sprite lives on. Drawing runs one tick behind the solver, so this " +
                 "must not be the rigidbody's own transform: writing a stale pose onto a dynamic " +
                 "body would feed it straight back into the next step.")]
        [SerializeField] Transform view;

        /// <summary>
        /// How far, in metres, a partner the recording names may be before we take the recording to
        /// describe a world that no longer exists.
        ///
        /// Wide. The question is asked before the state is applied, so the bodies are still a step
        /// short of the configuration being replayed and this has to swallow a step of closing
        /// speed. All it separates is "about to touch" from "somewhere else entirely", and a
        /// tolerance is only needed on this side -- see Diverged.
        /// </summary>
        static float causeRange = 0.25f;

        /// Stands in for a recording made before anything touched anything.
        static readonly HashSet<string> nothing = new HashSet<string>();

        /// Scratch for the contact query.
        static readonly List<ContactPoint2D> contacts = new List<ContactPoint2D>();

        Rigidbody2D rb;

        Collider2D[] colliders;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            colliders = GetComponentsInChildren<Collider2D>();

            // Overrides whatever the prefab was authored with; nothing here may be kinematic.
            rb.bodyType = RigidbodyType2D.Dynamic;

            // A sleeping body ignores the velocity we hand it, which silently freezes playback.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            Debug.Assert(view != null && view != transform,
                $"{name} needs a separate view transform; drawing must not move the rigidbody.",
                this);
        }

        /// <summary>
        /// Put the body back on its recorded pose.
        ///
        /// MovePosition does the moving. It skips gravity and damping, so an unobstructed body lands
        /// exactly, and it still collides, so an obstructed one is stopped short of the pose -- which
        /// is the whole reason playback can be leaned on. What it does not do is leave behind the
        /// velocity it used, and a replaying body's velocity is what it keeps if it gets claimed, so
        /// we state that ourselves.
        ///
        /// Aiming from where the body really is, rather than from where its recording says it was,
        /// is what pulls it back onto its path after a knock.
        /// </summary>
        protected override void Replay(in SimReplay<State> replay)
        {
            rb.linearVelocity = (replay.State.Position - rb.position) / Sim.SecondsPerTick;
            rb.angularVelocity = Mathf.DeltaAngle(rb.rotation, replay.State.Rotation) / Sim.SecondsPerTick;
            rb.MovePosition(replay.State.Position);
            rb.MoveRotation(replay.State.Rotation);
        }

        /// <summary>
        /// Whether the world still fits what this body is about to replay.
        ///
        /// Who it is touching, rather than where it is. How deeply two bodies overlap does not
        /// reproduce once the cursor turns round -- the solver recovers penetration over several
        /// steps and never pushes a pair together to put it back -- so a pose test tight enough to
        /// be useful fires on contacts that were faithfully replayed. Whether they are touching
        /// survives.
        ///
        /// Each direction asks whatever is trustworthy for it, and neither needs to look past the
        /// bodies actually involved. Absence is measured, because the solver's manifold drops a
        /// replayed contact and re-forms it; the partners to measure to are the handful the
        /// recording names. Presence is the manifold, which can only ever under-report, and it
        /// wants no tolerance at all: the set it gives now is the same set the previous tick
        /// recorded from it, nothing having moved in between, so the two agree exactly.
        ///
        /// The measured side is asked a step short of the configuration being replayed, though,
        /// which is why that side alone needs causeRange -- the partner may still be closing.
        /// </summary>
        public override bool Diverged(in SimStep step)
        {
            if (step.Take == Takes.None) return false;

            var recorded = layers.Read(step.Take, step.Tick).Touching ?? nothing;

            foreach (var id in recorded)
            {
                var partner = Find(id);

                if (partner == null || Gap(partner) > causeRange) return true;
            }

            // Null where history does not cover the tick we came from -- the first tick of a rewind
            // into recorded ground, most often. Then we are standing in a pose nothing recorded, so
            // its contacts contradict nothing and only the partners this tick names are asked about.
            var before = Recorded(step.Previous);

            if (before == null) return false;

            // A contact this tick did not record, and not one the tick behind is still letting go of.
            foreach (var id in Touching())
                if (!recorded.Contains(id) && !before.Contains(id)) return true;

            return false;
        }

        /// The rigidbody of a body the recording names, or null if it has left the world.
        static SimRigidbody Find(string id)
        {
            var body = Sim.I.Find(id);

            return body != null ? body.GetComponent<SimRigidbody>() : null;
        }

        /// What history says this body was touching at a tick, or null if nothing recorded it.
        HashSet<string> Recorded(int tick) => TryRead(tick, out var state) ? state.Touching ?? nothing : null;

        protected override State Capture(in SimStep step)
        {
            return new State
            {
                Position = rb.position,
                Rotation = rb.rotation,
                Touching = Touching(),
            };
        }

        /// <summary>
        /// Who this body is touching right now, as ids. Bodies rather than colliders, so a partner
        /// built from several counts once.
        ///
        /// The solver's own manifold. It is honest about a contact the solver made and unreliable
        /// only about one being replayed -- it separates a replayed overlap each step and the drive
        /// re-aims into it, so membership flickers. Neither costs anything here: recording only
        /// happens while the solver is the one choosing, and a flicker can drop a contact but never
        /// invent one, so at worst an intrusion is noticed a tick late.
        /// </summary>
        HashSet<string> Touching()
        {
            var touching = new HashSet<string>();

            int count = rb.GetContacts(contacts);

            for (int i = 0; i < count; i++)
            {
                var contact = contacts[i];
                var other = contact.rigidbody == rb ? contact.otherRigidbody : contact.rigidbody;

                if (other == null) continue;

                var body = other.GetComponent<SimBody>();

                if (body != null) touching.Add(body.Id);
            }

            return touching;
        }

        /// Distance between the nearest pair of shapes on these two bodies, negative when they
        /// overlap, and float.MaxValue when there is nothing to measure between.
        float Gap(SimRigidbody other)
        {
            float gap = float.MaxValue;

            foreach (var mine in colliders)
            {
                if (!mine.enabled) continue;

                foreach (var theirs in other.colliders)
                {
                    if (!theirs.enabled) continue;

                    var distance = mine.Distance(theirs);

                    if (distance.isValid) gap = Mathf.Min(gap, distance.distance);
                }
            }

            return gap;
        }

        /// Presentation only, and deliberately on `view` rather than the rigidbody. Runs on frames
        /// where no step happened, which is the whole point of it.
        protected override void Show(in SimBlend<State> blend)
        {
            Vector2 position = Vector2.Lerp(blend.From.Position, blend.To.Position, blend.T);
            float rotation = Mathf.LerpAngle(blend.From.Rotation, blend.To.Rotation, blend.T);

            // Keep whatever z the view was authored with; in 2D that is usually sort order.
            view.SetPositionAndRotation(
                new Vector3(position.x, position.y, view.position.z),
                Quaternion.Euler(0f, 0f, rotation));
        }
    }
}
