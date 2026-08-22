using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;

namespace Vivarium.Domain.Relationships
{
    /// <summary>
    /// Turns a shared context into a <b>bounded</b> set of interaction candidates (§32).
    /// <para>
    /// Global pairwise scanning is forbidden outright. But a shared context is not a licence for an
    /// O(k²) scan inside it either — a concert, station, or city square may hold thousands. This
    /// selector is where that bound is enforced.
    /// </para>
    /// <para>
    /// Sampling uses the deterministic oracle with a stable semantic scope, so a replay picks the same
    /// candidates (§14, invariant 55).
    /// </para>
    /// </summary>
    public sealed class InteractionCandidateSelector
    {
        private readonly IRandomOracle _random;

        public InteractionCandidateSelector(IRandomOracle random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Selects at most <paramref name="maxCandidates"/> counterparts for
        /// <paramref name="actor"/> from a shared-context pool.
        /// <para>
        /// Prefers existing acquaintances (relationship relevance), then fills any remaining slots with
        /// a bounded deterministic sample of strangers. The pool itself comes from an index — a room's
        /// occupants, a workplace's members, a journey segment — never from the population.
        /// </para>
        /// </summary>
        /// <param name="scopeId">Stable scope id for the sampling stream, e.g. the location id.</param>
        /// <param name="rollIndex">Monotonic per-opportunity counter, so repeated draws differ deterministically.</param>
        public IReadOnlyList<CharacterId> Select(
            CharacterId actor,
            IReadOnlyCollection<CharacterId> sharedContextPool,
            RelationshipIndex relationships,
            int maxCandidates,
            AuthoredId scopeType,
            int scopeId,
            int rollIndex)
        {
            if (maxCandidates <= 0 || sharedContextPool == null || sharedContextPool.Count == 0)
            {
                return new CharacterId[0];
            }

            var acquaintances = new List<CharacterId>();
            var strangers = new List<CharacterId>();

            foreach (CharacterId other in sharedContextPool)
            {
                if (other == actor)
                {
                    continue;
                }

                if (relationships != null && relationships.TryGetBetween(actor, other, out RelationshipId _))
                {
                    acquaintances.Add(other);
                }
                else
                {
                    strangers.Add(other);
                }
            }

            var selected = new List<CharacterId>(maxCandidates);

            // Acquaintances first, in deterministic id order.
            acquaintances.Sort();
            for (int i = 0; i < acquaintances.Count && selected.Count < maxCandidates; i++)
            {
                selected.Add(acquaintances[i]);
            }

            if (selected.Count >= maxCandidates || strangers.Count == 0)
            {
                return selected;
            }

            strangers.Sort();
            int remaining = maxCandidates - selected.Count;

            if (strangers.Count <= remaining)
            {
                selected.AddRange(strangers);
                return selected;
            }

            // Bounded deterministic sample: rejection-sample distinct indices, O(remaining) expected,
            // rather than shuffling a pool that could hold thousands.
            var scope = new RandomScope(scopeType, scopeId);
            var picked = new SortedSet<int>();
            int attempt = 0;
            int attemptLimit = remaining * 8 + 16;

            while (picked.Count < remaining && attempt < attemptLimit)
            {
                int index = _random.Range(
                    scope,
                    RandomPurposes.InteractionCandidateSample,
                    (rollIndex * attemptLimit) + attempt,
                    0,
                    strangers.Count);
                picked.Add(index);
                attempt++;
            }

            foreach (int index in picked)
            {
                selected.Add(strangers[index]);
            }

            return selected;
        }
    }
}
