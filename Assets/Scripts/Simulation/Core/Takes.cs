using System;
using System.Collections.Generic;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The stack of takes, and the undo cursor into it.
    ///
    /// A take is one stretch of the world re-run over ground that has already been run. Recordings
    /// are kept in a layer per take, so a take is in force simply by being on the stack: reading a
    /// tick means taking the newest layer that has it, and undo is this class saying the top of the
    /// stack is no longer there. Nothing is moved and nothing is restored.
    ///
    /// This class does not decide when a take opens. The player does, by taking a character --
    /// every time, see Sim.PlayerClaims.
    ///
    /// Take 1 is the world's first pass. It cannot be undone -- there is nothing to go back to.
    /// </summary>
    public sealed class Takes
    {
        /// The answer when nothing has recorded a tick. Never a real take number.
        public const int None = 0;

        struct Entry
        {
            /// The cursor tick when this take opened. Owned by whatever came before rather than by
            /// this take -- it is the state the take started from, and where undo returns to.
            public int Start;

            /// The range of ticks this take has run, Start included.
            public int Min, Max;

            /// The end of the run away from Start, which is where redo returns to. Which of the two
            /// that is depends on which way the take travels, so it is asked rather than stored.
            public int Far => Start == Min ? Max : Min;
        }

        /// Index 0 is the entry for "no take", so a take number can be used as an index directly.
        readonly List<Entry> entries = new List<Entry> { default, default };

        /// The newest take on the stack.
        public int Live { get; private set; } = 1;

        /// How many takes exist, including undone ones waiting to be redone.
        public int Count => entries.Count - 1;

        public bool CanUndo => Live > 1;
        public bool CanRedo => Live < Count;

        /// Where undo would put the cursor, and where redo would.
        public int UndoTick => entries[Live].Start;
        public int RedoTick => entries[Live + 1].Far;

        /// <summary>
        /// Open a take running from the cursor, and return its number.
        ///
        /// Drops any undone takes: re-recording is what makes a redo unreachable, and the layers
        /// filed under those numbers have to go with them before anything writes to the number this
        /// take has just been given.
        /// </summary>
        public int Open(int cursor)
        {
            entries.RemoveRange(Live + 1, Count - Live);
            entries.Add(new Entry { Start = cursor, Min = cursor, Max = cursor });

            return ++Live;
        }

        /// Note that the live take has now run this tick.
        public void Extend(int tick)
        {
            var entry = entries[Live];

            entry.Min = Math.Min(entry.Min, tick);
            entry.Max = Math.Max(entry.Max, tick);

            entries[Live] = entry;
        }

        /// Take the newest take off the stack. Wind to UndoTick first -- popping mid-wind would
        /// leave the bodies with nothing covering the ticks being crossed.
        public void Undo() => Live--;

        /// Put the last undone take back. Do this before winding to RedoTick, so the recording
        /// being redone is what plays on the way there.
        public void Redo() => Live++;
    }
}
