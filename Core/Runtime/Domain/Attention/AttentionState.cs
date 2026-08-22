using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Attention
{
    /// <summary>
    /// Attention is gameplay state, not presentation state (§20).
    /// <para>
    /// Owns the canonical <see cref="WatchState"/> per character (§20.1), the attention policies that
    /// decide what surfaces or holds, and the set of currently held decisions whose capacity is bounded
    /// by <see cref="Decisions.DecisionHoldPolicy"/>.
    /// </para>
    /// </summary>
    public sealed class AttentionState
    {
        private readonly SortedDictionary<CharacterId, WatchState> _watch = new SortedDictionary<CharacterId, WatchState>();
        private readonly SortedDictionary<CharacterId, AttentionPolicy> _characterPolicies = new SortedDictionary<CharacterId, AttentionPolicy>();
        private readonly SortedDictionary<DecisionId, AttentionPolicy> _decisionPolicies = new SortedDictionary<DecisionId, AttentionPolicy>();
        private readonly SortedDictionary<CharacterId, int> _observationOrdinals = new SortedDictionary<CharacterId, int>();
        private readonly SortedSet<DecisionId> _heldDecisions = new SortedSet<DecisionId>();

        /// <summary>The canonical watch signal. Unknown characters read as all-false.</summary>
        public WatchState WatchStateOf(CharacterId character) =>
            _watch.TryGetValue(character, out WatchState state) ? state : default;

        public void SetWatchState(CharacterId character, WatchState state) => _watch[character] = state;

        /// <summary>Characters the player is watching by any route, ascending.</summary>
        public IEnumerable<CharacterId> WatchedCharacters
        {
            get
            {
                foreach (KeyValuePair<CharacterId, WatchState> pair in _watch)
                {
                    if (pair.Value.IsWatched)
                    {
                        yield return pair.Key;
                    }
                }
            }
        }

        public AttentionPolicy PolicyFor(CharacterId character) =>
            _characterPolicies.TryGetValue(character, out AttentionPolicy policy) ? policy : AttentionPolicy.Normal;

        public void SetPolicy(CharacterId character, AttentionPolicy policy) => _characterPolicies[character] = policy;

        public AttentionPolicy PolicyFor(DecisionId decision) =>
            _decisionPolicies.TryGetValue(decision, out AttentionPolicy policy) ? policy : AttentionPolicy.Normal;

        public void SetPolicy(DecisionId decision, AttentionPolicy policy) => _decisionPolicies[decision] = policy;

        /// <summary>Held decisions, ascending. Bounded by policy — never allowed to grow freely (§20).</summary>
        public IReadOnlyCollection<DecisionId> HeldDecisions => _heldDecisions;

        public int HeldCount => _heldDecisions.Count;

        public bool IsHeld(DecisionId decision) => _heldDecisions.Contains(decision);

        public bool Hold(DecisionId decision) => _heldDecisions.Add(decision);

        public bool Release(DecisionId decision) => _heldDecisions.Remove(decision);

        /// <summary>
        /// Monotonic per-character observation counter feeding the random oracle's roll index, so
        /// repeated observations of the same character roll differently but reproducibly (§14).
        /// Persisted with attention state.
        /// </summary>
        public int NextObservationOrdinal(CharacterId character)
        {
            int next = (_observationOrdinals.TryGetValue(character, out int current) ? current : 0) + 1;
            _observationOrdinals[character] = next;
            return next;
        }

        public int ObservationOrdinal(CharacterId character) =>
            _observationOrdinals.TryGetValue(character, out int value) ? value : 0;

        public void RestoreObservationOrdinal(CharacterId character, int value) => _observationOrdinals[character] = value;

        /// <summary>Strips ephemeral watch flags. Called after load, since camera state does not persist.</summary>
        public void ClearEphemeralWatchState()
        {
            var characters = new List<CharacterId>(_watch.Keys);
            for (int i = 0; i < characters.Count; i++)
            {
                _watch[characters[i]] = _watch[characters[i]].DurableOnly();
            }
        }
    }
}
