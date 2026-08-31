using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Where a body is, recorded as a pose per tick.
    ///
    /// This is the component that turns the record-or-replay decision into physics. Recording means
    /// <b>dynamic</b>: the solver moves the body and whatever it does is the recording. Replaying
    /// means <b>kinematic</b> and aimed with MovePosition.
    /// 
    /// <b>Velocity is not recorded.</b> When a body is claimed it is worked out from the last two
    /// poses it was replaying.
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
        }

        [Tooltip("Transform the sprite lives on. Drawing runs one tick behind the solver, so this " +
                 "must not be the rigidbody's own transform: writing a stale pose onto a dynamic " +
                 "body would feed it straight back into the next step.")]
        [SerializeField] Transform view;

        Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // A sleeping body ignores MovePosition, which would silently freeze playback.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            Debug.Assert(view != null && view != transform,
                $"{name} needs a separate view transform; drawing must not move the rigidbody.",
                this);
        }

        protected override void PrepareRecording(in SimStep step)
        {
            if (rb.bodyType == RigidbodyType2D.Dynamic) return;

            rb.bodyType = RigidbodyType2D.Dynamic;

            // The last tick we replayed, and the one before it in recording order. Under a
            // descending cursor "before" is the numerically larger tick, which is exactly why the
            // subtraction below comes out negated without anyone asking it to.
            int last = step.Previous;
            int beforeLast = last - step.Dir;

            if (TryRead(beforeLast, out State from) && TryRead(last, out State to))
            {
                rb.linearVelocity = (to.Position - from.Position) / Sim.SecondsPerTick;
                rb.angularVelocity = Mathf.DeltaAngle(from.Rotation, to.Rotation) / Sim.SecondsPerTick;
            }
            else
            {
                // Nothing to difference: this body has never been anywhere. Starting at rest is the
                // honest answer, and it is what happens on the first step of a session.
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        /// Aim at the recorded pose. MovePosition rather than a teleport so the step sweeps us
        /// there, and momentum still transfers to anything in the way.
        protected override void Replay(in SimReplay<State> replay)
        {
            if (rb.bodyType != RigidbodyType2D.Kinematic)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;

                // A kinematic body still integrates its velocity, which would fight MovePosition.
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            rb.MovePosition(replay.State.Position);
            rb.MoveRotation(replay.State.Rotation);
        }

        protected override State Capture(in SimStep step) => new State
        {
            Position = rb.position,
            Rotation = rb.rotation,
        };

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
