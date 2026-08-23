using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    /// <summary>A compact, deterministic vector keyed by stable authored dimension ids.</summary>
    public sealed class SocialVector
    {
        private readonly SortedDictionary<AuthoredId, long> _values = new SortedDictionary<AuthoredId, long>();

        public SocialVector()
        {
        }

        public SocialVector(IEnumerable<KeyValuePair<AuthoredId, long>> values)
        {
            if (values == null)
            {
                return;
            }

            foreach (KeyValuePair<AuthoredId, long> pair in values)
            {
                Set(pair.Key, pair.Value);
            }
        }

        public int Count => _values.Count;

        public IEnumerable<KeyValuePair<AuthoredId, long>> All => _values;

        public long this[AuthoredId dimension] => Get(dimension);

        public long Get(AuthoredId dimension) => _values.TryGetValue(dimension, out long value) ? value : 0;

        public void Set(AuthoredId dimension, long value)
        {
            if (!dimension.IsSet)
            {
                throw new ArgumentException("A social dimension needs a stable authored id.", nameof(dimension));
            }

            _values[dimension] = SocialNumeric.Coordinate(value);
        }

        public SocialVector Copy() => new SocialVector(_values);
    }

    public static class SocialDimensions
    {
        public static readonly AuthoredId Warmth = new AuthoredId("social.dimension.warmth");
        public static readonly AuthoredId Agency = new AuthoredId("social.dimension.agency");
        public static readonly AuthoredId Stability = new AuthoredId("social.dimension.stability");
        public static readonly AuthoredId Sociability = new AuthoredId("social.dimension.sociability");
        public static readonly AuthoredId Openness = new AuthoredId("social.dimension.openness");
        public static readonly AuthoredId Discipline = new AuthoredId("social.dimension.discipline");
        public static readonly AuthoredId Attunement = new AuthoredId("social.dimension.attunement");

        public static readonly IReadOnlyList<AuthoredId> Provisional = new[]
        {
            Warmth,
            Agency,
            Stability,
            Sociability,
            Openness,
            Discipline,
            Attunement,
        };
    }

    /// <summary>A canonical symmetric coordinate pair used by sparse Q and covariance terms.</summary>
    public readonly struct SocialDimensionPair : IEquatable<SocialDimensionPair>, IComparable<SocialDimensionPair>
    {
        public SocialDimensionPair(AuthoredId first, AuthoredId second)
        {
            if (!first.IsSet || !second.IsSet)
            {
                throw new ArgumentException("Both social dimensions must be set.");
            }

            if (first.CompareTo(second) <= 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public AuthoredId First { get; }
        public AuthoredId Second { get; }
        public bool IsDiagonal => First == Second;

        public bool Equals(SocialDimensionPair other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is SocialDimensionPair other && Equals(other);
        public override int GetHashCode() => (First.GetHashCode() * 397) ^ Second.GetHashCode();
        public int CompareTo(SocialDimensionPair other)
        {
            int first = First.CompareTo(other.First);
            return first != 0 ? first : Second.CompareTo(other.Second);
        }

        public override string ToString() => $"{First}×{Second}";
    }
}
