using System.Collections.Generic;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The stack of takes, and the undo cursor into it.
    ///
    /// A take is one stretch of the world being re-run under one claim. Recordings are kept in a
    /// layer per take, so undoing one is not a matter of restoring anything -- it is this class
    /// saying the take is no longer in force, and the layer beneath becoming visible again.
    ///
    /// Takes 1..Live are in force. Anything above Live has been undone and can be redone, until
    /// something is claimed, at which point that branch is dropped for good. There is no tree: a
    /// new take after an undo throws the redone future away, the way undo has always worked.
    ///
    /// Take 1 is the world's first pass, before anyone claimed anything. It has no claimant and
    /// cannot be undone -- there is nothing to go back to.
    /// </summary>
    public sealed class Takes
    {
        /// The answer when nothing has recorded a tick. Never a real take number.
        public const int None = 0;

        struct Entry
        {
            /// Which body was claimed to open this take, by SimBody.Id. Null for the first pass,
            /// which nobody claimed. An id rather than the body itself so that the stack survives
            /// being written to disk, and so that a body destroyed and respawned -- pooled out and
            /// back in -- is still recognised as the same character.
            public string Claimant;

            /// The cursor tick when the take opened. Owned by whatever came before, not by this
            /// take -- it is the state the take started from, and where undo returns to.
            public int Start;

            /// The furthest tick this take re-ran. Where redo returns to.
            public int End;
        }

        /// Index 0 is the entry for "no take", so a take number can be used as an index directly.
        readonly List<Entry> entries = new List<Entry> { default, default };

        /// The newest take in force.
        public int Live { get; private set; } = 1;

        /// How many takes exist, including undone ones waiting to be redone.
        public int Count => entries.Count - 1;

        public bool CanUndo => Live > 1;
        public bool CanRedo => Live < Count;

        /// Where undo would put the cursor, and where redo would.
        public int UndoTick => entries[Live].Start;
        public int RedoTick => entries[Live + 1].End;

        /// <summary>
        /// Open a take in which <paramref name="claimant"/> re-runs the world from the cursor, and
        /// return its number. Drops any undone takes: acting is what makes a redo unreachable.
        ///
        /// It puts this claimant's earlier takes out of force from here on, and that is the whole
        /// re-recording rule. Everything a takeover cascaded -- a crate it shoved -- recorded into
        /// that same take, so it goes with it and stops replaying a shove that no longer happens.
        /// Takes belonging to <i>other</i> claimants are untouched, so the characters you are not
        /// re-recording go on performing.
        /// </summary>
        public int Open(string claimant, int cursor)
        {
            entries.RemoveRange(Live + 1, Count - Live);
            entries.Add(new Entry { Claimant = claimant, Start = cursor, End = cursor });

            return ++Live;
        }

        /// Note how far the live take has re-run, for redo to return to.
        public void Extend(int tick)
        {
            var entry = entries[Live];
            entry.End = tick;
            entries[Live] = entry;
        }

        /// <summary>
        /// Whether a recording made in this take is still the truth at this tick.
        ///
        /// It is not, if the same claimant has taken over again since from at or before this tick.
        /// Re-recording a character supersedes what they did before, and everything that takeover
        /// cascaded -- a crate it shoved -- recorded into the same take, so it goes with them.
        ///
        /// Worked out from the stack rather than kept as a field, so undo puts it back by itself.
        /// A stored voided-from tick would outlive the undo of the take that set it, and leave a
        /// performance deleted by the very thing that was just taken back.
        /// </summary>
        public bool InForce(int take, int tick)
        {
            if (take <= None || take > Live) return false;

            string claimant = entries[take].Claimant;

            for (int later = take + 1; later <= Live; later++)
                if (entries[later].Claimant == claimant && tick > entries[later].Start)
                    return false;

            return true;
        }

        /// Put the newest take out of force. Seek to UndoTick first -- popping mid-seek would leave
        /// the bodies replaying a layer that no longer covers the ticks being crossed.
        public void Undo() => Live--;

        /// Put the last undone take back in force. Do this before seeking to RedoTick, so the
        /// recording being redone is what plays on the way there.
        public void Redo() => Live++;
    }
}
