using Vivarium.Domain.Common;

namespace Vivarium.Domain.Randomness
{
    /// <summary>
    /// Counter-based deterministic random oracle (§14).
    /// <para>
    /// There is no stream state to advance and nothing to "consume" — a result is a pure function of
    /// <c>(world seed, scope, purpose, roll index)</c>. A reroll is the same query with the next roll
    /// index. Never <c>UnityEngine.Random</c>, never a scattered <c>System.Random</c>, never one
    /// global stream.
    /// </para>
    /// </summary>
    public interface IRandomOracle
    {
        /// <summary>Version of the hashing/mixing algorithm; recorded in saves and traces (§38, §53).</summary>
        int AlgorithmVersion { get; }

        long WorldSeed { get; }

        /// <summary>Raw 64 bits for a coordinate.</summary>
        ulong Raw(RandomScope scope, AuthoredId purpose, int rollIndex);

        /// <summary>Uniform integer in <c>[minInclusive, maxExclusive)</c>, rejection-corrected for bias.</summary>
        int Range(RandomScope scope, AuthoredId purpose, int rollIndex, int minInclusive, int maxExclusive);

        /// <summary>Rolls a die with <paramref name="sides"/> faces, returning 1..sides.</summary>
        int RollDie(RandomScope scope, AuthoredId purpose, int rollIndex, int sides);

        /// <summary>True with probability <paramref name="basisPoints"/>/10000 (§16).</summary>
        bool Chance(RandomScope scope, AuthoredId purpose, int rollIndex, int basisPoints);
    }
}
