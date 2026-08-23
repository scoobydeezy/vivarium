using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Observation;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Relationships
{
    /// <summary>A subordinate social occurrence between characters who remain in their Activities.</summary>
    public sealed class InteractionOccurredEvent : IDomainEvent
    {
        public static readonly AuthoredId Type = new AuthoredId("domain.interaction.occurred");

        public InteractionOccurredEvent(
            CharacterId actor,
            CharacterId counterpart,
            LocationId locationId,
            RelationshipId relationshipId,
            TravelSegmentKey? travelSegment = null)
        {
            Actor = actor;
            Counterpart = counterpart;
            LocationId = locationId;
            RelationshipId = relationshipId;
            TravelSegment = travelSegment;
        }

        public AuthoredId EventType => Type;
        public CharacterId Actor { get; }
        public CharacterId Counterpart { get; }
        public LocationId LocationId { get; }
        public RelationshipId RelationshipId { get; }
        public TravelSegmentKey? TravelSegment { get; }
    }

    /// <summary>
    /// Resolves one bounded interaction opportunity created by a shared location (§32).
    /// It never starts or replaces an Activity; it only applies social consequences.
    /// </summary>
    public sealed class InteractionService
    {
        public static readonly AuthoredId AcquaintanceKind = new AuthoredId("relationship.acquaintance");
        private static readonly AuthoredId HistoryKind = new AuthoredId("history.interaction");
        private const long AffinityGain = 100;
        private const int FamiliarityGain = 250;

        private readonly InteractionCandidateSelector _candidates;
        private readonly KnowledgeDiscoveryService _discovery;

        public InteractionService(InteractionCandidateSelector candidates, KnowledgeDiscoveryService discovery)
        {
            _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        }

        public bool TryInteractOnArrival(
            SimulationContext context,
            CharacterId actor,
            LocationId locationId,
            int maxCandidates = 1)
        {
            return TryInteract(
                context,
                actor,
                context.World,
                context.World.Spatial.DirectOccupantsOf(locationId),
                maxCandidates,
                RandomScopeTypes.Location,
                locationId.Value,
                locationId,
                null);
        }

        /// <summary>Resolves one opportunity among travellers indexed on the same directed segment.</summary>
        public bool TryInteractOnTravelSegment(
            SimulationContext context,
            CharacterId actor,
            TravelSegmentKey segment,
            int maxCandidates = 1)
        {
            int scopeId = unchecked((segment.From.Value * 397) ^ segment.To.Value);
            return TryInteract(
                context,
                actor,
                context.World,
                context.World.Spatial.TravelersOn(segment),
                maxCandidates,
                RandomScopeTypes.TravelSegment,
                scopeId,
                LocationId.None,
                segment);
        }

        private bool TryInteract(
            SimulationContext context,
            CharacterId actor,
            WorldState world,
            IReadOnlyCollection<CharacterId> pool,
            int maxCandidates,
            AuthoredId scopeType,
            int scopeId,
            LocationId locationId,
            TravelSegmentKey? travelSegment)
        {
            int rollIndex = unchecked((actor.Value * 397) ^ (int)world.Clock.Now.TotalMinutes);
            IReadOnlyList<CharacterId> selected = _candidates.Select(
                actor,
                pool,
                world.RelationshipIndex,
                maxCandidates,
                scopeType,
                scopeId,
                rollIndex);

            for (int i = 0; i < selected.Count; i++)
            {
                CharacterId counterpart = selected[i];
                if (!world.Characters.TryGet(counterpart, out Characters.Character other) || !other.IsActive)
                {
                    continue;
                }

                Relationship relationship;
                if (world.RelationshipIndex.TryGetBetween(actor, counterpart, out RelationshipId relationshipId))
                {
                    relationship = world.Relationships.Get(relationshipId);
                    if (!relationship.IsActive || relationship.LastInteractionAt == world.Clock.Now)
                    {
                        continue;
                    }
                }
                else
                {
                    relationship = new Relationship(
                        world.RuntimeIds.Relationships.Next(),
                        actor,
                        counterpart,
                        AcquaintanceKind,
                        AnalyticalProgression.Constant(0, world.Clock.Now),
                        world.Clock.Now);
                    world.Relationships.Add(relationship.Id, relationship);
                    world.RelationshipIndex.Register(relationship);
                }

                relationship.RecordInteraction(world.Clock.Now, AffinityGain, FamiliarityGain);
                world.BumpRevision(relationship.RevisionKey);
                var subjects = new List<EntityRef> { actor.ToRef(), counterpart.ToRef(), relationship.Id.ToRef() };
                if (locationId.IsSet)
                {
                    subjects.Add(locationId.ToRef());
                }

                string contextLabel = travelSegment.HasValue ? travelSegment.Value.ToString() : locationId.ToString();
                world.HistoryLedger.Record(
                    HistoryKind,
                    world.Clock.Now,
                    RetentionTier.Recent,
                    $"{actor} interacted with {counterpart} at {contextLabel}",
                    subjects);

                ObserveIfWatched(context, actor);
                ObserveIfWatched(context, counterpart);
                world.Publish(new InteractionOccurredEvent(actor, counterpart, locationId, relationship.Id, travelSegment));

                if (context.Trace.IsEnabled)
                {
                    context.Trace.Record("interaction", $"{world.Clock.Now} {actor} ↔ {counterpart} at {contextLabel}");
                }

                return true;
            }

            return false;
        }

        private void ObserveIfWatched(SimulationContext context, CharacterId character)
        {
            WorldState world = context.World;
            if (!world.Attention.WatchStateOf(character).SupportsObservation)
            {
                return;
            }

            var observation = new Vivarium.Domain.Observation.Observation(
                ObservationKind.WitnessInteraction,
                character.ToRef(),
                world.Clock.Now,
                DiscoveryChannels.Conversation);
            int ordinal = world.Attention.NextObservationOrdinal(character);
            _discovery.Discover(
                world,
                observation.Subject,
                new DiscoveryChannel(DiscoveryChannels.Conversation),
                context,
                ordinal);
        }
    }

    /// <summary>Creates an interaction opportunity from an indexed occupancy change.</summary>
    public sealed class CharacterArrivedInteractionHandler : DomainEventHandler<CharacterArrivedEvent>
    {
        private readonly InteractionService _interactions;

        public CharacterArrivedInteractionHandler(InteractionService interactions)
            : base(ActivityDomainEventTypes.CharacterArrived)
        {
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        protected override void Handle(CharacterArrivedEvent domainEvent, WorldState world, SimulationContext context) =>
            _interactions.TryInteractOnArrival(context, domainEvent.CharacterId, domainEvent.LocationId);
    }

    /// <summary>Creates an opportunity when a character begins Traveling on an indexed segment.</summary>
    public sealed class TravelStartedInteractionHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly InteractionService _interactions;

        public TravelStartedInteractionHandler(InteractionService interactions)
            : base(ActivityDomainEventTypes.ActivityStarted)
        {
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        protected override void Handle(ActivityStartedEvent domainEvent, WorldState world, SimulationContext context)
        {
            if (domainEvent.DefinitionId != WellKnownActivities.Traveling ||
                !world.Activities.TryGet(domainEvent.ActivityInstanceId, out ActivityInstance activity) ||
                !activity.SpatialContext.IsTraveling)
            {
                return;
            }

            TransitDetails transit = activity.SpatialContext.Transit;
            _interactions.TryInteractOnTravelSegment(
                context,
                domainEvent.CharacterId,
                new TravelSegmentKey(transit.OriginLocationId, transit.DestinationLocationId));
        }
    }
}
