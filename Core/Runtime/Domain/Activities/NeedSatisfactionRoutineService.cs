using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Turns an increasing pressure Need into an ordinary satisfying Activity at the nearest reachable
    /// location that explicitly affords it. Busy Activities are never interrupted by this routine.
    /// </summary>
    public sealed class NeedSatisfactionRoutineService
    {
        private const string SatisfactionParameterPrefix = "activity.param.need_satisfaction.";
        private readonly DefinitionCatalog _catalog;
        private readonly NeedProgressionService _needs;
        private readonly ActivityTransitionService _transitions;

        public NeedSatisfactionRoutineService(
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

        public void ReactToActivityCompleted(
            SimulationContext context,
            CharacterId characterId,
            ActivityInstanceId activityInstanceId)
        {
            if (!context.World.Characters.TryGet(characterId, out Character character) ||
                !context.World.Activities.TryGet(activityInstanceId, out ActivityInstance activity))
                return;

            var needIds = new List<AuthoredId>(character.Needs.Keys);
            needIds.Sort();
            for (int i = 0; i < needIds.Count; i++)
            {
                AuthoredId needId = needIds[i];
                AuthoredId parameter = SatisfactionParameter(needId);
                long offset = activity.CommittedParameterOr(parameter, 0);
                if (offset != 0) _needs.ApplyOffset(context, character, needId, offset);
            }
        }

        private bool TryStart(SimulationContext context, Character character, NeedDefinition definition)
        {
            WorldState world = context.World;
            NeedSatisfactionRoutineDefinition routine = definition.SatisfactionRoutine;
            if (routine == null ||
                !character.TryGetNeed(definition.Id, out NeedState need) ||
                need.ValueAt(world.Clock.Now) < routine.ActivationThreshold ||
                !world.TryGetCurrentActivity(character.Id, out ActivityInstance current) ||
                current.DefinitionId != WellKnownActivities.Waiting ||
                !current.SpatialContext.IsLocated ||
                !TryFindNearestAffordingLocation(
                    world,
                    current.SpatialContext.LocationId,
                    routine.ActivityDefinitionId,
                    out LocationId destination))
                return false;

            ActivityDefinition activity = _catalog.Activities[routine.ActivityDefinitionId];
            var parameters = new SortedDictionary<AuthoredId, long>
            {
                [SatisfactionParameter(definition.Id)] = routine.SatisfactionOffset,
            };

            if (current.SpatialContext.LocationId == destination)
            {
                _transitions.BeginActivity(
                    context,
                    character.Id,
                    activity.Id,
                    destination,
                    activity.DefaultDuration,
                    committedParameters: parameters);
                return true;
            }

            return _transitions.TryBeginTravel(
                context,
                character.Id,
                destination,
                out ActivityInstance _,
                continuationActivityDefinitionId: activity.Id,
                continuationDuration: activity.DefaultDuration,
                continuationCommittedParameters: parameters);
        }

        private static bool TryFindNearestAffordingLocation(
            WorldState world,
            LocationId origin,
            AuthoredId activityDefinitionId,
            out LocationId locationId)
        {
            locationId = LocationId.None;
            SimDuration bestCost = default;
            foreach (LocationId candidate in world.Locations.Affording(activityDefinitionId))
            {
                LocationNode node = world.Locations.Get(candidate);
                if (!node.IsOccupiable || !world.TravelNetwork.TryPlanRoute(origin, candidate, out TravelPlan plan))
                    continue;
                if (!locationId.IsSet || plan.TotalCost < bestCost ||
                    (plan.TotalCost == bestCost && candidate.CompareTo(locationId) < 0))
                {
                    locationId = candidate;
                    bestCost = plan.TotalCost;
                }
            }
            return locationId.IsSet;
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
                definition.SatisfactionRoutine != null;
        }

        private static AuthoredId SatisfactionParameter(AuthoredId needId) =>
            new AuthoredId(SatisfactionParameterPrefix + needId.Value);
    }

    public sealed class NeedSatisfactionThresholdHandler : DomainEventHandler<NeedThresholdReachedEvent>
    {
        private readonly NeedSatisfactionRoutineService _service;
        public NeedSatisfactionThresholdHandler(NeedSatisfactionRoutineService service)
            : base(NeedThresholdReachedEvent.Type) => _service = service;
        protected override void Handle(NeedThresholdReachedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToThreshold(context, e.CharacterId, e.NeedId);
    }

    public sealed class NeedSatisfactionActivityStartedHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly NeedSatisfactionRoutineService _service;
        public NeedSatisfactionActivityStartedHandler(NeedSatisfactionRoutineService service)
            : base(ActivityDomainEventTypes.ActivityStarted) => _service = service;
        protected override void Handle(ActivityStartedEvent e, WorldState world, SimulationContext context) =>
            _service.TryStartDeferred(context, e.CharacterId);
    }

    public sealed class NeedSatisfactionActivityCompletedHandler : DomainEventHandler<ActivityCompletedEvent>
    {
        private readonly NeedSatisfactionRoutineService _service;
        public NeedSatisfactionActivityCompletedHandler(NeedSatisfactionRoutineService service)
            : base(ActivityDomainEventTypes.ActivityCompleted) => _service = service;
        protected override void Handle(ActivityCompletedEvent e, WorldState world, SimulationContext context) =>
            _service.ReactToActivityCompleted(context, e.CharacterId, e.ActivityInstanceId);
    }
}
