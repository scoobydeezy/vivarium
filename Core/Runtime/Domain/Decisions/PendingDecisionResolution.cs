using System;
using System.Collections.Generic;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Frozen rolls awaiting player acceptance or bounded automatic commitment.</summary>
    public sealed class PendingDecisionResolution
    {
        private readonly List<InfluenceRoll> _accepted;
        private readonly List<InfluenceRoll> _superseded;

        public PendingDecisionResolution(SimTime producedAt, SimTime expiresAt, ScheduledEventId expiryEventId,
            IReadOnlyList<InfluenceRoll> accepted, IReadOnlyList<InfluenceRoll> superseded = null)
        {
            ProducedAt = producedAt; ExpiresAt = expiresAt; ExpiryEventId = expiryEventId;
            _accepted = new List<InfluenceRoll>(accepted ?? throw new ArgumentNullException(nameof(accepted)));
            _superseded = new List<InfluenceRoll>(superseded ?? new InfluenceRoll[0]);
        }

        public SimTime ProducedAt { get; }
        public SimTime ExpiresAt { get; }
        public ScheduledEventId ExpiryEventId { get; }
        public IReadOnlyList<InfluenceRoll> AcceptedRolls => _accepted;
        public IReadOnlyList<InfluenceRoll> SupersededRolls => _superseded;

        public bool TryGetAccepted(DecisionInfluenceId id, out InfluenceRoll roll)
        {
            for (int i = 0; i < _accepted.Count; i++) if (_accepted[i].InfluenceId == id) { roll = _accepted[i]; return true; }
            roll = default; return false;
        }

        public void Replace(InfluenceRoll replacement)
        {
            for (int i = 0; i < _accepted.Count; i++)
            {
                if (_accepted[i].InfluenceId != replacement.InfluenceId) continue;
                _superseded.Add(_accepted[i]);
                _accepted[i] = replacement;
                return;
            }
            throw new InvalidOperationException("A pending resolution can only re-roll a frozen participating Influence.");
        }
    }
}
