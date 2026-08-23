using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>Authored Domain Event types. Stable identifiers for ordered handler chains (§12.1).</summary>
    public static class ActivityDomainEventTypes
    {
        public static readonly AuthoredId ActivityStarted = new AuthoredId("domain.activity.started");
        public static readonly AuthoredId ActivityCompleted = new AuthoredId("domain.activity.completed");
        public static readonly AuthoredId CharacterArrived = new AuthoredId("domain.character.arrived");
        public static readonly AuthoredId CharacterDeparted = new AuthoredId("domain.character.departed");
        public static readonly AuthoredId CommitmentScheduleChanged =
            new AuthoredId("domain.commitment.schedule_changed");
        public static readonly AuthoredId CommitmentStatusChanged =
            new AuthoredId("domain.commitment.status_changed");
        public static readonly AuthoredId CommitmentOutcomeOccurred =
            new AuthoredId("domain.commitment.outcome_occurred");
    }

    /// <summary>A character began a new primary Activity (§29.1).</summary>
    public sealed class ActivityStartedEvent : IDomainEvent
    {
        public ActivityStartedEvent(CharacterId characterId, ActivityInstanceId activityInstanceId, AuthoredId definitionId)
        {
            CharacterId = characterId;
            ActivityInstanceId = activityInstanceId;
            DefinitionId = definitionId;
        }

        public AuthoredId EventType => ActivityDomainEventTypes.ActivityStarted;

        public CharacterId CharacterId { get; }

        public ActivityInstanceId ActivityInstanceId { get; }

        public AuthoredId DefinitionId { get; }
    }

    /// <summary>
    /// An Activity finished and its result was accepted. Fires identically for automatic and
    /// player-provided outcomes, since both feed the same consequence pipeline (§29.6).
    /// </summary>
    public sealed class ActivityCompletedEvent : IDomainEvent
    {
        public ActivityCompletedEvent(CharacterId characterId, ActivityInstanceId activityInstanceId, ActivityPerformanceResult result)
        {
            CharacterId = characterId;
            ActivityInstanceId = activityInstanceId;
            Result = result;
        }

        public AuthoredId EventType => ActivityDomainEventTypes.ActivityCompleted;

        public CharacterId CharacterId { get; }

        public ActivityInstanceId ActivityInstanceId { get; }

        public ActivityPerformanceResult Result { get; }
    }

    /// <summary>
    /// A character became <c>Located</c> somewhere. The signal that a room's occupant set changed —
    /// which is how interaction opportunities arise from shared context rather than from scanning (§32).
    /// </summary>
    public sealed class CharacterArrivedEvent : IDomainEvent
    {
        public CharacterArrivedEvent(CharacterId characterId, LocationId locationId)
        {
            CharacterId = characterId;
            LocationId = locationId;
        }

        public AuthoredId EventType => ActivityDomainEventTypes.CharacterArrived;

        public CharacterId CharacterId { get; }

        public LocationId LocationId { get; }
    }

    /// <summary>A character stopped occupying a location.</summary>
    public sealed class CharacterDepartedEvent : IDomainEvent
    {
        public CharacterDepartedEvent(CharacterId characterId, LocationId locationId)
        {
            CharacterId = characterId;
            LocationId = locationId;
        }

        public AuthoredId EventType => ActivityDomainEventTypes.CharacterDeparted;

        public CharacterId CharacterId { get; }

        public LocationId LocationId { get; }
    }

    /// <summary>A character's authoritative commitment intent changed and feasibility must be revisited.</summary>
    public sealed class CommitmentScheduleChangedEvent : IDomainEvent
    {
        public CommitmentScheduleChangedEvent(CharacterId characterId, int scheduleRevision)
        {
            CharacterId = characterId;
            ScheduleRevision = scheduleRevision;
        }
        public AuthoredId EventType => ActivityDomainEventTypes.CommitmentScheduleChanged;
        public CharacterId CharacterId { get; }
        public int ScheduleRevision { get; }
    }

    public sealed class CommitmentStatusChangedEvent : IDomainEvent
    {
        public CommitmentStatusChangedEvent(
            CommitmentId commitmentId,
            CharacterId characterId,
            CommitmentStatus previousStatus,
            CommitmentStatus newStatus)
        {
            CommitmentId = commitmentId;
            CharacterId = characterId;
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
        public AuthoredId EventType => ActivityDomainEventTypes.CommitmentStatusChanged;
        public CommitmentId CommitmentId { get; }
        public CharacterId CharacterId { get; }
        public CommitmentStatus PreviousStatus { get; }
        public CommitmentStatus NewStatus { get; }
    }

    public sealed class CommitmentOutcomeOccurredEvent : IDomainEvent
    {
        public CommitmentOutcomeOccurredEvent(CharacterId characterId, CommitmentOutcome outcome)
        {
            CharacterId = characterId;
            Outcome = outcome;
        }
        public AuthoredId EventType => ActivityDomainEventTypes.CommitmentOutcomeOccurred;
        public CharacterId CharacterId { get; }
        public CommitmentOutcome Outcome { get; }
    }

    /// <summary>One authority for revisioning and announcing commitment-intent changes.</summary>
    public static class CommitmentScheduleChanges
    {
        public static int Publish(WorldState world, CharacterId characterId)
        {
            int revision = world.BumpRevision(new RevisionKey(characterId.ToRef(), RevisionAspects.Schedule));
            world.Publish(new CommitmentScheduleChangedEvent(characterId, revision));
            return revision;
        }
    }

    /// <summary>Publishes one schedule revision after a Commitment lifecycle transition.</summary>
    public sealed class CommitmentStatusScheduleChangeHandler : DomainEventHandler<CommitmentStatusChangedEvent>
    {
        public CommitmentStatusScheduleChangeHandler() : base(ActivityDomainEventTypes.CommitmentStatusChanged) { }
        protected override void Handle(CommitmentStatusChangedEvent e, WorldState world, SimulationContext context) =>
            CommitmentScheduleChanges.Publish(world, e.CharacterId);
    }

    public sealed class CommitmentOutcomeScheduleChangeHandler : DomainEventHandler<CommitmentOutcomeOccurredEvent>
    {
        public CommitmentOutcomeScheduleChangeHandler() : base(ActivityDomainEventTypes.CommitmentOutcomeOccurred) { }
        protected override void Handle(CommitmentOutcomeOccurredEvent e, WorldState world, SimulationContext context) =>
            CommitmentScheduleChanges.Publish(world, e.CharacterId);
    }
}
