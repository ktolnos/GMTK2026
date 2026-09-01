using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// One simulated object: the thing that decides whether it is writing history or reading it,
    /// and hands that decision to the SimComponents sitting beside it.
    ///
    /// This class knows nothing about rigidbodies, sprites or collision. It owns identity, the
    /// record-or-replay decision, and which take answers for a tick. Everything about <i>how</i> a
    /// body moves or looks belongs to a component.
    /// </summary>
    public class SimBody : MonoBehaviour
    {
        [Tooltip("Who this body is, across sessions and across being destroyed and respawned. " +
                 "Generated when the component is added; do not edit it, and do not paste one " +
                 "body's over another's.")]
        [SerializeField] string id = "";

        /// <summary>
        /// Who this body is, for anything that has to name it across a round trip to disk.
        ///
        /// Nothing at runtime reads it today.
        /// </summary>
        public string Id => id;

        void Reset() => id = System.Guid.NewGuid().ToString("N");

        void OnValidate()
        {
            // Duplicating a GameObject copies the id with it, and two bodies answering to the same
            // name would be indistinguishable once a recording is read back from disk.
            if (id.Length == 0 || IsIdTaken()) id = System.Guid.NewGuid().ToString("N");
        }

        bool IsIdTaken()
        {
            foreach (var other in FindObjectsByType<SimBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != this && other.id == id)
                    return true;

            return false;
        }

        /// <summary>
        /// Something has taken this body live, so it records even where history already exists --
        /// overwriting it.
        /// </summary>
        public bool IsSimulated { get; set; }

        /// <summary>
        /// Which way the cursor has to be travelling for this body to be writing history: +1 for
        /// anything ordinary, -1 for a body that was inverted.
        ///
        /// Being claimed is not enough to record, because seeking is how you look at what you did
        /// and it must not overwrite it. Recording during a backward pass is a real thing -- it is
        /// how inverted objects come to exist -- but it takes the reversal machine to flip this,
        /// not a held key. Whoever claims a body sets it.
        /// </summary>
        public int RecordDir { get; set; } = 1;

        /// The step currently being taken. Set in PrepareStep and read again in CommitStep, so the
        /// two sides of the solver cannot disagree about what was decided.
        SimStep step;

        public bool IsRecording => step.IsRecording;

        /// <summary>
        /// Empty for now. It exists because this body's layers are the authority on which take
        /// answers for a tick, and every component follows what it decides. Whether the object is
        /// in the world at all is what will fill it in.
        /// </summary>
        public struct State { }

        readonly Layers<State> layers = new Layers<State>();

        SimComponent[] parts;

        void Awake()
        {
            parts = GetComponents<SimComponent>();
        }

        void OnEnable() => Sim.I.Register(this);

        void OnDisable()
        {
            if (Sim.I != null) Sim.I.Unregister(this);
        }

        /// Which take answers for this tick, or Takes.None if nothing ever recorded it.
        public int TakeAt(int tick) => layers.Resolve(Sim.I.Takes.Live, tick);

        /// Whether anything has a recording of this tick.
        public bool HasState(int tick) => TakeAt(tick) != Takes.None;

        /// <summary>
        /// Whether recording this tick would need a take of its own.
        ///
        /// A layer is one unbroken stretch, so this body can go on writing into the live take only
        /// where that leaves it unbroken: the tick has to be new to the layer, and next to what the
        /// layer already holds. Everything else needs a fresh layer, which means a fresh take --
        /// re-running ground this take already recorded, or coming back to recorded ground after
        /// wandering off the end of it.
        ///
        /// Asked of every body before the step, because the take is shared: one body needing a new
        /// one is enough, and the rest simply start fresh layers in it.
        /// </summary>
        public bool NeedsOwnTake(int tick, int dir)
        {
            // A wind writes nothing, and neither does a body reading back what it already did.
            if (Sim.I.IsWinding) return false;
            if (!IsSimulated && TakeAt(tick) != Takes.None) return false;

            int live = Sim.I.Takes.Live;

            // Already ours in this take: writing it again would overwrite inside one layer, which
            // is the one thing a layer cannot survive.
            if (layers.Has(live, tick)) return true;

            // Not adjacent to what we have in this take, and we do have something: a hole.
            return layers.Any(live) && !layers.Has(live, tick - dir);
        }

        ///  Before the solver
        public void PrepareStep(int tick, int dir)
        {
            int read = TakeAt(tick);

            // IsSimulated alone: Sim has already released anyone the cursor is travelling against,
            // so a claim that survives to here agrees with the direction by construction.
            //
            // Nothing recorded this tick, so the body has to be somewhere and records whichever way
            // the cursor is going -- unless the cursor is only winding, in which case it is passing
            // through rather than living through, and writes nothing.
            bool recording = !Sim.I.IsWinding && (IsSimulated || read == Takes.None);

            // Always the live take, whether this is a claim overwriting a recording or a body at
            // the frontier inventing one. Everything simulated while a take runs belongs to it.
            step = new SimStep(tick, dir, recording, recording ? Sim.I.Takes.Live : read);

            foreach (var part in parts)
                part.PrepareStep(step);
        }

        /// After the solver. Write down what happened, if we were the one who caused it.
        public void CommitStep()
        {
            if (!step.IsRecording) return;

            foreach (var part in parts)
                part.CommitStep(step);

            layers.Write(step.Take, step.Tick, default);
        }

        /// Once a frame, presentation only. See the contract on Sim.Show.
        public void Show(in SimShow show)
        {
            foreach (var part in parts)
                part.Show(show);
        }

        /// Throw away the recordings of takes a new take has made unreachable.
        public void DropTakesAbove(int take)
        {
            layers.DropAbove(take);

            foreach (var part in parts)
                part.DropTakesAbove(take);
        }
    }
}
