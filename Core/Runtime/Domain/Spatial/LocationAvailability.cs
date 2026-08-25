using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Spatial
{
    public static class LocationAvailabilityRules
    {
        public static readonly AuthoredId ReasonUnknownLocation = new AuthoredId("location.availability.unknown_location");
        public static readonly AuthoredId ReasonNotManaged = new AuthoredId("location.availability.not_player_managed");
        public static readonly AuthoredId ReasonAlreadySet = new AuthoredId("location.availability.already_set");
        public static readonly AuthoredId ReasonInsufficientNudges = new AuthoredId("location.availability.insufficient_nudges");

        public const int NudgeCost = 1;

        public static Result Evaluate(WorldState world, LocationId locationId, bool open)
        {
            if (!world.Locations.TryGet(locationId, out LocationNode location))
                return Result.Fail(ReasonUnknownLocation, locationId.ToString());
            if (!location.SupportsPlayerManagedAvailability)
                return Result.Fail(ReasonNotManaged, locationId.ToString());
            if (location.IsOpen == open)
                return Result.Fail(ReasonAlreadySet, open ? "Already open." : "Already closed.");
            if (!world.Nudges.CanSpend(NudgeCost))
                return Result.Fail(ReasonInsufficientNudges, $"Needs {NudgeCost}; balance is {world.Nudges.Balance}.");
            return Result.Ok();
        }
    }

    public static class LocationAvailabilityDomainEventTypes
    {
        public static readonly AuthoredId Changed = new AuthoredId("domain.location.availability_changed");
    }

    public sealed class LocationAvailabilityChangedEvent : IDomainEvent
    {
        public LocationAvailabilityChangedEvent(LocationId locationId, bool isOpen, int revision)
        {
            LocationId = locationId;
            IsOpen = isOpen;
            Revision = revision;
        }

        public AuthoredId EventType => LocationAvailabilityDomainEventTypes.Changed;
        public LocationId LocationId { get; }
        public bool IsOpen { get; }
        public int Revision { get; }
    }

    /// <summary>Revalidates only in-flight discretionary activities depending on the changed destination.</summary>
    public sealed class LocationAvailabilityTravelRevalidationHandler : DomainEventHandler<LocationAvailabilityChangedEvent>
    {
        private readonly ActivityTransitionService _transitions;

        public LocationAvailabilityTravelRevalidationHandler(ActivityTransitionService transitions)
            : base(LocationAvailabilityDomainEventTypes.Changed) =>
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));

        protected override void Handle(LocationAvailabilityChangedEvent e, WorldState world, SimulationContext context)
        {
            if (e.IsOpen) return;

            var affected = new List<CharacterId>(world.Spatial.TravelersTo(e.LocationId));
            for (int i = 0; i < affected.Count; i++)
            {
                CharacterId characterId = affected[i];
                if (!world.TryGetCurrentActivity(characterId, out ActivityInstance travel) ||
                    !travel.SpatialContext.IsTraveling ||
                    !world.Scheduler.TryGet(travel.PendingCompletionEventId, out ScheduledEvent scheduled) ||
                    !(scheduled.Payload is TravelArrivalPayload arrival) ||
                    !arrival.ContinuationActivityDefinitionId.IsSet)
                    continue;

                LocationNode destination = world.Locations.Get(e.LocationId);
                if (!destination.Affords(arrival.ContinuationActivityDefinitionId)) continue;

                // Generic invalid-travel fallback. Ordinary ActivityStarted reactions may then choose
                // another real affordance; this handler never assigns a preferred replacement outcome.
                _transitions.BeginActivity(
                    context,
                    characterId,
                    WellKnownActivities.Waiting,
                    travel.SpatialContext.Transit.OriginLocationId,
                    SimDuration.FromHours(1));
            }
        }
    }

    public sealed class LocationAvailabilityHistoryHandler : DomainEventHandler<LocationAvailabilityChangedEvent>
    {
        public static readonly AuthoredId HistoryKind = new AuthoredId("history.location.availability_changed");
        public LocationAvailabilityHistoryHandler() : base(LocationAvailabilityDomainEventTypes.Changed) { }

        protected override void Handle(LocationAvailabilityChangedEvent e, WorldState world, SimulationContext context)
        {
            LocationNode location = world.Locations.Get(e.LocationId);
            world.HistoryLedger.Record(
                HistoryKind,
                world.Clock.Now,
                RetentionTier.Recent,
                $"{location.DisplayName} was {(e.IsOpen ? "opened" : "closed")}.",
                new[] { e.LocationId.ToRef() });
        }
    }
}
