using System.Collections.Generic;

namespace Chronomancers.Sim
{
    /// <summary>
    /// One thing's recording, kept as a layer per take.
    ///
    /// Recording a tick never overwrites what an earlier take put there; it writes into that take's
    /// own layer, on top. So undo has nothing to restore -- taking a take off the stack is enough
    /// for the layer beneath to become visible again -- and re-recording the same stretch a dozen
    /// times costs a dozen layers.
    ///
    /// Layers are indexed by take number and allocated only when a take actually writes to one.
    /// </summary>
    public sealed class Layers<T> where T : struct
    {
        /// Index 0 is the layer for "no take", so a take number indexes this directly.
        readonly List<History<T>> layers = new List<History<T>> { null };

        /// Whether this take wrote anything at all. A layer exists only once written to.
        public bool Any(int take) =>
            take > Takes.None && take < layers.Count && layers[take] != null;

        /// Whether this take wrote this tick.
        public bool Has(int take, int tick) =>
            take > Takes.None && take < layers.Count &&
            layers[take] != null && layers[take].Has(tick);

        /// <summary>
        /// Which take's recording of this tick is the truth, or Takes.None if none of them is.
        ///
        /// The newest layer on the stack that wrote the tick. Coming up empty means nothing ever recorded
        /// here, and is the signal to record rather than replay.
        /// </summary>
        public int Resolve(int live, int tick)
        {
            for (int take = live; take > Takes.None; take--)
                if (Has(take, tick))
                    return take;

            return Takes.None;
        }

        public T Read(int take, int tick) => layers[take][tick];

        public void Write(int take, int tick, in T value)
        {
            while (layers.Count <= take) layers.Add(null);

            layers[take] ??= new History<T>();
            layers[take][tick] = value;
        }

        /// Forget every take above this one, because a new take has made them unreachable. This is
        /// the only place a recording is ever actually thrown away.
        public void DropAbove(int take)
        {
            if (layers.Count > take + 1)
                layers.RemoveRange(take + 1, layers.Count - take - 1);
        }
    }
}
