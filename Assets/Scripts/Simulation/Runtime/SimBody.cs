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

        /// Start writing history with this body, laying it down as the cursor travels dir. Claiming
        /// something already claimed is harmless.
        public void Claim(int dir)
        {
            RecordDir = dir;
            IsSimulated = true;
        }

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

        /// What this body is doing this tick, settled before the solver and read again after it.
        SimStep step;

        /// <summary>
        /// Before the solver: settle what this body is doing and hand it to every part.
        ///
        /// Divergence belongs here, ahead of applying anything. A body whose recording no longer
        /// fits the world around it is claimed now, so it is never driven towards a pose that
        /// recording asked for -- and so it carries none of that pose's velocity afterwards.
        /// </summary>
        public void PrepareStep(int tick, int dir)
        {
            step = Decide(tick, dir);

            if (!step.IsRecording && Diverged(step))
            {
                Claim(dir);
                step = Decide(tick, dir);
            }

            foreach (var part in parts)
                part.PrepareStep(step);
        }

        /// After the solver: write down what it did to us, if we were the one being simulated.
        public void CommitStep(int tick, int dir)
        {
            Debug.Assert(step.Tick == tick && step.Dir == dir,
                $"{name} is committing t{tick} having prepared t{step.Tick}.", this);

            if (!step.IsRecording) return;

            foreach (var part in parts)
                part.CommitStep(step);

            layers.Write(step.Take, tick, default);
        }

        /// Record or replay, and which layer answers -- the one decision behind both halves of a
        /// step, so neither half can hold a different view of it.
        SimStep Decide(int tick, int dir)
        {
            int read = layers.Resolve(Sim.I.Takes.Live, tick);

            // IsSimulated alone: Sim has already released anyone the cursor is travelling against,
            // so a claim that survives to here agrees with the direction by construction.
            //
            // Nothing recorded this tick, so the body has to be somewhere and records whichever way
            // the cursor is going -- unless the cursor is only winding, in which case it is passing
            // through rather than living through, and writes nothing.
            bool recording = !Sim.I.IsWinding && (IsSimulated || read == Takes.None);

            if (!recording) return new SimStep(tick, dir, false, read);

            // Replacing something goes into the live take, because the live take is what undo
            // lifts. Ground nobody has covered replaces nothing, so it continues whichever layer
            // already ends next to it: a layer is one unbroken stretch, and starting the same body
            // higher up would leave a hole in the one below and put untouched ground on the stack.
            int join = read != Takes.None ? Takes.None : layers.Resolve(Sim.I.Takes.Live, tick - dir);

            return new SimStep(tick, dir, true, join != Takes.None ? join : Sim.I.Takes.Live);
        }

        /// Whether any part of this body finds the world no longer fits what it is about to replay.
        /// A wind is passing through the world rather than living through it, so it claims nothing.
        bool Diverged(in SimStep step)
        {
            if (Sim.I.IsWinding) return false;

            foreach (var part in parts)
                if (part.Diverged(step))
                    return true;

            return false;
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
