using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;

namespace Vivarium.Domain.Relationships
{
    public interface IInteractionRelevance
    {
        long Score(CharacterId actor, CharacterId candidate);
    }

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
            int rollIndex,
            IInteractionRelevance relevance = null)
        {
            if (maxCandidates <= 0 || sharedContextPool == null || sharedContextPool.Count == 0)
            {
                return new CharacterId[0];
            }

            // Spatial and travel membership indexes expose their already-sorted sets through the
            // IReadOnlyCollection contract. Preserve the exact selection semantics without copying
            // and sorting the whole shared context for every arrival.
            if (sharedContextPool is SortedSet<CharacterId> sortedPool)
            {
                return SelectFromSortedPool(
                    actor,
                    sortedPool,
                    relationships,
                    maxCandidates,
                    scopeType,
                    scopeId,
                    rollIndex,
                    relevance);
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
            acquaintances.Sort((left, right) =>
            {
                if (relevance != null)
                {
                    int score = relevance.Score(actor, right).CompareTo(relevance.Score(actor, left));
                    if (score != 0) return score;
                }
                return left.CompareTo(right);
            });
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
            IReadOnlyList<int> picked = PickStrangerOrdinals(scope, rollIndex, strangers.Count, remaining);

            for (int i = 0; i < picked.Count; i++)
            {
                selected.Add(strangers[picked[i]]);
            }

            return selected;
        }

        private IReadOnlyList<CharacterId> SelectFromSortedPool(
            CharacterId actor,
            SortedSet<CharacterId> sortedPool,
            RelationshipIndex relationships,
            int maxCandidates,
            AuthoredId scopeType,
            int scopeId,
            int rollIndex,
            IInteractionRelevance relevance)
        {
            var acquaintances = new List<CharacterId>();
            if (relationships != null)
            {
                foreach (CharacterId known in relationships.KnownCharactersOf(actor))
                {
                    if (known != actor && sortedPool.Contains(known))
                    {
                        acquaintances.Add(known);
                    }
                }
            }

            acquaintances.Sort((left, right) =>
            {
                if (relevance != null)
                {
                    int score = relevance.Score(actor, right).CompareTo(relevance.Score(actor, left));
                    if (score != 0) return score;
                }
                return left.CompareTo(right);
            });
            var acquaintanceSet = new HashSet<CharacterId>(acquaintances);

            var selected = new List<CharacterId>(maxCandidates);
            for (int i = 0; i < acquaintances.Count && selected.Count < maxCandidates; i++)
            {
                selected.Add(acquaintances[i]);
            }

            if (selected.Count >= maxCandidates)
            {
                return selected;
            }

            int strangerCount = sortedPool.Count - acquaintances.Count - (sortedPool.Contains(actor) ? 1 : 0);
            if (strangerCount <= 0)
            {
                return selected;
            }

            int remaining = maxCandidates - selected.Count;
            if (strangerCount <= remaining)
            {
                foreach (CharacterId candidate in sortedPool)
                {
                    if (candidate != actor && !acquaintanceSet.Contains(candidate))
                    {
                        selected.Add(candidate);
                    }
                }
                return selected;
            }

            var scope = new RandomScope(scopeType, scopeId);
            var picked = PickStrangerOrdinals(scope, rollIndex, strangerCount, remaining);
            if (TryAppendDensePoolSelections(sortedPool, actor, acquaintanceSet, picked, selected))
            {
                return selected;
            }

            int strangerOrdinal = 0;
            int pickedOrdinal = 0;
            foreach (CharacterId candidate in sortedPool)
            {
                if (candidate == actor || acquaintanceSet.Contains(candidate))
                {
                    continue;
                }

                if (pickedOrdinal < picked.Count && strangerOrdinal == picked[pickedOrdinal])
                {
                    selected.Add(candidate);
                    pickedOrdinal++;
                    if (pickedOrdinal == picked.Count)
                    {
                        break;
                    }
                }

                strangerOrdinal++;
            }

            return selected;
        }

        private static bool TryAppendDensePoolSelections(
            SortedSet<CharacterId> sortedPool,
            CharacterId actor,
            HashSet<CharacterId> acquaintanceSet,
            IReadOnlyList<int> picked,
            List<CharacterId> selected)
        {
            if (sortedPool.Count == 0 ||
                (long)sortedPool.Max.Value - sortedPool.Min.Value + 1 != sortedPool.Count)
            {
                return false;
            }

            var excluded = new List<CharacterId>(acquaintanceSet.Count + 1);
            foreach (CharacterId acquaintance in acquaintanceSet)
            {
                excluded.Add(acquaintance);
            }
            if (sortedPool.Contains(actor))
            {
                excluded.Add(actor);
            }
            excluded.Sort();

            for (int i = 0; i < picked.Count; i++)
            {
                long candidateValue = (long)sortedPool.Min.Value + picked[i];
                for (int excludedIndex = 0; excludedIndex < excluded.Count; excludedIndex++)
                {
                    if (excluded[excludedIndex].Value > candidateValue)
                    {
                        break;
                    }
                    candidateValue++;
                }
                selected.Add(new CharacterId((int)candidateValue));
            }

            return true;
        }

        private List<int> PickStrangerOrdinals(
            RandomScope scope,
            int rollIndex,
            int strangerCount,
            int remaining)
        {
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
                    strangerCount);
                picked.Add(index);
                attempt++;
            }

            return new List<int>(picked);
        }
    }
}
