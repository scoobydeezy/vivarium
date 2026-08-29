using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>Configurable content reaction for a disliked character sharing a Work location.</summary>
    public sealed class WorkContextPressureService
    {
        private readonly ActivityTransitionService _transitions;
        private readonly DecisionReevaluationService _reevaluation;
        private readonly AuthoredId _workActivityId;
        private readonly AuthoredId _modifierId;
        private readonly AuthoredId _decisionContextKind;
        private readonly long _relationshipThreshold;
        private readonly long _pressuredRate;

        public WorkContextPressureService(
            ActivityTransitionService transitions,
            DecisionReevaluationService reevaluation,
            AuthoredId workActivityId,
            AuthoredId modifierId,
            AuthoredId decisionContextKind,
            long affinityThreshold,
            long pressuredRate)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _reevaluation = reevaluation ?? throw new ArgumentNullException(nameof(reevaluation));
            _workActivityId = workActivityId;
            _modifierId = modifierId;
            _decisionContextKind = decisionContextKind;
            _relationshipThreshold = affinityThreshold;
            _pressuredRate = pressuredRate;
        }

        public void CharacterArrived(SimulationContext context, CharacterId arrived, LocationId location)
        {
            WorldState world = context.World;
            // Only an existing negative relationship can produce this modifier. Walk the arrived
            // character's derived relationship adjacency instead of every occupant in a potentially
            // population-scale location.
            foreach (CharacterId occupant in world.RelationshipIndex.KnownCharactersOf(arrived))
            {
                if (!world.Spatial.TryGetDirectLocation(occupant, out LocationId occupantLocation) ||
                    occupantLocation != location)
                {
                    continue;
                }

                ApplyIfRelevant(context, arrived, occupant);
                ApplyIfRelevant(context, occupant, arrived);
            }
        }

        public void CharacterDeparted(SimulationContext context, CharacterId departed, LocationId location)
        {
            WorldState world = context.World;
            foreach (CharacterId worker in world.RelationshipIndex.KnownCharactersOf(departed))
            {
                if (!world.Spatial.TryGetDirectLocation(worker, out LocationId workerLocation) ||
                    workerLocation != location ||
                    !IsNegativeRelationship(world, worker, departed) ||
                    !world.TryGetCurrentActivity(worker, out ActivityInstance activity) ||
                    activity.DefinitionId != _workActivityId ||
                    !activity.HasModifier(_modifierId))
                {
                    continue;
                }

                long restoredRate = activity.CommittedParameterOr(ActivityTransitionService.PerformanceRateParameter, 0);
                _transitions.RemoveContextModifier(context, activity, _modifierId, restoredRate);
                Reevaluate(context, worker);
            }
        }

        private void ApplyIfRelevant(SimulationContext context, CharacterId worker, CharacterId cause)
        {
            WorldState world = context.World;
            if (!IsNegativeRelationship(world, worker, cause) ||
                !world.TryGetCurrentActivity(worker, out ActivityInstance activity) ||
                activity.DefinitionId != _workActivityId ||
                activity.HasModifier(_modifierId))
            {
                return;
            }

            _transitions.ApplyContextModifier(
                context,
                activity,
                new ActivityContextModifier(_modifierId, world.Clock.Now, _pressuredRate, 1, cause.ToRef()));
            Reevaluate(context, worker);
        }

        private bool IsNegativeRelationship(WorldState world, CharacterId a, CharacterId b) =>
            world.RelationshipIndex.TryGetBetween(a, b, out RelationshipId id) &&
            world.Relationships.Get(id).IsActive &&
            world.Relationships.Get(id).From(a).ChannelAt(RelationshipChannels.Affection, world.Clock.Now) <= _relationshipThreshold;

        private void Reevaluate(SimulationContext context, CharacterId worker) =>
            _reevaluation.ReevaluateDependents(
                context,
                new DecisionDependencyKey(_decisionContextKind, worker.ToRef()));
    }

    public sealed class WorkContextArrivalHandler : DomainEventHandler<CharacterArrivedEvent>
    {
        private readonly WorkContextPressureService _service;
        public WorkContextArrivalHandler(WorkContextPressureService service)
            : base(ActivityDomainEventTypes.CharacterArrived) => _service = service;
        protected override void Handle(CharacterArrivedEvent domainEvent, WorldState world, SimulationContext context) =>
            _service.CharacterArrived(context, domainEvent.CharacterId, domainEvent.LocationId);
    }

    public sealed class WorkContextDepartureHandler : DomainEventHandler<CharacterDepartedEvent>
    {
        private readonly WorkContextPressureService _service;
        public WorkContextDepartureHandler(WorkContextPressureService service)
            : base(ActivityDomainEventTypes.CharacterDeparted) => _service = service;
        protected override void Handle(CharacterDepartedEvent domainEvent, WorldState world, SimulationContext context) =>
            _service.CharacterDeparted(context, domainEvent.CharacterId, domainEvent.LocationId);
    }
}
