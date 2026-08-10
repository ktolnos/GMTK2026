using System;

namespace Chronomancers.Sim
{
    /// <summary>
    /// A position on (or a signed distance along) the loop's timeline, as fixed-point seconds.
    /// <para>
    /// Fixed-point rather than float because this is a lookup key: spans compare against it,
    /// samples are sorted by it, and it is accumulated tens of thousands of times per loop. Float
    /// would drift and make equality meaningless. The unit is a power of ten so raw values stay
    /// readable in a debugger — raw 12345 is plainly 1.2345 s.
    /// </para>
    /// <para>
    /// One type serves both instants and durations, as <c>TimeSpan</c> does; all the arithmetic
    /// here is affine, so splitting them buys friction rather than safety.
    /// </para>
    /// </summary>
    [Serializable]
    public readonly struct LoopTime : IEquatable<LoopTime>, IComparable<LoopTime>
    {
        public const int UnitsPerSecond = 10_000;

        /// <summary>Fixed-point value in 1/<see cref="UnitsPerSecond"/> of a second.</summary>
        public readonly int Raw;

        public LoopTime(int raw) => Raw = raw;

        public static readonly LoopTime Zero = default;

        public static LoopTime FromRaw(int raw) => new(raw);
        public static LoopTime FromSeconds(double seconds) => new(checked((int)Math.Round(seconds * UnitsPerSecond)));

        public double Seconds => (double)Raw / UnitsPerSecond;
        public float SecondsF => (float)Raw / UnitsPerSecond;

        /// <summary>
        /// Where <paramref name="at"/> falls between <paramref name="a"/> and <paramref name="b"/>:
        /// 0 at a, 1 at b, clamped. Returns 0 for a degenerate interval.
        /// </summary>
        public static float InverseLerp(LoopTime a, LoopTime b, LoopTime at)
        {
            var span = b.Raw - a.Raw;
            if (span == 0) return 0f;
            var t = (float)(at.Raw - a.Raw) / span;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        public static LoopTime Clamp(LoopTime value, LoopTime min, LoopTime max) =>
            value.Raw < min.Raw ? min : value.Raw > max.Raw ? max : value;

        public static LoopTime operator +(LoopTime a, LoopTime b) => new(a.Raw + b.Raw);
        public static LoopTime operator -(LoopTime a, LoopTime b) => new(a.Raw - b.Raw);

        public static bool operator <(LoopTime a, LoopTime b) => a.Raw < b.Raw;
        public static bool operator >(LoopTime a, LoopTime b) => a.Raw > b.Raw;
        public static bool operator <=(LoopTime a, LoopTime b) => a.Raw <= b.Raw;
        public static bool operator >=(LoopTime a, LoopTime b) => a.Raw >= b.Raw;
        public static bool operator ==(LoopTime a, LoopTime b) => a.Raw == b.Raw;
        public static bool operator !=(LoopTime a, LoopTime b) => a.Raw != b.Raw;

        public bool Equals(LoopTime other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is LoopTime other && Raw == other.Raw;
        public override int GetHashCode() => Raw;
        public int CompareTo(LoopTime other) => Raw.CompareTo(other.Raw);
        public override string ToString() => $"{Seconds:0.####}s";
    }
}