using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Prunes resolved Decisions and their frozen evidence with the linked history record.</summary>
    public sealed class DecisionHistoryRetentionService
    {
        public int Prune(WorldState world, SimTime olderThan, RetentionTier maxTierToPrune)
        {
            var removing = new List<DecisionId>();
            foreach (Decision decision in world.Decisions.All)
            {
                if (decision.Status == DecisionStatus.Resolved && decision.ResolutionHistoryEntryId.IsSet &&
                    world.HistoryLedger.TryGet(decision.ResolutionHistoryEntryId, out HistoryEntry history) &&
                    history.OccurredAt < olderThan && (int)history.Tier <= (int)maxTierToPrune)
                {
                    removing.Add(decision.Id);
                }
            }
            world.HistoryLedger.Prune(olderThan, maxTierToPrune);
            // CommitmentOutcome records have Ephemeral retention and share the same historical
            // cutoff. Durable memories/knowledge retain denormalized meaning plus weak provenance.
            if ((int)maxTierToPrune >= (int)RetentionTier.Ephemeral)
                world.CommitmentOutcomes.PruneBefore(olderThan);
            for (int i = 0; i < removing.Count; i++) world.Decisions.Remove(removing[i]);
            return removing.Count;
        }
    }
}
