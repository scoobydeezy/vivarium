using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Scores available discretionary Activities without allocating a Decision. Ordinary instances
    /// begin directly; only a candidate set whose derived magnitude clears admission is promoted.
    /// </summary>
    public sealed class RecreationRoutineService
    {
        private readonly DefinitionCatalog _catalog;
        private readonly ActivityTransitionService _transitions;
        private readonly DecisionSignalProviderRegistry _signals;
        private readonly CompiledDecisionReasoningPreflightService _preflight =
            new CompiledDecisionReasoningPreflightService();
        private readonly CompiledDecisionGenerationService _generation;
        private readonly SortedDictionary<AuthoredId, AuthoredId> _needByDecision =
            new SortedDictionary<AuthoredId, AuthoredId>();

        public RecreationRoutineService(
            DefinitionCatalog catalog,
            ActivityTransitionService transitions,
            DecisionSignalProviderRegistry signals)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            _generation = new CompiledDecisionGenerationService(signals);
            foreach (KeyValuePair<AuthoredId, NeedDefinition> pair in catalog.Needs)
            {
                RecreationRoutineDefinition routine = pair.Value.RecreationRoutine;
                if (routine == null) continue;
                if (_needByDecision.ContainsKey(routine.DecisionDefinitionId))
                    throw new InvalidOperationException(
                        $"Recreation Decision '{routine.DecisionDefinitionId}' is used by more than one Need.");
                _needByDecision.Add(routine.DecisionDefinitionId, pair.Key);
            }
        }

        public void ReactToThreshold(SimulationContext context, CharacterId characterId, AuthoredId needId)
        {
            if (!TryGet(context.World, characterId, needId, out Character character, out NeedDefinition definition))
                return;
            TryStart(context, character, definition);
        }

        public void TryStartDeferred(SimulationContext context, CharacterId characterId)
        {
            if (!context.World.Characters.TryGet(characterId, out Character character)) return;
            var needIds = new List<AuthoredId>(character.Needs.Keys);
            needIds.Sort();
            for (int i = 0; i < needIds.Count; i++)
            {
                if (TryGet(context.World, characterId, needIds[i], out Character _, out NeedDefinition definition) &&
                    TryStart(context, character, definition))
                    return;
            }
        }

        public void ReactToDecisionResolved(SimulationContext context, DecisionId decisionId)
        {
            WorldState world = context.World;
            if (!world.Decisions.TryGet(decisionId, out Decision decision) || decision.Resolution == null ||
                !_needByDecision.ContainsKey(decision.DefinitionId))
                return;
            DecisionOption selected = null;
            for (int i = 0; i < decision.Options.Count; i++)
                if (decision.Options[i].Id == decision.Resolution.ChosenOptionId) selected = decision.Options[i];
            if (selected != null) TryStartOption(context, decision.CharacterId, selected);
        }

        private bool TryStart(SimulationContext context, Character character, NeedDefinition definition)
        {
            WorldState world = context.World;
            RecreationRoutineDefinition routine = definition.RecreationRoutine;
            if (routine == null ||
                !character.TryGetNeed(definition.Id, out NeedState need) ||
                need.ValueAt(world.Clock.Now) < routine.ActivationThreshold ||
                !world.TryGetCurrentActivity(character.Id, out ActivityInstance current) ||
                current.DefinitionId != WellKnownActivities.Waiting ||
                !current.SpatialContext.IsLocated)
                return false;

            DecisionDefinition decisionDefinition = _catalog.Decisions[routine.DecisionDefinitionId];
            var options = new List<DecisionOption>();
            for (int c = 0; c < routine.Candidates.Count; c++)
            {
                RecreationCandidateDefinition candidate = routine.Candidates[c];
                if (!ActivityAffordanceSelector.TryFindNearest(
                    world,
                    current.SpatialContext.LocationId,
                    candidate.ActivityDefinitionId,
                    out LocationId destination))
                    continue;
                DecisionOption template = FindOption(decisionDefinition, candidate.OptionId);
                DecisionOption option = template.Copy();
                ActivityDefinition activity = _catalog.Activities[candidate.ActivityDefinitionId];
                option.SetContext(
                    DecisionReasoningParameters.InterestId,
                    DecisionParameterValue.FromAuthoredId(candidate.InterestId));
                option.SetContext(
                    DecisionReasoningParameters.Destination,
                    DecisionParameterValue.FromEntity(destination.ToRef()));
                option.SetContext(
                    DecisionReasoningParameters.ActivityDefinitionId,
                    DecisionParameterValue.FromAuthoredId(activity.Id));
                option.SetContext(
                    DecisionReasoningParameters.ActivityDurationMinutes,
                    DecisionParameterValue.FromInteger(activity.DefaultDuration.TotalMinutes));
                option.SetContext(
                    DecisionReasoningParameters.NeedId,
                    DecisionParameterValue.FromAuthoredId(definition.Id));
                option.SetContext(
                    DecisionReasoningParameters.NeedSatisfactionOffset,
                    DecisionParameterValue.FromInteger(routine.SatisfactionOffset));
                options.Add(option);
            }
            if (options.Count == 0) return false;

            var reasoningContext = new DecisionReasoningContext(
                character.Id,
                options,
                decisionDefinition.ReasoningProgram);
            DecisionReasoningPreflightResult result = _preflight.Evaluate(world, reasoningContext, _signals);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record(
                    "routine",
                    $"{world.Clock.Now} {character.Id} evaluated {routine.DecisionDefinitionId}: " +
                    $"selected={result.SelectedOptionId} importance={result.Importance}");
            }

            if (options.Count >= 2 && result.Importance >= _catalog.DecisionImportancePolicy.AdmissionFloor)
            {
                var conflict = decisionDefinition.ConflictScopeKind.IsSet
                    ? new DecisionConflictScope(decisionDefinition.ConflictScopeKind, character.Id.ToRef())
                    : DecisionConflictScope.None;
                Decision generated = _generation.GenerateFromPreflight(
                    world,
                    new CompiledDecisionGenerationRequest(
                        character.Id,
                        decisionDefinition.Id,
                        decisionDefinition.TimeToResolve,
                        reasoningContext.Options,
                        reasoningContext.ReasoningProgram,
                        conflictScope: conflict),
                    result);
                return generated != null || HasActiveDecision(world, character.Id, decisionDefinition.Id);
            }

            DecisionOption selected = null;
            for (int i = 0; i < reasoningContext.Options.Count; i++)
                if (reasoningContext.Options[i].Id == result.SelectedOptionId) selected = reasoningContext.Options[i];
            return selected != null && TryStartOption(context, character.Id, selected);
        }

        private bool TryStartOption(SimulationContext context, CharacterId characterId, DecisionOption option)
        {
            WorldState world = context.World;
            if (!world.TryGetCurrentActivity(characterId, out ActivityInstance current) ||
                current.DefinitionId != WellKnownActivities.Waiting || !current.SpatialContext.IsLocated ||
                !TryPlan(option, out LocationId destination, out AuthoredId activityId,
                    out SimDuration duration, out AuthoredId needId, out long satisfactionOffset) ||
                !world.Locations.TryGet(destination, out LocationNode destinationNode) ||
                !destinationNode.Affords(activityId))
                return false;

            var parameters = new SortedDictionary<AuthoredId, long>
            {
                [ActivityNeedParameters.SatisfactionOffset(needId)] = satisfactionOffset,
            };
            if (current.SpatialContext.LocationId == destination)
            {
                _transitions.BeginActivity(
                    context,
                    characterId,
                    activityId,
                    destination,
                    duration,
                    committedParameters: parameters);
                return true;
            }
            return _transitions.TryBeginTravel(
                context,
                characterId,
                destination,
                out ActivityInstance _,
                continuationActivityDefinitionId: activityId,
                continuationDuration: duration,
                continuationCommittedParameters: parameters);
        }

        private static bool TryPlan(
            DecisionOption option,
            out LocationId destination,
            out AuthoredId activityId,
            out SimDuration duration,
            out AuthoredId needId,
            out long satisfactionOffset)
        {
            destination = default;
            activityId = default;
            duration = default;
            needId = default;
            satisfactionOffset = 0;
            if (!option.TryGetContext(DecisionReasoningParameters.Destination, out DecisionParameterValue destinationValue) ||
                destinationValue.Kind != DecisionParameterKind.Entity ||
                destinationValue.Entity.Kind != EntityKind.Location ||
                !option.TryGetContext(DecisionReasoningParameters.ActivityDefinitionId, out DecisionParameterValue activityValue) ||
                activityValue.Kind != DecisionParameterKind.AuthoredId ||
                !option.TryGetContext(DecisionReasoningParameters.ActivityDurationMinutes, out DecisionParameterValue durationValue) ||
                durationValue.Kind != DecisionParameterKind.Integer || durationValue.Integer < 0 ||
                !option.TryGetContext(DecisionReasoningParameters.NeedId, out DecisionParameterValue needValue) ||
                needValue.Kind != DecisionParameterKind.AuthoredId ||
                !option.TryGetContext(DecisionReasoningParameters.NeedSatisfactionOffset, out DecisionParameterValue offsetValue) ||
                offsetValue.Kind != DecisionParameterKind.Integer)
                return false;
            destination = new LocationId(destinationValue.Entity.RuntimeId);
            activityId = activityValue.AuthoredId;
            duration = SimDuration.FromMinutes(durationValue.Integer);
            needId = needValue.AuthoredId;
            satisfactionOffset = offsetValue.Integer;
            return destination.IsSet && activityId.IsSet && needId.IsSet;
        }

        private static DecisionOption FindOption(DecisionDefinition definition, AuthoredId optionId)
        {
            for (int i = 0; i < definition.Options.Count; i++)
                if (definition.Options[i].Id == optionId) return definition.Options[i];
            throw new InvalidOperationException($"Decision '{definition.Id}' has no Option '{optionId}'.");
        }

        private bool TryGet(
            WorldState world,
            CharacterId characterId,
            AuthoredId needId,
            out Character character,
            out NeedDefinition definition)
        {
            definition = null;
            return world.Characters.TryGet(characterId, out character) &&
                _catalog.Needs.TryGetValue(needId, out definition) &&
                definition.RecreationRoutine != null;
        }

        private static bool HasActiveDecision(WorldState world, CharacterId actor, AuthoredId definitionId)
        {
            foreach (Decision decision in world.Decisions.All)
                if (decision.IsActive && decision.CharacterId == actor && decision.DefinitionId == definitionId) return true;
            return false;
        }
    }

    public sealed class RecreationThresholdHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private readonly RecreationRoutineService _service;
        public RecreationThresholdHandler(RecreationRoutineService service)
            : base(NeedThresholdReachedEvent.Type) => _service = service;
        protected override void Handle(NeedThresholdReachedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToThreshold(context, e.CharacterId, e.NeedId);
    }

    public sealed class RecreationActivityStartedHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly RecreationRoutineService _service;
        public RecreationActivityStartedHandler(RecreationRoutineService service)
            : base(ActivityDomainEventTypes.ActivityStarted) => _service = service;
        protected override void Handle(ActivityStartedEvent e, WorldState world, SimulationContext context) =>
            _service.TryStartDeferred(context, e.CharacterId);
    }

    public sealed class RecreationDecisionResolvedHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly RecreationRoutineService _service;
        public RecreationDecisionResolvedHandler(RecreationRoutineService service)
            : base(DecisionDomainEventTypes.DecisionResolved) => _service = service;
        protected override void Handle(DecisionResolvedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToDecisionResolved(context, e.DecisionId);
    }
}
