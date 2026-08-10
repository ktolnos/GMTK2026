using System;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The single cursor into the loop's timeline, plus the rate it moves at.
    /// <para>
    /// There is exactly one cursor. Recording only ever happens in the current direction, and
    /// switching characters happens at a single instant, so every body — playing back or recording —
    /// is always at the same loop time. That is what makes interactions reproducible: they always
    /// occur between bodies at equal loop time, so replaying the timeline reproduces them by
    /// construction.
    /// </para>
    /// <para>
    /// <see cref="Rate"/> comes from the <i>watched</i> character (which may itself be on playback),
    /// signed for direction. A character in bullet time therefore looks normal while watched and
    /// fast to everyone else, with no code for it: its samples are simply dense in loop time.
    /// </para>
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>Sub-unit precision of the advance accumulator.</summary>
        const long SubUnits = 10_000;

        long _remainder;

        public SimClock(LoopTime length) => Length = length;

        /// <summary>Duration of one loop. The cursor is clamped to [0, Length].</summary>
        public LoopTime Length { get; }

        public LoopTime Cursor { get; private set; }

        /// <summary>
        /// Loop-seconds per real second, signed. Zero freezes the timeline, and a frozen clock
        /// records nothing at all.
        /// </summary>
        public float Rate { get; set; } = 1f;

        public int Dir => Rate > 0f ? 1 : Rate < 0f ? -1 : 0;
        public bool Frozen => Rate == 0f;
        public bool AtStart => Cursor.Raw <= 0;
        public bool AtEnd => Cursor.Raw >= Length.Raw;
        public bool AtBoundary => AtStart || AtEnd;

        public void Seek(LoopTime at)
        {
            Cursor = LoopTime.Clamp(at, LoopTime.Zero, Length);
            _remainder = 0;
        }

        /// <summary>
        /// Advances the cursor by one physics step and returns the signed distance actually moved —
        /// zero when frozen, when clamped at a loop boundary, or when the rate is small enough that
        /// less than one unit has accrued. Callers record a sample only when this is non-zero.
        /// <para>
        /// <paramref name="stepSeconds"/> stays a <c>double</c> and is folded into a sub-unit
        /// accumulator on purpose: 1/60 s is not representable in a power-of-ten fixed point, so
        /// quantising the step itself would drift ~0.12% (about 72 ms per minute-long loop). This way
        /// the error stays bounded at one unit instead of accumulating. At Unity's default 0.02 s
        /// step it is exact regardless.
        /// </para>
        /// </summary>
        public LoopTime Advance(double stepSeconds)
        {
            if (Rate == 0f || stepSeconds == 0d) return LoopTime.Zero;

            _remainder += (long)Math.Round(Rate * stepSeconds * LoopTime.UnitsPerSecond * SubUnits);

            var delta = (int)(_remainder / SubUnits);
            if (delta == 0) return LoopTime.Zero;
            _remainder -= delta * SubUnits;

            var target = LoopTime.FromRaw(Cursor.Raw + delta);
            var clamped = LoopTime.Clamp(target, LoopTime.Zero, Length);
            if (clamped.Raw != target.Raw) _remainder = 0;

            var moved = clamped - Cursor;
            Cursor = clamped;
            return moved;
        }
    }
}
