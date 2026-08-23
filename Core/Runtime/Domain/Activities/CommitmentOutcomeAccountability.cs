using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// The sole boundary allowed to translate authoritative lifecycle cause into stakeholder belief.
    /// Social consequence code receives only the resulting attribution and never reads simulation truth.
    /// </summary>
    public static class CommitmentAttributionMapper
    {
        public static KnownCommitmentAttribution Observe(CommitmentOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            switch (outcome.Cause.Kind)
            {
                case CommitmentOutcomeCauseKind.ConflictResolution:
                case CommitmentOutcomeCauseKind.ExplicitCancellation:
                    return Known(outcome, PerceivedCommitmentCause.RelinquishedByActor, true);
                case CommitmentOutcomeCauseKind.ExternalCancellation:
                    return Known(outcome, PerceivedCommitmentCause.NotAttributedToActor, false);
                case CommitmentOutcomeCauseKind.WindowExpired:
                case CommitmentOutcomeCauseKind.None:
                    return Known(outcome, PerceivedCommitmentCause.Unknown, true);
                default:
                    throw new InvalidOperationException($"Unsupported commitment cause {outcome.Cause.Kind}.");
            }
        }

        private static KnownCommitmentAttribution Known(
            CommitmentOutcome outcome,
            PerceivedCommitmentCause perceivedCause,
            bool actorAccountable) =>
            new KnownCommitmentAttribution(
                outcome.Outcome, perceivedCause, outcome.OccurredAt, outcome.Id, actorAccountable);
    }

    /// <summary>Applies one policy-selected consequence set to each Character stakeholder.</summary>
    public sealed class CommitmentOutcomeConsequenceHandler : DomainEventHandler<CommitmentOutcomeOccurredEvent>
    {
        private static readonly AuthoredId HistoryKind = new AuthoredId("history.commitment_outcome");
        private readonly DefinitionCatalog _catalog;
        private readonly SocialBeliefUpdateService _beliefs;

        public CommitmentOutcomeConsequenceHandler(DefinitionCatalog catalog, SocialBeliefUpdateService beliefs)
            : base(ActivityDomainEventTypes.CommitmentOutcomeOccurred)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _beliefs = beliefs ?? throw new ArgumentNullException(nameof(beliefs));
        }

        protected override void Handle(
            CommitmentOutcomeOccurredEvent domainEvent,
            WorldState world,
            SimulationContext context)
        {
            CommitmentOutcome outcome = domainEvent.Outcome;
            if (!world.CommitmentOutcomes.TryMarkAccountabilityApplied(outcome.Id)) return;
            if (!world.Commitments.TryGet(outcome.CommitmentId, out Commitment commitment)) return;

            // This is the only call site below the outcome event that is permitted to inspect Cause.
            KnownCommitmentAttribution attribution = CommitmentAttributionMapper.Observe(outcome);
            for (int i = 0; i < commitment.Stakeholders.Count; i++)
            {
                StakeholderRef stakeholder = commitment.Stakeholders[i];
                if (stakeholder.Entity.Kind != EntityKind.Character) continue;
                var observerId = new CharacterId(stakeholder.Entity.RuntimeId);
                if (observerId == commitment.CharacterId ||
                    !world.Characters.TryGet(observerId, out Characters.Character observer) || !observer.IsActive)
                    continue;

                RecordAttribution(world, observerId, outcome, attribution);
                if (!attribution.ActorAccountable) continue;

                CommitmentConsequenceSet consequences =
                    commitment.AccountabilityPolicy.Resolve(attribution, stakeholder.Role);
                ApplyEvidence(world, observerId, commitment.CharacterId, outcome, consequences);
                ApplyRelationshipEffects(world, observerId, commitment.CharacterId, outcome, consequences);
            }
        }

        private static void RecordAttribution(
            WorldState world,
            CharacterId observerId,
            CommitmentOutcome outcome,
            KnownCommitmentAttribution attribution)
        {
            world.Knowledge.Record(new KnowledgeEntry(
                new FactKey(
                    FactKinds.CommitmentOutcomeAttribution,
                    outcome.Id.ToRef(),
                    OutcomeQualifier(attribution.ObservedOutcome)),
                new ObservedValue(AuthoredId.None, (long)attribution.PerceivedCause),
                attribution.ObservedAt,
                KnowledgeConfidence.Known,
                new DiscoverySource(
                    DiscoveryChannels.Accountability,
                    default,
                    default,
                    outcome.Id),
                ObserverRef.Character(observerId)));
        }

        private void ApplyEvidence(
            WorldState world,
            CharacterId observerId,
            CharacterId actorId,
            CommitmentOutcome outcome,
            CommitmentConsequenceSet consequences)
        {
            if (!consequences.EvidenceActionId.IsSet) return;
            if (!_catalog.SocialEvidence.TryGetValue(
                consequences.EvidenceActionId, out SocialEvidenceDefinition definition))
                throw new InvalidOperationException(
                    $"Commitment policy references missing social evidence {consequences.EvidenceActionId}.");
            _beliefs.Apply(
                world,
                new ObservedSocialEvidence(
                    actorId,
                    ObserverRef.Character(observerId),
                    definition.ActionDefinitionId,
                    outcome.OccurredAt,
                    new AuthoredId("social.context.commitment_outcome"),
                    outcome.Id),
                definition);
        }

        private static void ApplyRelationshipEffects(
            WorldState world,
            CharacterId observerId,
            CharacterId actorId,
            CommitmentOutcome outcome,
            CommitmentConsequenceSet consequences)
        {
            if (consequences.Memory == null && consequences.ChannelDeltas.Count == 0) return;
            if (!world.RelationshipIndex.TryGetBetween(observerId, actorId, out RelationshipId relationshipId) ||
                !world.Relationships.TryGet(relationshipId, out Relationship relationship) || !relationship.IsActive)
                return;

            DirectionalRelationshipState direction = relationship.From(observerId);
            foreach (System.Collections.Generic.KeyValuePair<AuthoredId, long> delta in consequences.ChannelDeltas)
                direction.ApplyChannelDelta(delta.Key, outcome.OccurredAt, delta.Value);

            if (consequences.Memory != null)
            {
                string actorName = world.Characters.TryGet(actorId, out Characters.Character actor)
                    ? actor.DisplayName
                    : actorId.ToString();
                string observerName = world.Characters.TryGet(observerId, out Characters.Character observer)
                    ? observer.DisplayName
                    : observerId.ToString();
                string summary = $"{observerName} remembers that {actorName}'s commitment was {outcome.Outcome}.";
                HistoryEntry history = world.HistoryLedger.Record(
                    HistoryKind,
                    outcome.OccurredAt,
                    consequences.Memory.RetentionTier,
                    summary,
                    new[] { observerId.ToRef(), actorId.ToRef(), outcome.CommitmentId.ToRef() },
                    outcome.Id);
                direction.AddMemory(new RelationshipMemory(
                    consequences.Memory.MemoryKind,
                    outcome.OccurredAt,
                    consequences.Memory.ExplanationId,
                    consequences.ChannelDeltas,
                    history.Id,
                    outcome.Id));
            }
            world.BumpRevision(relationship.RevisionKey);
        }

        private static AuthoredId OutcomeQualifier(CommitmentOutcomeKind outcome) =>
            new AuthoredId("commitment.outcome." + outcome.ToString().ToLowerInvariant());
    }
}
