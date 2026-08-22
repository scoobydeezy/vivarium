using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Randomness
{
    /// <summary>Version of the fixed hashing/mixing algorithm used by <see cref="DeterministicRandomOracle"/>.</summary>
    public static class RandomAlgorithmVersion
    {
        /// <summary>
        /// Bump this whenever the mixing, rejection, or component order below changes. Saves record it
        /// so a divergent replay can be diagnosed as an intentional algorithm change rather than an
        /// input divergence (§39.1, §53).
        /// </summary>
        public const int Current = 1;
    }

    /// <summary>
    /// The authoritative random oracle: <c>FixedHash(WorldSeed, ScopeType, ScopeId, Purpose, RollIndex)</c> (§14).
    /// <para>
    /// Every component is either an integer or an <see cref="AuthoredId"/> hashed through
    /// <see cref="StableHash"/>, so no result depends on <c>string.GetHashCode()</c> or any other
    /// runtime-dependent function.
    /// </para>
    /// </summary>
    public sealed class DeterministicRandomOracle : IRandomOracle
    {
        public DeterministicRandomOracle(long worldSeed)
        {
            WorldSeed = worldSeed;
        }

        public int AlgorithmVersion => RandomAlgorithmVersion.Current;

        public long WorldSeed { get; }

        public ulong Raw(RandomScope scope, AuthoredId purpose, int rollIndex)
        {
            ulong hash = StableHash.Avalanche(unchecked((ulong)WorldSeed) ^ StableHash.GoldenGamma);
            hash = StableHash.Combine(hash, scope.ScopeType);
            hash = StableHash.Combine(hash, scope.ScopeId);
            hash = StableHash.Combine(hash, purpose);
            hash = StableHash.Combine(hash, rollIndex);
            return hash;
        }

        public int Range(RandomScope scope, AuthoredId purpose, int rollIndex, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)BoundedUniform(Raw(scope, purpose, rollIndex), span);
        }

        public int RollDie(RandomScope scope, AuthoredId purpose, int rollIndex, int sides)
        {
            if (sides < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sides), "A die needs at least one face.");
            }

            return 1 + (int)BoundedUniform(Raw(scope, purpose, rollIndex), (ulong)sides);
        }

        public bool Chance(RandomScope scope, AuthoredId purpose, int rollIndex, int basisPoints)
        {
            if (basisPoints <= 0)
            {
                return false;
            }

            if (basisPoints >= 10000)
            {
                return true;
            }

            return BoundedUniform(Raw(scope, purpose, rollIndex), 10000UL) < (ulong)basisPoints;
        }

        /// <summary>
        /// Uniform value in <c>[0, span)</c> with modulo bias removed by rejection. Rejected draws are
        /// re-mixed with a fixed increment rather than reaching for more entropy, keeping the whole
        /// function pure and reproducible.
        /// </summary>
        private static ulong BoundedUniform(ulong hash, ulong span)
        {
            if (span <= 1)
            {
                return 0;
            }

            ulong limit = ulong.MaxValue - (ulong.MaxValue % span);
            int guard = 0;
            while (hash >= limit)
            {
                hash = StableHash.Avalanche(hash + StableHash.GoldenGamma);
                if (++guard > 64)
                {
                    // Unreachable in practice; keeps the function total rather than theoretically looping.
                    break;
                }
            }

            return hash % span;
        }
    }
}
