using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Maps world contexts back to the active Decisions they can affect (§17.2).
    /// <para>
    /// This is what makes living decisions affordable. Without it, every unrelated world change would
    /// have to scan every open Decision — the exact pattern §50 forbids. With it, "a new apartment
    /// opened in this district" reaches only the decisions that registered an interest (invariant 38).
    /// </para>
    /// <para>
    /// Rebuildable from the active decisions after load (§40).
    /// </para>
    /// </summary>
    public sealed class DecisionDependencyIndex
    {
        private readonly IndexedMembership<DecisionDependencyKey, DecisionId> _index =
            new IndexedMembership<DecisionDependencyKey, DecisionId>();

        /// <summary>Registers every dependency the decision currently declares.</summary>
        public void Register(Decision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            foreach (DecisionDependencyKey key in decision.DependencyKeys)
            {
                _index.Add(key, decision.Id);
            }
        }

        public void RegisterDependency(DecisionDependencyKey key, DecisionId decision)
        {
            if (key.IsSet)
            {
                _index.Add(key, decision);
            }
        }

        /// <summary>Drops a decision from the index — call on resolution, expiry, or supersession.</summary>
        public void Unregister(DecisionId decision) => _index.RemoveMember(decision);

        /// <summary>
        /// Active decisions that may need reevaluation because <paramref name="key"/> changed.
        /// Ascending by DecisionId, so reevaluation order is deterministic (§15).
        /// </summary>
        public IReadOnlyCollection<DecisionId> DecisionsDependingOn(DecisionDependencyKey key) => _index.MembersOf(key);

        public void Clear() => _index.Clear();
    }
}
