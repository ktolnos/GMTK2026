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
        /// How close, in metres, two bodies have to be to count as touching.
        ///
        /// Well above how far the solver's separation pass can hold a replayed overlap open, which
        /// is what has to be swallowed for the answer to come out the same in both directions. Well
        /// below anything visible, so a pair that reads as touching looks like it.
        /// </summary>
        static float contactRange = 0.05f;

        /// <summary>
        /// How far, in metres, a body the recording expects may be from us before we take the
        /// recording to describe a world that no longer exists.
        ///
        /// Loose on purpose. It is asked before the state is applied, so the bodies are still a step
        /// short of the configuration being replayed and it has to swallow a step of closing speed.
        /// All it has to separate is "about to touch" from "somewhere else entirely".
        /// </summary>
        static float causeRange = 0.25f;

        /// Stands in for a recording made before anything touched anything.
        static readonly HashSet<string> nothing = new HashSet<string>();

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
        /// Asked of the world as it stands, a step short of the pose being replayed, which is why
        /// the two directions get different ranges. A partner the recording expects only has to be
        /// nearby, because it may still be closing. A partner it does not expect has to be properly
        /// touching -- and one that was touching a tick ago is a contact ending, not a new one.
        /// </summary>
        public override bool Diverged(in SimStep step)
        {
            if (step.Take == Takes.None) return false;

            var recorded = layers.Read(step.Take, step.Tick).Touching ?? nothing;
            var before = Recorded(step.Previous) ?? nothing;

            var bodies = Sim.I.Bodies;

            for (int i = 0; i < bodies.Count; i++)
            {
                var other = bodies[i].GetComponent<SimRigidbody>();

                if (other == null || other == this) continue;

                float gap = Gap(other);
                string id = bodies[i].Id;

                // The cause of what we are about to replay is not even in the neighbourhood.
                if (recorded.Contains(id))
                {
                    if (gap > causeRange) return true;
                }

                // Something is touching us that neither this tick nor the one behind accounts for.
                else if (gap < contactRange && !before.Contains(id)) return true;
            }

            return false;
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
        /// Measured, rather than read out of the solver. What the solver holds in a manifold depends
        /// on how the bodies came together: it only ever pushes a pair apart, never together, so a
        /// replay of a recorded overlap ends up more separated than it was recorded and the manifold
        /// is dropped, then re-aimed into existence, then dropped again. Distance between shapes has
        /// no such history and answers the same going either way.
        /// </summary>
        HashSet<string> Touching()
        {
            var touching = new HashSet<string>();

            var others = Sim.I.Bodies;

            for (int i = 0; i < others.Count; i++)
            {
                var other = others[i].GetComponent<SimRigidbody>();

                if (other == null || other == this) continue;

                if (Gap(other) < contactRange) touching.Add(others[i].Id);
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
