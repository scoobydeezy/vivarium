using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Turns a reserve-Need crossing during an authored ongoing Activity into a cheap Rest/Continue
    /// preflight. Ordinary instances resolve directly; important instances adopt the exact reasoning
    /// into a persistent Decision. Continue rearms a strictly lower analytical threshold.
    /// </summary>
    public sealed class NeedContinuationRoutineService
    {
        private readonly DefinitionCatalog _catalog;
        private readonly NeedProgressionService _needs;
        private readonly NeedRestRoutineService _rest;
        private readonly ActivityTransitionService _transitions;
        private readonly DecisionSignalProviderRegistry _signals;
        private readonly CompiledDecisionReasoningPreflightService _preflight =
            new CompiledDecisionReasoningPreflightService();
        private readonly CompiledDecisionGenerationService _generation;
        private readonly SortedDictionary<AuthoredId, NeedDefinition> _needByDecision =
            new SortedDictionary<AuthoredId, NeedDefinition>();

        public NeedContinuationRoutineService(
            DefinitionCatalog catalog,
            NeedProgressionService needs,
            NeedRestRoutineService rest,
            ActivityTransitionService transitions,
            DecisionSignalProviderRegistry signals)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _rest = rest ?? throw new ArgumentNullException(nameof(rest));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            _generation = new CompiledDecisionGenerationService(signals);
            foreach (KeyValuePair<AuthoredId, NeedDefinition> pair in catalog.Needs)
            {
                NeedContinuationRoutineDefinition routine = pair.Value.ContinuationRoutine;
                if (routine == null) continue;
                if (_needByDecision.ContainsKey(routine.DecisionDefinitionId))
                    throw new InvalidOperationException(
                        $"Continuation Decision '{routine.DecisionDefinitionId}' is used by more than one Need.");
                _needByDecision.Add(routine.DecisionDefinitionId, pair.Value);
            }
        }

        public void ReactToThreshold(
            SimulationContext context,
            CharacterId characterId,
            AuthoredId needId,
            long crossedThreshold)
        {
            WorldState world = context.World;
            if (!world.Characters.TryGet(characterId, out Character character) ||
                !_catalog.Needs.TryGetValue(needId, out NeedDefinition definition) ||
                definition.ContinuationRoutine == null ||
                !character.TryGetNeed(needId, out NeedState need) ||
                need.Progression.IsIncreasing ||
                crossedThreshold > definition.ContinuationRoutine.ActivationThreshold ||
                !world.TryGetCurrentActivity(characterId, out ActivityInstance current) ||
                !definition.ContinuationRoutine.TryGetCandidate(
                    current.DefinitionId,
                    out NeedContinuationCandidateDefinition candidate))
                return;

            NeedContinuationRoutineDefinition routine = definition.ContinuationRoutine;
            if (!TryNextThreshold(definition, routine, crossedThreshold, out long nextThreshold))
            {
                StartRest(context, character, current);
                return;
            }

            DecisionDefinition decisionDefinition = _catalog.Decisions[routine.DecisionDefinitionId];
            var options = new List<DecisionOption>(decisionDefinition.Options.Count);
            for (int i = 0; i < decisionDefinition.Options.Count; i++)
            {
                DecisionOption option = decisionDefinition.Options[i].Copy();
                if (option.Id == routine.ContinueOptionId)
                {
                    option.SetContext(
                        DecisionReasoningParameters.InterestId,
                        DecisionParameterValue.FromAuthoredId(candidate.InterestId));
                }
                options.Add(option);
            }
            var decisionContext = new SortedDictionary<AuthoredId, DecisionParameterValue>
            {
                [DecisionReasoningParameters.NeedId] = DecisionParameterValue.FromAuthoredId(needId),
                [DecisionReasoningParameters.PlannedActivity] =
                    DecisionParameterValue.FromEntity(current.Id.ToRef()),
                [DecisionReasoningParameters.NextNeedThreshold] =
                    DecisionParameterValue.FromInteger(nextThreshold),
            };
            var reasoningContext = new DecisionReasoningContext(
                characterId,
                options,
                decisionDefinition.ReasoningProgram,
                decisionContext);
            DecisionReasoningPreflightResult result = _preflight.Evaluate(world, reasoningContext, _signals);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record(
                    "routine",
                    $"{world.Clock.Now} {characterId} evaluated {routine.DecisionDefinitionId}: " +
                    $"selected={result.SelectedOptionId} importance={result.Importance} next={nextThreshold}");
            }

            if (result.Importance >= _catalog.DecisionImportancePolicy.AdmissionFloor)
            {
                var conflict = decisionDefinition.ConflictScopeKind.IsSet
                    ? new DecisionConflictScope(decisionDefinition.ConflictScopeKind, characterId.ToRef())
                    : DecisionConflictScope.None;
                Decision generated = _generation.GenerateFromPreflight(
                    world,
                    new CompiledDecisionGenerationRequest(
                        characterId,
                        decisionDefinition.Id,
                        decisionDefinition.TimeToResolve,
                        reasoningContext.Options,
                        reasoningContext.ReasoningProgram,
                        reasoningContext.Context,
                        conflict),
                    result);
                if (generated != null || HasActiveDecision(world, characterId, decisionDefinition.Id)) return;
            }

            // Continuing past authored reserve pressure is itself the meaningful branch. If the
            // circumstance is not important enough to admit (or cannot claim its conflict scope),
            // ordinary fatigue always takes the safe Rest fallback.
            ApplyOutcome(context, character, current.Id, definition, routine, routine.RestOptionId, nextThreshold);
        }

        public void ReactToDecisionResolved(SimulationContext context, DecisionId decisionId)
        {
            WorldState world = context.World;
            if (!world.Decisions.TryGet(decisionId, out Decision decision) || decision.Resolution == null ||
                !_needByDecision.TryGetValue(decision.DefinitionId, out NeedDefinition definition) ||
                !decision.TryGetContextParameter(
                    DecisionReasoningParameters.PlannedActivity,
                    out DecisionParameterValue planned) ||
                planned.Kind != DecisionParameterKind.Entity ||
                planned.Entity.Kind != EntityKind.ActivityInstance ||
                !decision.TryGetContextParameter(
                    DecisionReasoningParameters.NextNeedThreshold,
                    out DecisionParameterValue threshold) ||
                threshold.Kind != DecisionParameterKind.Integer ||
                !world.Characters.TryGet(decision.CharacterId, out Character character))
                return;

            ApplyOutcome(
                context,
                character,
                new ActivityInstanceId(planned.Entity.RuntimeId),
                definition,
                definition.ContinuationRoutine,
                decision.Resolution.ChosenOptionId,
                threshold.Integer);
        }

        private void ApplyOutcome(
            SimulationContext context,
            Character character,
            ActivityInstanceId plannedActivityId,
            NeedDefinition definition,
            NeedContinuationRoutineDefinition routine,
            AuthoredId selectedOptionId,
            long nextThreshold)
        {
            WorldState world = context.World;
            if (!world.TryGetCurrentActivity(character.Id, out ActivityInstance current) ||
                current.Id != plannedActivityId)
            {
                _rest.TryStartDeferredRecovery(context, character.Id);
                return;
            }

            if (selectedOptionId == routine.ContinueOptionId &&
                character.TryGetNeed(definition.Id, out NeedState need))
            {
                long value = need.ValueAt(world.Clock.Now);
                while (nextThreshold >= value &&
                       TryNextThreshold(definition, routine, nextThreshold, out long lower))
                    nextThreshold = lower;
                if (nextThreshold < value && nextThreshold >= definition.MinValue)
                {
                    _needs.SetThreshold(context, character, definition.Id, nextThreshold);
                    return;
                }
            }

            StartRest(context, character, current);
        }

        private void StartRest(SimulationContext context, Character character, ActivityInstance current)
        {
            if (!current.SpatialContext.IsLocated) return;
            _transitions.BeginActivity(
                context,
                character.Id,
                WellKnownActivities.Waiting,
                current.SpatialContext.LocationId,
                _catalog.Activities[WellKnownActivities.Waiting].DefaultDuration);
            _rest.TryStartDeferredRecovery(context, character.Id);
        }

        private static bool TryNextThreshold(
            NeedDefinition definition,
            NeedContinuationRoutineDefinition routine,
            long currentThreshold,
            out long nextThreshold)
        {
            nextThreshold = currentThreshold - routine.ContinuationThresholdStep;
            if (nextThreshold < definition.MinValue) return false;
            for (int i = 0; i < definition.BehaviouralThresholds.Count; i++)
                if (definition.BehaviouralThresholds[i] == nextThreshold) return true;
            return false;
        }

        private static bool HasActiveDecision(WorldState world, CharacterId actor, AuthoredId definitionId)
        {
            foreach (Decision decision in world.Decisions.All)
                if (decision.IsActive && decision.CharacterId == actor && decision.DefinitionId == definitionId) return true;
            return false;
        }
    }

    public sealed class NeedContinuationThresholdHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private readonly NeedContinuationRoutineService _service;
        public NeedContinuationThresholdHandler(NeedContinuationRoutineService service)
            : base(NeedThresholdReachedEvent.Type) => _service = service;
        protected override void Handle(NeedThresholdReachedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToThreshold(context, e.CharacterId, e.NeedId, e.Threshold);
    }

    public sealed class NeedContinuationDecisionResolvedHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly NeedContinuationRoutineService _service;
        public NeedContinuationDecisionResolvedHandler(NeedContinuationRoutineService service)
            : base(DecisionDomainEventTypes.DecisionResolved) => _service = service;
        protected override void Handle(DecisionResolvedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToDecisionResolved(context, e.DecisionId);
    }

    /// <summary>Targets only compiled reasons subscribed to this character/Need revision.</summary>
    public sealed class NeedChangedDecisionHandler : DomainEventHandler<NeedChangedEvent>
    {
        private readonly DecisionReevaluationService _reevaluation;
        public NeedChangedDecisionHandler(DecisionReevaluationService reevaluation)
            : base(NeedChangedEvent.Type) => _reevaluation = reevaluation;
        protected override void Handle(NeedChangedEvent e, WorldState world, SimulationContext context) =>
            _reevaluation.ReevaluateDependents(
                context,
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(RevisionAspects.Need, e.NeedId),
                    e.CharacterId.ToRef()));
    }
}
