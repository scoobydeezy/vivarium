using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// A stable, human-readable content identifier such as <c>trait.ambitious</c>,
    /// <c>activity.traveling</c>, or <c>rng.decision.influence_roll</c> (Architecture §7).
    /// <para>
    /// Authored ids survive builds, patches, and saves. They are compared and hashed through
    /// <see cref="StableHash"/> so that no authoritative behaviour depends on the runtime-dependent
    /// <see cref="string.GetHashCode()"/> (§14).
    /// </para>
    /// </summary>
    public readonly struct AuthoredId : IEquatable<AuthoredId>, IComparable<AuthoredId>
    {
        /// <summary>The unset id. Valid as a sentinel, never valid as authoritative content.</summary>
        public static readonly AuthoredId None = default;

        private readonly string _value;

        public AuthoredId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>The raw authored string, or <c>null</c> when unset.</summary>
        public string Value => _value;

        public bool IsSet => _value != null;

        /// <summary>
        /// Deterministic 64-bit hash of the authored text. Stable across processes, platforms, and
        /// runtime versions, so it is safe to use as a random-oracle seed component.
        /// </summary>
        public ulong StableHashCode => StableHash.OfString(_value);

        public bool Equals(AuthoredId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AuthoredId other && Equals(other);

        /// <summary>
        /// Truncated <see cref="StableHashCode"/>. Deterministic, unlike <see cref="string.GetHashCode()"/>,
        /// so hash containers keyed by <see cref="AuthoredId"/> behave identically between runs.
        /// </summary>
        public override int GetHashCode() => (int)(StableHashCode ^ (StableHashCode >> 32));

        public int CompareTo(AuthoredId other) => string.CompareOrdinal(_value, other._value);

        public override string ToString() => _value ?? "<none>";

        public static bool operator ==(AuthoredId left, AuthoredId right) => left.Equals(right);

        public static bool operator !=(AuthoredId left, AuthoredId right) => !left.Equals(right);

        public static implicit operator AuthoredId(string value) => new AuthoredId(value);
    }
}
