using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Turns active Social pressure into a primary Socializing Activity with one bounded,
    /// shared-location counterpart. The counterpart's primary Activity remains untouched.
    /// </summary>
    public sealed class SocializingRoutineService
    {
        public static readonly AuthoredId TargetCharacterParameter =
            new AuthoredId("activity.param.socializing.target_character_id");

        private readonly DefinitionCatalog _catalog;
        private readonly ActivityTransitionService _transitions;
        private readonly InteractionService _interactions;
        private readonly CompiledDecisionGenerationService _decisions;
        private readonly SortedDictionary<AuthoredId, NeedDefinition> _invitationNeeds =
            new SortedDictionary<AuthoredId, NeedDefinition>();

        public SocializingRoutineService(
            DefinitionCatalog catalog,
            ActivityTransitionService transitions,
            InteractionService interactions,
            DecisionSignalProviderRegistry decisionSignals)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            _decisions = new CompiledDecisionGenerationService(
                decisionSignals ?? throw new ArgumentNullException(nameof(decisionSignals)));
            foreach (KeyValuePair<AuthoredId, NeedDefinition> pair in catalog.Needs)
            {
                SocialInvitationRoutineDefinition invitation = pair.Value.SocializingRoutine?.Invitation;
                if (invitation == null) continue;
                if (_invitationNeeds.ContainsKey(invitation.DecisionDefinitionId))
                    throw new InvalidOperationException(
                        $"Social invitation Decision '{invitation.DecisionDefinitionId}' is used by more than one Need.");
                _invitationNeeds.Add(invitation.DecisionDefinitionId, pair.Value);
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

        public void ReactToArrival(SimulationContext context, CharacterId arrived, LocationId locationId)
        {
            TryStartDeferred(context, arrived);
            IReadOnlyList<CharacterId> nearby = _interactions.SelectCandidatesAtLocation(
                context.World,
                arrived,
                locationId,
                SocializingRoutineDefinition.MaximumCandidateLimit);
            for (int i = 0; i < nearby.Count; i++) TryStartDeferred(context, nearby[i]);
        }

        public void ReactToDecisionResolved(SimulationContext context, DecisionId decisionId)
        {
            WorldState world = context.World;
            if (!world.Decisions.TryGet(decisionId, out Decision decision) || decision.Resolution == null ||
                !_invitationNeeds.TryGetValue(decision.DefinitionId, out NeedDefinition need))
                return;
            SocialInvitationRoutineDefinition invitation = need.SocializingRoutine.Invitation;
            if (decision.Resolution.ChosenOptionId != invitation.AcceptOptionId ||
                !decision.TryGetContextParameter(
                    DecisionReasoningParameters.PlannedActivity,
                    out DecisionParameterValue planned) ||
                planned.Kind != DecisionParameterKind.Entity ||
                planned.Entity.Kind != EntityKind.ActivityInstance)
                return;

            DecisionOption accept = FindOption(decision.Options, invitation.AcceptOptionId);
            if (accept == null ||
                !accept.TryGetContext(DecisionReasoningParameters.Target, out DecisionParameterValue requester) ||
                requester.Kind != DecisionParameterKind.Entity || requester.Entity.Kind != EntityKind.Character)
                return;
            var requesterId = new CharacterId(requester.Entity.RuntimeId);
            if (!world.Characters.TryGet(decision.CharacterId, out Character recipient) ||
                recipient.CurrentActivityId.Value != planned.Entity.RuntimeId ||
                !world.TryGetCurrentActivity(requesterId, out ActivityInstance requesterActivity) ||
                requesterActivity.DefinitionId != need.SocializingRoutine.ActivityDefinitionId ||
                !world.TryGetSpatialContext(decision.CharacterId, out ActivitySpatialContext recipientContext) ||
                !world.TryGetSpatialContext(requesterId, out ActivitySpatialContext requesterContext) ||
                !recipientContext.IsLocated || !requesterContext.IsLocated ||
                recipientContext.LocationId != requesterContext.LocationId)
                return;

            var parameters = new SortedDictionary<AuthoredId, long>
            {
                [TargetCharacterParameter] = requesterId.Value,
                [ActivityNeedParameters.SatisfactionOffset(need.Id)] = need.SocializingRoutine.SatisfactionOffset,
            };
            ActivityDefinition activity = _catalog.Activities[need.SocializingRoutine.ActivityDefinitionId];
            _transitions.BeginActivity(
                context,
                decision.CharacterId,
                activity.Id,
                recipientContext.LocationId,
                activity.DefaultDuration,
                committedParameters: parameters);
        }

        private bool TryStart(SimulationContext context, Character character, NeedDefinition definition)
        {
            WorldState world = context.World;
            SocializingRoutineDefinition routine = definition.SocializingRoutine;
            if (routine == null ||
                !character.TryGetNeed(definition.Id, out NeedState need) ||
                need.ValueAt(world.Clock.Now) < routine.ActivationThreshold ||
                !world.TryGetCurrentActivity(character.Id, out ActivityInstance current) ||
                current.DefinitionId != WellKnownActivities.Waiting ||
                !current.SpatialContext.IsLocated ||
                !world.Locations.Get(current.SpatialContext.LocationId).Affords(routine.ActivityDefinitionId))
                return false;

            LocationId location = current.SpatialContext.LocationId;
            IReadOnlyList<CharacterId> candidates = _interactions.SelectCandidatesAtLocation(
                world,
                character.Id,
                location,
                routine.MaxCandidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                CharacterId target = candidates[i];
                if (!_interactions.TryInteractAtLocation(context, character.Id, target, location)) continue;

                ActivityDefinition activity = _catalog.Activities[routine.ActivityDefinitionId];
                var parameters = new SortedDictionary<AuthoredId, long>
                {
                    [ActivityNeedParameters.SatisfactionOffset(definition.Id)] = routine.SatisfactionOffset,
                    [TargetCharacterParameter] = target.Value,
                };
                _transitions.BeginActivity(
                    context,
                    character.Id,
                    activity.Id,
                    location,
                    activity.DefaultDuration,
                    committedParameters: parameters);

                TryGenerateInvitation(world, character.Id, target, definition, routine);

                if (context.Trace.IsEnabled)
                    context.Trace.Record(
                        "routine",
                        $"{world.Clock.Now} {character.Id} selected {target} for {activity.Id} at {location}");
                return true;
            }
            return false;
        }

        private void TryGenerateInvitation(
            WorldState world,
            CharacterId requester,
            CharacterId recipient,
            NeedDefinition need,
            SocializingRoutineDefinition routine)
        {
            SocialInvitationRoutineDefinition invitation = routine.Invitation;
            if (invitation == null ||
                !world.TryGetCurrentActivity(recipient, out ActivityInstance planActivity) ||
                !invitation.TryGetPlan(planActivity.DefinitionId, out SocialInvitationPlanDefinition plan))
                return;

            DecisionDefinition definition = _catalog.Decisions[invitation.DecisionDefinitionId];
            var options = new List<DecisionOption>(definition.Options.Count);
            for (int i = 0; i < definition.Options.Count; i++)
            {
                DecisionOption option = definition.Options[i].Copy();
                if (option.Id == invitation.AcceptOptionId)
                {
                    option.SetContext(
                        DecisionReasoningParameters.Target,
                        DecisionParameterValue.FromEntity(requester.ToRef()));
                }
                else
                {
                    option.SetContext(
                        DecisionReasoningParameters.InterestId,
                        DecisionParameterValue.FromAuthoredId(plan.InterestId));
                }
                options.Add(option);
            }

            var context = new SortedDictionary<AuthoredId, DecisionParameterValue>
            {
                [DecisionReasoningParameters.PlannedActivity] =
                    DecisionParameterValue.FromEntity(planActivity.Id.ToRef()),
                [DecisionReasoningParameters.NeedId] = DecisionParameterValue.FromAuthoredId(need.Id),
                [DecisionReasoningParameters.NeedSatisfactionOffset] =
                    DecisionParameterValue.FromInteger(routine.SatisfactionOffset),
            };
            var conflict = definition.ConflictScopeKind.IsSet
                ? new DecisionConflictScope(definition.ConflictScopeKind, recipient.ToRef())
                : DecisionConflictScope.None;
            _decisions.Generate(
                world,
                new CompiledDecisionGenerationRequest(
                    recipient,
                    definition.Id,
                    definition.TimeToResolve,
                    options,
                    definition.ReasoningProgram,
                    context,
                    conflict));
        }

        private static DecisionOption FindOption(IReadOnlyList<DecisionOption> options, AuthoredId id)
        {
            for (int i = 0; i < options.Count; i++) if (options[i].Id == id) return options[i];
            return null;
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
                definition.SocializingRoutine != null;
        }
    }

    public sealed class SocializingThresholdHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private readonly SocializingRoutineService _service;
        public SocializingThresholdHandler(SocializingRoutineService service)
            : base(NeedThresholdReachedEvent.Type) => _service = service;
        protected override void Handle(NeedThresholdReachedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToThreshold(context, e.CharacterId, e.NeedId);
    }

    public sealed class SocializingActivityStartedHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly SocializingRoutineService _service;
        public SocializingActivityStartedHandler(SocializingRoutineService service)
            : base(ActivityDomainEventTypes.ActivityStarted) => _service = service;
        protected override void Handle(ActivityStartedEvent e, WorldState world, SimulationContext context) =>
            _service.TryStartDeferred(context, e.CharacterId);
    }

    public sealed class SocializingArrivalHandler : DomainEventHandler<CharacterArrivedEvent>
    {
        private readonly SocializingRoutineService _service;
        public SocializingArrivalHandler(SocializingRoutineService service)
            : base(ActivityDomainEventTypes.CharacterArrived) => _service = service;
        protected override void Handle(CharacterArrivedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToArrival(context, e.CharacterId, e.LocationId);
    }

    public sealed class SocialInvitationDecisionResolvedHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly SocializingRoutineService _service;
        public SocialInvitationDecisionResolvedHandler(SocializingRoutineService service)
            : base(DecisionDomainEventTypes.DecisionResolved) => _service = service;
        protected override void Handle(DecisionResolvedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToDecisionResolved(context, e.DecisionId);
    }
}
