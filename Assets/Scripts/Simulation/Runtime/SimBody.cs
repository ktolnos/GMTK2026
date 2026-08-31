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
        /// What the take stack records as having been claimed.
        ///
        /// A string rather than this component, so the undo stack means something after it has
        /// been written to disk and read back, and so a body pooled out and back in is still the
        /// same character rather than a new one whose earlier takes nothing supersedes.
        /// </summary>
        public string Id => id;

        void Reset() => id = System.Guid.NewGuid().ToString("N");

        void OnValidate()
        {
            // Duplicating a GameObject copies the id with it, and two bodies claiming to be the
            // same character would supersede each other's takes.
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

        /// Which take answers for this tick, or Takes.None if nothing in force recorded it.
        public int TakeAt(int tick) => layers.Resolve(Sim.I.Takes, tick);

        /// Whether anything in force has a recording of this tick.
        public bool HasState(int tick) => TakeAt(tick) != Takes.None;

        ///  Before the solver
        public void PrepareStep(int tick, int dir)
        {
            int read = TakeAt(tick);
            bool claimed = IsSimulated && dir == RecordDir;

            // Nothing in force recorded this tick, so the body has to be somewhere and records
            // whichever way the cursor is going -- unless the cursor is only winding, in which case
            // it is passing through rather than living through, and writes nothing.
            bool recording = !Sim.I.IsWinding && (claimed || read == Takes.None);

            step = new SimStep(tick, dir, recording, recording ? WriteTake(claimed, tick - dir) : read);

            foreach (var part in parts)
                part.PrepareStep(step);
        }

        /// <summary>
        /// Which take to record this tick under.
        ///
        /// A take that is running owns everything simulated while it runs, claimed or not. That is
        /// what makes a shoved crate belong to the take that shoved it: undo that take and the
        /// crate loses the shove, because the recording of it went away with the take.
        ///
        /// With nothing running the cursor is only wandering -- off the front of the first pass, or
        /// past the end of what anyone recorded -- and there is no take that can be said to have
        /// caused what happens. Growing whatever answered for the tick we came from keeps that
        /// recording one unbroken stretch, which is the only thing that matters there.
        /// </summary>
        int WriteTake(bool claimed, int cameFrom)
        {
            if (claimed || Sim.I.IsRerunning) return Sim.I.Takes.Live;

            int neighbour = TakeAt(cameFrom);

            // Only at a cold start, where the live take has recorded nothing either.
            return neighbour == Takes.None ? Sim.I.Takes.Live : neighbour;
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

        /// Throw away the recordings of takes a new claim has made unreachable.
        public void DropTakesAbove(int take)
        {
            layers.DropAbove(take);

            foreach (var part in parts)
                part.DropTakesAbove(take);
        }
    }
}
