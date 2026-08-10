using System;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Stable identity of a simulated body.
    /// <para>
    /// Authored bodies take their value from the scene. Runtime spawns MUST derive theirs via
    /// <see cref="Spawn"/>: replaying a take has to reproduce byte-identical ids, which
    /// <c>Guid.NewGuid()</c> or an incrementing instance counter cannot do.
    /// </para>
    /// </summary>
    [Serializable]
    public readonly struct SimId : IEquatable<SimId>, IComparable<SimId>
    {
        public readonly int Value;

        public SimId(int value) => Value = value;

        public static readonly SimId None = default;

        public bool IsValid => Value != 0;

        /// <summary>
        /// Deterministic id for a body spawned by <paramref name="spawner"/> at
        /// <paramref name="at"/>. <paramref name="ordinal"/> disambiguates several spawns by the
        /// same spawner at the same instant (a shotgun) and must itself be deterministic — an index
        /// within the pellet loop, not a global counter.
        /// </summary>
        public static SimId Spawn(SimId spawner, LoopTime at, int ordinal)
        {
            unchecked
            {
                const int prime = 16777619;
                var h = (int)2166136261u;
                h = (h ^ spawner.Value) * prime;
                h = (h ^ at.Raw) * prime;
                h = (h ^ ordinal) * prime;
                return new SimId(h == 0 ? 1 : h);
            }
        }

        public bool Equals(SimId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SimId other && Value == other.Value;
        public override int GetHashCode() => Value;
        public int CompareTo(SimId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsValid ? $"#{Value:X8}" : "#none";

        public static bool operator ==(SimId a, SimId b) => a.Value == b.Value;
        public static bool operator !=(SimId a, SimId b) => a.Value != b.Value;
    }
}