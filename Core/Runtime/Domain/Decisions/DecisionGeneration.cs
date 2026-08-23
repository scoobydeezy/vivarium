using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Content rule for generating one Decision when a Need reaches a meaningful threshold.</summary>
    public sealed class NeedThresholdDecisionTrigger
    {
        public NeedThresholdDecisionTrigger(AuthoredId needId, long threshold)
        {
            NeedId = needId;
            Threshold = threshold;
        }

        public AuthoredId NeedId { get; }

        public long Threshold { get; }
    }

    /// <summary>An authored initial influence; its character subject is bound when the Decision is created.</summary>
    public sealed class DecisionInfluenceTemplate
    {
        public DecisionInfluenceTemplate(
            AuthoredId optionId,
            AuthoredId categoryId,
            AuthoredId labelId,
            Die die,
            InfluenceVisibility visibility,
            bool subjectIsCharacter = false)
        {
            OptionId = optionId;
            CategoryId = categoryId;
            LabelId = labelId;
            Die = die;
            Visibility = visibility;
            SubjectIsCharacter = subjectIsCharacter;
        }

        public AuthoredId OptionId { get; }
        public AuthoredId CategoryId { get; }
        public AuthoredId LabelId { get; }
        public Die Die { get; }
        public InfluenceVisibility Visibility { get; }
        public bool SubjectIsCharacter { get; }
    }

    /// <summary>A small content-authored consequence using the common primary Activity transition path.</summary>
    public sealed class DecisionActivityOutcome
    {
        public DecisionActivityOutcome(AuthoredId optionId, AuthoredId activityDefinitionId, SimDuration duration)
        {
            OptionId = optionId;
            ActivityDefinitionId = activityDefinitionId;
            Duration = duration;
        }

        public AuthoredId OptionId { get; }
        public AuthoredId ActivityDefinitionId { get; }
        public SimDuration Duration { get; }
    }

    /// <summary>Turns Need threshold events into content-backed living Decisions.</summary>
    public sealed class NeedThresholdDecisionGenerationHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private static readonly AuthoredId ResolveDelayParameter = new AuthoredId("decision.param.time_to_resolve_minutes");
        private readonly DefinitionCatalog _catalog;
        private readonly DecisionSignalProviderRegistry _signalProviders;
        private readonly CompiledDecisionReasoningService _reasoning = new CompiledDecisionReasoningService();

        public NeedThresholdDecisionGenerationHandler(
            DefinitionCatalog catalog,
            DecisionSignalProviderRegistry signalProviders = null)
            : base(NeedThresholdReachedEvent.Type)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _signalProviders = signalProviders ?? DecisionSignalProviderRegistry.WithBuiltIns();
        }

        protected override void Handle(NeedThresholdReachedEvent domainEvent, WorldState world, SimulationContext context)
        {
            var matching = new List<DecisionDefinition>();
            foreach (KeyValuePair<AuthoredId, DecisionDefinition> pair in _catalog.Decisions)
            {
                DecisionDefinition definition = pair.Value;
                if (definition.Trigger != null &&
                    definition.Trigger.NeedId == domainEvent.NeedId &&
                    domainEvent.Value >= definition.Trigger.Threshold)
                {
                    matching.Add(definition);
                }
            }

            matching.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < matching.Count; i++)
            {
                TryGenerate(matching[i], domainEvent.CharacterId, world);
            }
        }

        private void TryGenerate(DecisionDefinition definition, CharacterId characterId, WorldState world)
        {
            var conflict = definition.ConflictScopeKind.IsSet
                ? new DecisionConflictScope(definition.ConflictScopeKind, characterId.ToRef())
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

            decision.SnapshotParameter(ResolveDelayParameter, definition.TimeToResolve.TotalMinutes);
            if (definition.ReasoningProgram != null) decision.SnapshotReasoningProgram(definition.ReasoningProgram);
            for (int i = 0; i < definition.InfluenceTemplates.Count; i++)
            {
                DecisionInfluenceTemplate influence = definition.InfluenceTemplates[i];
                decision.AddInfluence(
                    influence.OptionId,
                    influence.CategoryId,
                    influence.LabelId,
                    influence.Die,
                    influence.Visibility,
                    default,
                    influence.SubjectIsCharacter ? characterId.ToRef() : default);
            }

            world.Decisions.Add(decision.Id, decision);
            if (decision.ReasoningProgram != null)
            {
                _reasoning.EvaluateAndReconcile(world, decision, _signalProviders);
            }
            for (int i = 0; i < definition.DependencyTemplates.Count; i++)
            {
                DecisionDependencyKey template = definition.DependencyTemplates[i];
                decision.RegisterDependency(new DecisionDependencyKey(
                    template.ContextKind,
                    template.Subject.IsSet ? template.Subject : characterId.ToRef()));
            }
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

    /// <summary>Applies content-authored Activity outcomes after deterministic Decision resolution.</summary>
    public sealed class DecisionActivityOutcomeHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly DefinitionCatalog _catalog;
        private readonly ActivityTransitionService _transitions;

        public DecisionActivityOutcomeHandler(DefinitionCatalog catalog, ActivityTransitionService transitions)
            : base(DecisionDomainEventTypes.DecisionResolved)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        protected override void Handle(DecisionResolvedEvent domainEvent, WorldState world, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(domainEvent.DecisionId);
            if (!_catalog.Decisions.TryGetValue(decision.DefinitionId, out DecisionDefinition definition) ||
                !world.TryGetSpatialContext(domainEvent.CharacterId, out ActivitySpatialContext spatial) ||
                !spatial.IsLocated)
            {
                return;
            }

            for (int i = 0; i < definition.ActivityOutcomes.Count; i++)
            {
                DecisionActivityOutcome outcome = definition.ActivityOutcomes[i];
                if (outcome.OptionId == domainEvent.Resolution.ChosenOptionId)
                {
                    _transitions.BeginActivity(
                        context,
                        domainEvent.CharacterId,
                        outcome.ActivityDefinitionId,
                        spatial.LocationId,
                        outcome.Duration);
                    return;
                }
            }
        }
    }
}
