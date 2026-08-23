using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Promotes a meaningful Decision appearance from the transient event queue into History.</summary>
    public sealed class DecisionCreatedHistoryHandler : DomainEventHandler<DecisionCreatedEvent>
    {
        public static readonly AuthoredId HistoryKind = new AuthoredId("history.decision_created");

        public DecisionCreatedHistoryHandler() : base(DecisionDomainEventTypes.DecisionCreated) { }

        protected override void Handle(DecisionCreatedEvent domainEvent, WorldState world, SimulationContext context)
        {
            world.HistoryLedger.Record(
                HistoryKind,
                world.Clock.Now,
                RetentionTier.Recent,
                domainEvent.DefinitionId.ToString(),
                new[] { domainEvent.CharacterId.ToRef(), domainEvent.DecisionId.ToRef() });
        }
    }

    /// <summary>Retains successful player influence as causal history; rejected commands create none.</summary>
    public sealed class DecisionInterventionHistoryHandler : DomainEventHandler<DecisionInterventionAppliedEvent>
    {
        public static readonly AuthoredId HistoryKind = new AuthoredId("history.decision_intervention");

        public DecisionInterventionHistoryHandler() : base(DecisionDomainEventTypes.DecisionInterventionApplied) { }

        protected override void Handle(
            DecisionInterventionAppliedEvent domainEvent,
            WorldState world,
            SimulationContext context)
        {
            world.HistoryLedger.Record(
                HistoryKind,
                world.Clock.Now,
                RetentionTier.Recent,
                $"{domainEvent.InterventionDefinitionId} → {domainEvent.InfluenceId}",
                new[] { domainEvent.CharacterId.ToRef(), domainEvent.DecisionId.ToRef() });
        }
    }

    public sealed class DecisionDissolvedHistoryHandler : DomainEventHandler<DecisionDissolvedEvent>
    {
        public static readonly AuthoredId HistoryKind = new AuthoredId("history.decision_dissolved");
        public DecisionDissolvedHistoryHandler() : base(DecisionDomainEventTypes.DecisionDissolved) { }
        protected override void Handle(DecisionDissolvedEvent e, WorldState world, SimulationContext context)
        {
            world.HistoryLedger.Record(HistoryKind, e.DissolvedAt, RetentionTier.Ephemeral,
                $"{e.Reason}; refunded {e.InterventionsToRefund.Count} intervention(s)",
                new[] { e.CharacterId.ToRef(), e.DecisionId.ToRef() });
        }
    }
}
