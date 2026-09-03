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
            /// How far away every nearby body was, by <see cref="SimBody.Id"/>, negative where the
            /// shapes overlapped.
            ///
            /// A distance rather than a yes-or-no, because on the way back it is compared against
            /// where things have actually got to, and how much a partner has moved is the question.
            /// Static geometry is left out: it cannot change, so distance to it is never evidence
            /// that history has.
            /// </summary>
            public Dictionary<string, float> Gaps;
        }

        [Tooltip("Transform the sprite lives on. Drawing runs one tick behind the solver, so this " +
                 "must not be the rigidbody's own transform: writing a stale pose onto a dynamic " +
                 "body would feed it straight back into the next step.")]
        [SerializeField] Transform view;

        /// How far apart, in metres, two bodies can be and still count as touching.
        static float touchRange = 0.02f;

        /// <summary>
        /// How much further than the recording had it a partner has to have got before the recording
        /// is describing a world that no longer exists.
        ///
        /// Wide, and a difference rather than a distance. The question is asked before the state is
        /// applied, so the bodies are still a step short of the configuration being replayed and this
        /// has to swallow a step of closing speed. All it separates is "roughly where it was" from
        /// "somewhere else entirely".
        /// </summary>
        static float causeBand = 0.2f;

        /// How far away a body has to be before it is not worth writing down. Anything past this
        /// cannot come back as a contact without having crossed causeBand to get here.
        static float notedRange = 0.5f;

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
        /// Both halves compare distances, and neither walks the world. Where the recording had a
        /// partner close enough to be leaning on, that one partner is looked up and measured. What
        /// is touching us that the recording did not have close comes from the solver's contacts,
        /// which is a candidate list rather than an answer: it over-reports pairs that are merely
        /// near, since a contact exists once fattened bounds overlap, and it drops a replayed
        /// contact and re-forms it. Both are harmless against a recorded distance -- over-reporting
        /// is filtered by the separation, and a drop only delays noticing an intruder by a tick.
        ///
        /// The recorded distance is also what makes this forgiving. A contact ending, or one still
        /// arriving, differs from its recording by a fraction of a step's travel rather than by
        /// whether a set contains an id, so nothing has to be special-cased about either end.
        /// </summary>
        public override bool Diverged(in SimStep step)
        {
            if (step.Take == Takes.None) return false;

            var recorded = layers.Read(step.Take, step.Tick).Gaps;

            if (recorded == null) return false;

            // Has anything this tick was leaning on gone somewhere else?
            foreach (var pair in recorded)
            {
                if (pair.Value > touchRange) continue;

                var partner = Find(pair.Key);

                if (partner == null || Gap(partner) > pair.Value + causeBand) return true;
            }

            // Is anything touching us that this tick had nowhere near?
            int count = rb.GetContacts(contacts);

            for (int i = 0; i < count; i++)
            {
                if (contacts[i].separation > touchRange) continue;

                var body = Named(contacts[i]);

                if (body == null) continue;

                if (!recorded.TryGetValue(body.Id, out float was) || was > touchRange + causeBand) return true;
            }

            return false;
        }

        /// The rigidbody of a body the recording names, or null if it has left the world -- which is
        /// itself an answer, since a partner that no longer exists has certainly gone.
        static SimRigidbody Find(string id)
        {
            var body = Sim.I.Find(id);

            return body != null ? body.GetComponent<SimRigidbody>() : null;
        }

        /// Who the far side of a contact is, or null when it is not something we record.
        SimBody Named(in ContactPoint2D contact)
        {
            var other = contact.rigidbody == rb ? contact.otherRigidbody : contact.rigidbody;

            return other != null ? other.GetComponent<SimBody>() : null;
        }

        protected override State Capture(in SimStep step)
        {
            return new State
            {
                Position = rb.position,
                Rotation = rb.rotation,
                Gaps = Gaps(),
            };
        }

        /// <summary>
        /// How far away everything within notedRange is, by id.
        ///
        /// This is the side that walks the world, and it can afford to: it runs only while recording,
        /// which is a handful of bodies at a time, and it is the side that has to be exact. The
        /// solver's contacts would be cheaper and are not usable -- a contact exists once fattened
        /// bounds overlap, and how fat those are depends on how the body has been moving, so the same
        /// pair at the same distance is in the list on one pass and out of it on another.
        /// </summary>
        Dictionary<string, float> Gaps()
        {
            var gaps = new Dictionary<string, float>();

            var bodies = Sim.I.Bodies;

            for (int i = 0; i < bodies.Count; i++)
            {
                var other = bodies[i].GetComponent<SimRigidbody>();

                if (other == null || other == this) continue;

                float gap = Gap(other);

                if (gap <= notedRange) gaps[bodies[i].Id] = gap;
            }

            return gaps;
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
