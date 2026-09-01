using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// One recordable aspect of a body: where it is, how much health it has, whether a door is open.
    ///
    /// The non-generic base exists so SimBody can hold a plain array of them; nothing outside the
    /// simulation should derive from it directly. Derive from SimComponent&lt;TState&gt;.
    /// </summary>
    public abstract class SimComponent : MonoBehaviour
    {
        SimBody body;

        /// The body this belongs to. Resolved on demand rather than in Awake so subclasses do not
        /// have to remember to call base.Awake().
        public SimBody Body
        {
            get
            {
                if (body == null) body = GetComponent<SimBody>();
                return body;
            }
        }

        /// Before the solver: get ready to be moved by it, or aim at the recorded pose so it sweeps
        /// us there.
        public abstract void PrepareStep(in SimStep step);

        /// After the solver: write down what it did to us, if we were the one being simulated.
        public abstract void CommitStep(in SimStep step);

        /// Once a frame, presentation only.
        public abstract void Show(in SimShow show);

        /// Throw away the recordings of takes that a new claim has made unreachable.
        public abstract void DropTakesAbove(int take);
    }

    /// <summary>
    /// A component that records a TState per tick.
    /// </summary>
    public abstract class SimComponent<TState> : SimComponent where TState : struct
    {
        protected readonly Layers<TState> layers = new Layers<TState>();

        /// <summary>
        /// Read whatever the timeline currently says about this tick, if anything does.
        ///
        /// Resolves the layer itself rather than using the step's, because it is for looking at
        /// ticks other than the one being stepped -- differencing a velocity out of the last two
        /// poses, drawing between two of them.
        /// </summary>
        protected bool TryRead(int tick, out TState state)
        {
            int take = layers.Resolve(Sim.I.Takes.Live, tick);

            if (take == Takes.None)
            {
                state = default;
                return false;
            }

            state = layers.Read(take, tick);
            return true;
        }

        public sealed override void PrepareStep(in SimStep step)
        {
            if (step.IsRecording) PrepareRecording(step);

            // Takes.None while replaying means the cursor is seeking across ticks nothing
            // has a recording of. There is nothing to apply and nothing to record, so we sit still.
            else if (step.Take != Takes.None) Replay(new SimReplay<TState>(step, layers.Read(step.Take, step.Tick)));
        }

        public sealed override void CommitStep(in SimStep step) =>
            layers.Write(step.Take, step.Tick, Capture(step));

        public sealed override void Show(in SimShow show)
        {
            if (TryRead(show.FromTick, out var from) && TryRead(show.ToTick, out var to))
                Show(new SimBlend<TState>(show, from, to));
        }

        public sealed override void DropTakesAbove(int take) => layers.DropAbove(take);

        /// <summary>
        /// We are live this step -- the solver is about to move us and whatever it does is the
        /// recording. There is no state to apply, so most components have nothing to do here. It is
        /// where a motion component turns its rigidbody dynamic.
        /// </summary>
        protected virtual void PrepareRecording(in SimStep step) { }

        /// Put a recorded state back onto the object, before the solver runs.
        protected abstract void Replay(in SimReplay<TState> replay);

        /// Read the current state off the object, after the solver has run.
        protected abstract TState Capture(in SimStep step);

        /// Draw somewhere between two recorded states. Presentation only -- this must not touch
        /// anything the solver reads, because it runs on frames that no step happened on.
        protected abstract void Show(in SimBlend<TState> blend);
    }
}
