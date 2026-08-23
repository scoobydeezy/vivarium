using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Activities
{
    /// <summary>Applies a time-accurate Activity modifier from a calibrated directional social appraisal.</summary>
    public sealed class SocialActivityPressureService
    {
        private readonly ActivityTransitionService _transitions;
        private readonly DefinitionCatalog _catalog;
        private readonly SocialPressureDefinition _pressure;
        private readonly AuthoredId _lensId;
        private readonly AuthoredId _activityId;
        private readonly AuthoredId _modifierId;
        private readonly long _pressuredRate;
        private readonly AppraisalStrength _minimumStrength;
        private readonly SocialPressureEvaluator _evaluator = new SocialPressureEvaluator();

        public SocialActivityPressureService(
            ActivityTransitionService transitions,
            DefinitionCatalog catalog,
            SocialPressureDefinition pressure,
            AuthoredId lensId,
            AuthoredId activityId,
            AuthoredId modifierId,
            long pressuredRate,
            AppraisalStrength minimumStrength = AppraisalStrength.Moderate)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _pressure = pressure ?? throw new ArgumentNullException(nameof(pressure));
            _lensId = lensId;
            _activityId = activityId;
            _modifierId = modifierId;
            _pressuredRate = pressuredRate;
            _minimumStrength = minimumStrength;
        }

        public void CharacterArrived(SimulationContext context, CharacterId arrived, LocationId location)
        {
            foreach (CharacterId occupant in context.World.Spatial.DirectOccupantsOf(location))
            {
                if (occupant == arrived) continue;
                ApplyIfRelevant(context, arrived, occupant);
                ApplyIfRelevant(context, occupant, arrived);
            }
        }

        public void CharacterDeparted(SimulationContext context, CharacterId departed, LocationId location)
        {
            foreach (CharacterId worker in context.World.Spatial.DirectOccupantsOf(location))
            {
                if (!context.World.TryGetCurrentActivity(worker, out ActivityInstance activity) ||
                    activity.DefinitionId != _activityId ||
                    !activity.HasModifier(_modifierId))
                {
                    continue;
                }

                long restoredRate = activity.CommittedParameterOr(ActivityTransitionService.PerformanceRateParameter, 0);
                _transitions.RemoveContextModifier(context, activity, _modifierId, restoredRate);
            }
        }

        private void ApplyIfRelevant(SimulationContext context, CharacterId worker, CharacterId cause)
        {
            if (!context.World.TryGetCurrentActivity(worker, out ActivityInstance activity) ||
                activity.DefinitionId != _activityId ||
                activity.HasModifier(_modifierId) ||
                !context.World.Characters.Get(worker).TryGetAppraisalField(_lensId, out AppraisalField _))
            {
                return;
            }

            CompositeSocialEvaluationResult evaluation = _evaluator.Evaluate(
                context.World,
                worker,
                cause,
                _lensId,
                new SocialEvaluationContext(),
                _pressure,
                _catalog);
            if (evaluation.NormalizedAppraisal >= 0 || evaluation.Strength < _minimumStrength)
            {
                return;
            }

            _transitions.ApplyContextModifier(
                context,
                activity,
                new ActivityContextModifier(_modifierId, context.World.Clock.Now, _pressuredRate, 1, cause.ToRef()));
        }
    }

    public sealed class SocialActivityArrivalHandler : DomainEventHandler<CharacterArrivedEvent>
    {
        private readonly SocialActivityPressureService _service;
        public SocialActivityArrivalHandler(SocialActivityPressureService service)
            : base(ActivityDomainEventTypes.CharacterArrived) => _service = service;
        protected override void Handle(CharacterArrivedEvent domainEvent, WorldState world, SimulationContext context) =>
            _service.CharacterArrived(context, domainEvent.CharacterId, domainEvent.LocationId);
    }

    public sealed class SocialActivityDepartureHandler : DomainEventHandler<CharacterDepartedEvent>
    {
        private readonly SocialActivityPressureService _service;
        public SocialActivityDepartureHandler(SocialActivityPressureService service)
            : base(ActivityDomainEventTypes.CharacterDeparted) => _service = service;
        protected override void Handle(CharacterDepartedEvent domainEvent, WorldState world, SimulationContext context) =>
            _service.CharacterDeparted(context, domainEvent.CharacterId, domainEvent.LocationId);
    }
}
