using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
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

        public SocializingRoutineService(
            DefinitionCatalog catalog,
            ActivityTransitionService transitions,
            InteractionService interactions)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
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

                if (context.Trace.IsEnabled)
                    context.Trace.Record(
                        "routine",
                        $"{world.Clock.Now} {character.Id} selected {target} for {activity.Id} at {location}");
                return true;
            }
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
}
