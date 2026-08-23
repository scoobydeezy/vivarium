using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Rebuildable projection preventing duplicate active Decisions for one conflict episode.</summary>
    public sealed class CommitmentConflictIndex
    {
        private readonly SortedDictionary<CommitmentConflictKey, DecisionId> _byKey =
            new SortedDictionary<CommitmentConflictKey, DecisionId>();

        public void Register(Decision decision)
        {
            if (decision?.CommitmentConflictKey == null) return;
            if (_byKey.TryGetValue(decision.CommitmentConflictKey, out DecisionId existing) && existing != decision.Id)
                throw new InvalidOperationException(
                    $"Conflict {decision.CommitmentConflictKey.ConflictInstanceRevision} is already owned by {existing}.");
            _byKey[decision.CommitmentConflictKey] = decision.Id;
        }

        public void Unregister(DecisionId decisionId)
        {
            CommitmentConflictKey removing = null;
            foreach (KeyValuePair<CommitmentConflictKey, DecisionId> pair in _byKey)
            {
                if (pair.Value == decisionId) { removing = pair.Key; break; }
            }
            if (removing != null) _byKey.Remove(removing);
        }

        public bool TryFindByParticipants(
            CharacterId characterId,
            IReadOnlyList<CommitmentId> participants,
            out DecisionId decisionId)
        {
            foreach (KeyValuePair<CommitmentConflictKey, DecisionId> pair in _byKey)
            {
                if (pair.Key.HasSameParticipants(characterId, participants))
                {
                    decisionId = pair.Value;
                    return true;
                }
            }
            decisionId = default;
            return false;
        }

        public void Clear() => _byKey.Clear();
    }
}
