using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    public sealed class SocialDecisionInfluenceSpec
    {
        public SocialDecisionInfluenceSpec(
            AuthoredId positiveOptionId,
            AuthoredId negativeOptionId,
            AuthoredId categoryId,
            AuthoredId positiveLabelId,
            AuthoredId negativeLabelId,
            InfluenceVisibility visibility)
        {
            PositiveOptionId = positiveOptionId;
            NegativeOptionId = negativeOptionId;
            CategoryId = categoryId;
            PositiveLabelId = positiveLabelId;
            NegativeLabelId = negativeLabelId;
            Visibility = visibility;
        }

        public AuthoredId PositiveOptionId { get; }
        public AuthoredId NegativeOptionId { get; }
        public AuthoredId CategoryId { get; }
        public AuthoredId PositiveLabelId { get; }
        public AuthoredId NegativeLabelId { get; }
        public InfluenceVisibility Visibility { get; }
    }

    public sealed class SocialInteractionDecisionTrigger
    {
        public SocialInteractionDecisionTrigger(
            AuthoredId pressureDefinitionId,
            AuthoredId lensId,
            SocialDecisionInfluenceSpec influenceSpec,
            AppraisalStrength minimumStrength = AppraisalStrength.Minor)
        {
            PressureDefinitionId = pressureDefinitionId;
            LensId = lensId;
            InfluenceSpec = influenceSpec ?? throw new ArgumentNullException(nameof(influenceSpec));
            MinimumStrength = minimumStrength;
        }

        public AuthoredId PressureDefinitionId { get; }
        public AuthoredId LensId { get; }
        public SocialDecisionInfluenceSpec InfluenceSpec { get; }
        public AppraisalStrength MinimumStrength { get; }
    }

    public sealed class DecisionRelationshipOutcome
    {
        public DecisionRelationshipOutcome(AuthoredId optionId, AuthoredId channelId, long delta)
        {
            OptionId = optionId;
            ChannelId = channelId;
            Delta = delta;
        }

        public AuthoredId OptionId { get; }
        public AuthoredId ChannelId { get; }
        public long Delta { get; }
    }

    /// <summary>A second concrete Decision path: a social interaction creates an explainable choice.</summary>
    public sealed class SocialInteractionDecisionGenerationHandler : DomainEventHandler<InteractionOccurredEvent>
    {
        public static readonly AuthoredId TargetParameter = new AuthoredId("decision.param.social_target_id");
        public const string RelationshipOutcomeParameterPrefix = "decision.param.social_relationship_outcome/";
        private readonly DefinitionCatalog _catalog;
        private readonly SocialPressureEvaluator _social = new SocialPressureEvaluator();
        private readonly InterpersonalComfortConsideration _consideration = new InterpersonalComfortConsideration();
        private readonly ReasonConsolidator _consolidator = new ReasonConsolidator();
        private readonly DecisionReasoningInfluenceFactory _influences = new DecisionReasoningInfluenceFactory();

        public SocialInteractionDecisionGenerationHandler(DefinitionCatalog catalog)
            : base(InteractionOccurredEvent.Type)
        {
            _catalog = catalog;
        }

        protected override void Handle(InteractionOccurredEvent domainEvent, WorldState world, SimulationContext context)
        {
            var matching = new List<DecisionDefinition>();
            foreach (KeyValuePair<AuthoredId, DecisionDefinition> pair in _catalog.Decisions)
            {
                if (pair.Value.SocialTrigger != null)
                {
                    matching.Add(pair.Value);
                }
            }
            matching.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < matching.Count; i++)
            {
                TryGenerate(matching[i], domainEvent.Actor, domainEvent.Counterpart, world);
            }
        }

        private void TryGenerate(
            DecisionDefinition definition,
            CharacterId characterId,
            CharacterId targetId,
            WorldState world)
        {
            SocialInteractionDecisionTrigger trigger = definition.SocialTrigger;
            if (!_catalog.SocialPressures.TryGetValue(trigger.PressureDefinitionId, out SocialPressureDefinition pressure) ||
                !world.Characters.Get(characterId).TryGetAppraisalField(trigger.LensId, out AppraisalField _))
            {
                return;
            }

            CompositeSocialEvaluationResult evaluation = _social.Evaluate(
                world,
                characterId,
                targetId,
                trigger.LensId,
                new SocialEvaluationContext(),
                pressure,
                _catalog);
            if (evaluation.Strength < trigger.MinimumStrength)
            {
                return;
            }

            var conflict = definition.ConflictScopeKind.IsSet
                ? new DecisionConflictScope(definition.ConflictScopeKind, targetId.ToRef())
                : DecisionConflictScope.None;
            foreach (Decision existing in world.Decisions.All)
            {
                if (existing.IsActive && existing.CharacterId == characterId &&
                    (existing.DefinitionId == definition.Id || (conflict.IsSet && existing.ConflictScope.Equals(conflict))))
                {
                    return;
                }
            }

            var decision = new Decision(
                world.RuntimeIds.Decisions.Next(),
                characterId,
                definition.Id,
                world.Clock.Now,
                world.Clock.Now.Plus(definition.TimeToResolve),
                definition.Options,
                conflict,
                definition.Importance);
            decision.SnapshotParameter(TargetParameter, targetId.Value);
            for (int i = 0; i < definition.RelationshipOutcomes.Count; i++)
            {
                DecisionRelationshipOutcome outcome = definition.RelationshipOutcomes[i];
                decision.SnapshotParameter(
                    new AuthoredId(RelationshipOutcomeParameterPrefix + outcome.OptionId.Value + "/" + outcome.ChannelId.Value),
                    outcome.Delta);
            }
            CandidateReason candidate = _consideration.Evaluate(decision, targetId, evaluation, trigger.InfluenceSpec);
            IReadOnlyList<CandidateReason> reasons = _consolidator.Consolidate(new[] { candidate });
            if (reasons.Count > 0)
            {
                _influences.Add(decision, reasons[0]);
            }

            world.Decisions.Add(decision.Id, decision);
            world.DecisionDependencies.Register(decision);
            ScheduledEvent scheduled = world.Scheduler.Schedule(
                decision.ResolveAt,
                SchedulePhase.Decision,
                ScheduledEventTypes.DecisionResolve,
                new DecisionResolvePayload(decision.Id, characterId));
            decision.SetPendingResolveEvent(scheduled.Id);
            world.Publish(new DecisionCreatedEvent(decision.Id, characterId, definition.Id));
        }
    }

    public sealed class DecisionRelationshipOutcomeHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly DefinitionCatalog _catalog;

        public DecisionRelationshipOutcomeHandler(DefinitionCatalog catalog)
            : base(DecisionDomainEventTypes.DecisionResolved)
        {
            _catalog = catalog;
        }

        protected override void Handle(DecisionResolvedEvent domainEvent, WorldState world, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(domainEvent.DecisionId);
            if (!decision.SnapshottedParameters.TryGetValue(
                    SocialInteractionDecisionGenerationHandler.TargetParameter,
                    out long targetValue))
            {
                return;
            }

            var targetId = new CharacterId((int)targetValue);
            if (!world.RelationshipIndex.TryGetBetween(domainEvent.CharacterId, targetId, out RelationshipId relationshipId))
            {
                return;
            }

            string chosenPrefix = SocialInteractionDecisionGenerationHandler.RelationshipOutcomeParameterPrefix +
                                  domainEvent.Resolution.ChosenOptionId.Value + "/";
            foreach (KeyValuePair<AuthoredId, long> parameter in decision.SnapshottedParameters)
            {
                if (!parameter.Key.Value.StartsWith(chosenPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                Relationship relationship = world.Relationships.Get(relationshipId);
                relationship.From(domainEvent.CharacterId).ApplyChannelDelta(
                    new AuthoredId(parameter.Key.Value.Substring(chosenPrefix.Length)),
                    world.Clock.Now,
                    parameter.Value);
                world.BumpRevision(relationship.RevisionKey);
            }
        }
    }

    /// <summary>Rebuilds a live social pressure when belief, preference, or dyadic state changes.</summary>
    public sealed class SocialDecisionInfluenceReevaluator : IDecisionInfluenceReevaluator
    {
        private readonly DecisionDefinition _definition;
        private readonly DefinitionCatalog _catalog;
        private readonly SocialPressureEvaluator _social = new SocialPressureEvaluator();
        private readonly InterpersonalComfortConsideration _consideration = new InterpersonalComfortConsideration();
        private readonly ReasonConsolidator _consolidator = new ReasonConsolidator();
        private readonly DecisionReasoningInfluenceFactory _factory = new DecisionReasoningInfluenceFactory();

        public SocialDecisionInfluenceReevaluator(DecisionDefinition definition, DefinitionCatalog catalog)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public AuthoredId DecisionDefinitionId => _definition.Id;

        public void Reevaluate(WorldState world, Decision decision, DecisionDependencyKey changedKey, SimulationContext context)
        {
            SocialInteractionDecisionTrigger trigger = _definition.SocialTrigger;
            if (trigger == null ||
                !_catalog.SocialPressures.TryGetValue(trigger.PressureDefinitionId, out SocialPressureDefinition pressure) ||
                !decision.SnapshottedParameters.TryGetValue(
                    SocialInteractionDecisionGenerationHandler.TargetParameter,
                    out long targetValue))
            {
                return;
            }

            var targetId = new CharacterId((int)targetValue);
            CompositeSocialEvaluationResult evaluation = _social.Evaluate(
                world,
                decision.CharacterId,
                targetId,
                trigger.LensId,
                new SocialEvaluationContext(),
                pressure,
                _catalog);
            CandidateReason candidate = _consideration.Evaluate(decision, targetId, evaluation, trigger.InfluenceSpec);
            IReadOnlyList<CandidateReason> reasons = _consolidator.Consolidate(new[] { candidate });
            DecisionInfluence existing = FindActiveSocialInfluence(
                decision,
                targetId,
                ReasonChannelIds.InterpersonalComfort);
            if (reasons.Count == 0)
            {
                if (existing != null) decision.RetractInfluence(existing.Id);
                return;
            }

            CandidateReason reason = reasons[0];
            Die desiredDie = reason.GameplayDie;
            if (existing != null && existing.OptionId == reason.OptionId && existing.Polarity == reason.Polarity)
            {
                if (existing.CurrentDie != desiredDie)
                {
                    decision.ChangeInfluenceDie(existing.Id, desiredDie);
                }
                return;
            }

            if (existing != null)
            {
                decision.RetractInfluence(existing.Id);
            }
            _factory.Add(decision, reason);
        }

        private static DecisionInfluence FindActiveSocialInfluence(
            Decision decision,
            CharacterId target,
            AuthoredId reasonChannel)
        {
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (!influence.IsRetracted &&
                    influence.Subject.Equals(target.ToRef()) &&
                    influence.ReasonChannelId == reasonChannel)
                {
                    return influence;
                }
            }
            return null;
        }
    }
}
