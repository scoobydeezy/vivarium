using Vivarium.Domain.Common;
using Vivarium.Domain.Events;

namespace Vivarium.Domain.Activities
{
    /// <summary>Authored Domain Event types. Stable identifiers for ordered handler chains (§12.1).</summary>
    public static class ActivityDomainEventTypes
    {
        public static readonly AuthoredId ActivityStarted = new AuthoredId("domain.activity.started");
        public static readonly AuthoredId ActivityCompleted = new AuthoredId("domain.activity.completed");
        public static readonly AuthoredId CharacterArrived = new AuthoredId("domain.character.arrived");
        public static readonly AuthoredId CharacterDeparted = new AuthoredId("domain.character.departed");
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
}
