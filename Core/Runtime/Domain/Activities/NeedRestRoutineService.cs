using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Executes content-backed Need recovery routines without turning ordinary Sleep into a Decision.
    /// It is deliberately narrow: the Need definition supplies policy, households supply place, and
    /// Activity transitions remain the sole mutation authority.
    /// </summary>
    public sealed class NeedRestRoutineService
    {
        private readonly DefinitionCatalog _catalog;
        private readonly NeedProgressionService _needs;
        private readonly ActivityTransitionService _transitions;

        public NeedRestRoutineService(
            DefinitionCatalog catalog,
            NeedProgressionService needs,
            ActivityTransitionService transitions)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public void ReactToThreshold(SimulationContext context, CharacterId characterId, AuthoredId needId)
        {
            if (!TryGet(context.World, characterId, needId, out Character character, out NeedDefinition definition))
                return;

            NeedRestRoutineDefinition rest = definition.RestRoutine;
            ActivityInstance current = CurrentActivity(context.World, character);
            if (current == null)
                return;

            if (current.DefinitionId == rest.ActivityDefinitionId)
            {
                if (character.TryGetNeed(needId, out NeedState need) &&
                    need.ValueAt(context.World.Clock.Now) >= rest.RecoveredThreshold &&
                    current.SpatialContext.IsLocated)
                {
                    _transitions.BeginActivity(
                        context,
                        characterId,
                        WellKnownActivities.Waiting,
                        current.SpatialContext.LocationId,
                        _catalog.Activities[WellKnownActivities.Waiting].DefaultDuration);
                }
                return;
            }

            TryStartRecovery(context, character, definition);
        }

        public void ReactToActivityStarted(SimulationContext context, CharacterId characterId, AuthoredId activityDefinitionId)
        {
            if (!context.World.Characters.TryGet(characterId, out Character character))
                return;

            foreach (AuthoredId needId in new List<AuthoredId>(character.Needs.Keys))
            {
                if (!_catalog.Needs.TryGetValue(needId, out NeedDefinition definition) ||
                    definition.RestRoutine == null ||
                    !character.TryGetNeed(needId, out NeedState need))
                    continue;

                NeedRestRoutineDefinition rest = definition.RestRoutine;
                if (activityDefinitionId == rest.ActivityDefinitionId)
                {
                    _needs.SetRateAndThreshold(
                        context,
                        character,
                        definition.Id,
                        rest.RecoveryRateNumerator,
                        rest.RecoveryRateDenominator,
                        rest.RecoveredThreshold);
                }
                else if (need.BehaviouralThreshold == rest.RecoveredThreshold)
                {
                    _needs.SetRateAndThreshold(
                        context,
                        character,
                        definition.Id,
                        definition.DefaultRateNumerator,
                        definition.DefaultRateDenominator,
                        rest.ActivationThreshold);
                }
            }
        }

        public void TryStartDeferredRecovery(SimulationContext context, CharacterId characterId)
        {
            if (!context.World.Characters.TryGet(characterId, out Character character))
                return;

            foreach (var pair in character.Needs)
            {
                if (_catalog.Needs.TryGetValue(pair.Key, out NeedDefinition definition) &&
                    definition.RestRoutine != null &&
                    pair.Value.ValueAt(context.World.Clock.Now) <= definition.RestRoutine.ActivationThreshold &&
                    TryStartRecovery(context, character, definition))
                    return;
            }
        }

        private bool TryStartRecovery(SimulationContext context, Character character, NeedDefinition definition)
        {
            WorldState world = context.World;
            ActivityInstance current = CurrentActivity(world, character);
            NeedRestRoutineDefinition rest = definition.RestRoutine;
            if (current == null || current.DefinitionId != WellKnownActivities.Waiting || !current.SpatialContext.IsLocated)
                return false;
            if (!character.TryGetNeed(definition.Id, out NeedState need) ||
                need.ValueAt(world.Clock.Now) > rest.ActivationThreshold)
                return false;
            if (!TryFindRecoveryLocation(world, character.Id, rest.LocationGroupKindId, out LocationId locationId))
                return false;

            ActivityDefinition activity = _catalog.Activities[rest.ActivityDefinitionId];
            if (current.SpatialContext.LocationId == locationId)
            {
                _transitions.BeginActivity(
                    context,
                    character.Id,
                    rest.ActivityDefinitionId,
                    locationId,
                    activity.DefaultDuration);
                return true;
            }

            return _transitions.TryBeginTravel(
                context,
                character.Id,
                locationId,
                out ActivityInstance _,
                default,
                rest.ActivityDefinitionId,
                activity.DefaultDuration);
        }

        private static bool TryFindRecoveryLocation(
            WorldState world,
            CharacterId characterId,
            AuthoredId groupKindId,
            out LocationId locationId)
        {
            foreach (GroupId groupId in world.Memberships.GroupsOf(characterId))
            {
                if (world.Groups.TryGet(groupId, out Group group) &&
                    group.Kind == groupKindId &&
                    group.PrimaryLocationId.IsSet)
                {
                    locationId = group.PrimaryLocationId;
                    return true;
                }
            }

            locationId = LocationId.None;
            return false;
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
                definition.RestRoutine != null;
        }

        private static ActivityInstance CurrentActivity(WorldState world, Character character) =>
            character.CurrentActivityId.IsSet && world.Activities.TryGet(character.CurrentActivityId, out ActivityInstance current)
                ? current
                : null;
    }

    public sealed class NeedRestThresholdHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private readonly NeedRestRoutineService _service;
        public NeedRestThresholdHandler(NeedRestRoutineService service)
            : base(NeedThresholdReachedEvent.Type) => _service = service;
        protected override void Handle(NeedThresholdReachedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToThreshold(context, e.CharacterId, e.NeedId);
    }

    public sealed class NeedRestActivityStartedHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly NeedRestRoutineService _service;
        public NeedRestActivityStartedHandler(NeedRestRoutineService service)
            : base(ActivityDomainEventTypes.ActivityStarted) => _service = service;
        protected override void Handle(ActivityStartedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToActivityStarted(context, e.CharacterId, e.DefinitionId);
    }

    public sealed class NeedRestActivityCompletedHandler : DomainEventHandler<ActivityCompletedEvent>
    {
        private readonly NeedRestRoutineService _service;
        public NeedRestActivityCompletedHandler(NeedRestRoutineService service)
            : base(ActivityDomainEventTypes.ActivityCompleted) => _service = service;
        protected override void Handle(ActivityCompletedEvent e, WorldState world, SimulationContext context) =>
            _service.TryStartDeferredRecovery(context, e.CharacterId);
    }

    public sealed class NeedRestArrivalHandler : DomainEventHandler<CharacterArrivedEvent>
    {
        private readonly NeedRestRoutineService _service;
        public NeedRestArrivalHandler(NeedRestRoutineService service)
            : base(ActivityDomainEventTypes.CharacterArrived) => _service = service;
        protected override void Handle(CharacterArrivedEvent e, WorldState world, SimulationContext context) =>
            _service.TryStartDeferredRecovery(context, e.CharacterId);
    }
}
