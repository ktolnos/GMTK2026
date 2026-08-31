using System;

namespace Chronomancers.Sim
{
    /// <summary>
    /// One thing's recording within one take: a slot per tick, growable in both directions.
    ///
    /// Ticks are absolute and may be negative. The loop has no fixed start, so a body claimed at
    /// tick 0 and recording backwards runs off the front and storage follows it. Internally this is
    /// a flat array plus the tick that slot 0 stands for -- growing reallocates and copies, which is
    /// rare because the cursor only ever moves one tick at a time and growth doubles.
    ///
    /// Written ticks are one unbroken stretch, so First..Last describes the set exactly.
    ///
    /// Not Unity-serialised. The state structs it holds are, but a 3000-entry array in the
    /// inspector would be useless; saving goes through a writer that walks ticks explicitly.
    /// </summary>
    public sealed class History<T> where T : struct
    {
        const int InitialCapacity = 256;

        T[] slots = new T[InitialCapacity];

        /// The tick that slots[0] stands for.
        int origin;

        bool InRange(int tick) => tick >= origin && tick < origin + slots.Length;

        /// Lowest tick written, and highest. First > Last while nothing has been.
        public int First { get; private set; } = int.MaxValue;
        public int Last { get; private set; } = int.MinValue;

        /// Whether this tick was written.
        public bool Has(int tick) => tick >= First && tick <= Last;

        public T this[int tick]
        {
            get
            {
                if (Has(tick)) return slots[tick - origin];

                Console.Error.WriteLine(
                    $"History<{typeof(T).Name}>: read of tick {tick}, which was never written. " +
                    $"Resolve which take owns a tick before reading it.");
                return default;
            }
            set
            {
                if (First <= Last && (tick < First - 1 || tick > Last + 1))
                {
                    Console.Error.WriteLine(
                        $"History<{typeof(T).Name}>: write of tick {tick} leaves a gap in {First}..{Last}. " +
                        "A recording is one unbroken stretch; whoever chose this take to write into " +
                        "picked one whose recording is somewhere else entirely.");
                    return;
                }

                Grow(tick);
                slots[tick - origin] = value;
                First = Math.Min(First, tick);
                Last = Math.Max(Last, tick);
            }
        }

        /// <summary>
        /// Make sure a slot exists for this tick, reallocating if it falls outside the array.
        /// Capacity doubles, and the spare room is placed on whichever side we grew toward, so a
        /// body recording steadily in one direction reallocates O(log n) times rather than
        /// every tick.
        /// </summary>
        void Grow(int tick)
        {
            if (InRange(tick)) return;

            int newOrigin = Math.Min(origin, tick);
            int needed = Math.Max(origin + slots.Length - 1, tick) - newOrigin + 1;

            int capacity = slots.Length;
            while (capacity < needed) capacity *= 2;

            // Growing at the front means the new slots have to sit before the old ones, so the
            // spare capacity goes there and origin moves down past it.
            if (tick < origin) newOrigin -= capacity - needed;

            var grown = new T[capacity];
            Array.Copy(slots, 0, grown, origin - newOrigin, slots.Length);
            slots = grown;
            origin = newOrigin;
        }
    }
}
