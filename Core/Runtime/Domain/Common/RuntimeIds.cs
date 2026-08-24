using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Marker for the typed runtime-id value objects. Every family is a deterministically allocated,
    /// never-reused integer (§7) that also orders deterministically (§15).
    /// </summary>
    public interface IRuntimeId
    {
        int Value { get; }

        EntityKind Kind { get; }
    }

    /// <summary>Identity of a <see cref="Characters.Character"/>.</summary>
    public readonly struct CharacterId : IRuntimeId, IEquatable<CharacterId>, IComparable<CharacterId>
    {
        public static readonly CharacterId None = default;

        public CharacterId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Character;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Character, Value);

        public bool Equals(CharacterId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CharacterId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(CharacterId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Character#" + Value : "<none>";

        public static bool operator ==(CharacterId a, CharacterId b) => a.Value == b.Value;

        public static bool operator !=(CharacterId a, CharacterId b) => a.Value != b.Value;
    }

    /// <summary>Identity of an <see cref="Activities.ActivityInstance"/>.</summary>
    public readonly struct ActivityInstanceId : IRuntimeId, IEquatable<ActivityInstanceId>, IComparable<ActivityInstanceId>
    {
        public static readonly ActivityInstanceId None = default;

        public ActivityInstanceId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.ActivityInstance;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.ActivityInstance, Value);

        public bool Equals(ActivityInstanceId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ActivityInstanceId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(ActivityInstanceId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Activity#" + Value : "<none>";

        public static bool operator ==(ActivityInstanceId a, ActivityInstanceId b) => a.Value == b.Value;

        public static bool operator !=(ActivityInstanceId a, ActivityInstanceId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a <see cref="Activities.Commitment"/>.</summary>
    public readonly struct CommitmentId : IRuntimeId, IEquatable<CommitmentId>, IComparable<CommitmentId>
    {
        public static readonly CommitmentId None = default;

        public CommitmentId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Commitment;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Commitment, Value);

        public bool Equals(CommitmentId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CommitmentId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(CommitmentId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Commitment#" + Value : "<none>";

        public static bool operator ==(CommitmentId a, CommitmentId b) => a.Value == b.Value;

        public static bool operator !=(CommitmentId a, CommitmentId b) => a.Value != b.Value;
    }

    /// <summary>Identity of one immutable terminal Commitment outcome.</summary>
    public readonly struct CommitmentOutcomeId : IRuntimeId, IEquatable<CommitmentOutcomeId>, IComparable<CommitmentOutcomeId>
    {
        public static readonly CommitmentOutcomeId None = default;
        public CommitmentOutcomeId(int value) => Value = value;
        public int Value { get; }
        public EntityKind Kind => EntityKind.CommitmentOutcome;
        public bool IsSet => Value > 0;
        public EntityRef ToRef() => new EntityRef(EntityKind.CommitmentOutcome, Value);
        public bool Equals(CommitmentOutcomeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CommitmentOutcomeId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(CommitmentOutcomeId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsSet ? "CommitmentOutcome#" + Value : "<none>";
        public static bool operator ==(CommitmentOutcomeId a, CommitmentOutcomeId b) => a.Value == b.Value;
        public static bool operator !=(CommitmentOutcomeId a, CommitmentOutcomeId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a <see cref="Relationships.Relationship"/>.</summary>
    public readonly struct RelationshipId : IRuntimeId, IEquatable<RelationshipId>, IComparable<RelationshipId>
    {
        public static readonly RelationshipId None = default;

        public RelationshipId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Relationship;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Relationship, Value);

        public bool Equals(RelationshipId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is RelationshipId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(RelationshipId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Relationship#" + Value : "<none>";

        public static bool operator ==(RelationshipId a, RelationshipId b) => a.Value == b.Value;

        public static bool operator !=(RelationshipId a, RelationshipId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a <see cref="Decisions.Decision"/>.</summary>
    public readonly struct DecisionId : IRuntimeId, IEquatable<DecisionId>, IComparable<DecisionId>
    {
        public static readonly DecisionId None = default;

        public DecisionId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Decision;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Decision, Value);

        public bool Equals(DecisionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is DecisionId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(DecisionId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Decision#" + Value : "<none>";

        public static bool operator ==(DecisionId a, DecisionId b) => a.Value == b.Value;

        public static bool operator !=(DecisionId a, DecisionId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a <see cref="Spatial.LocationNode"/>.</summary>
    public readonly struct LocationId : IRuntimeId, IEquatable<LocationId>, IComparable<LocationId>
    {
        public static readonly LocationId None = default;

        public LocationId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Location;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Location, Value);

        public bool Equals(LocationId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is LocationId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(LocationId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Location#" + Value : "<none>";

        public static bool operator ==(LocationId a, LocationId b) => a.Value == b.Value;

        public static bool operator !=(LocationId a, LocationId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a non-spatial group: household, employer, club, faction (§31).</summary>
    public readonly struct GroupId : IRuntimeId, IEquatable<GroupId>, IComparable<GroupId>
    {
        public static readonly GroupId None = default;

        public GroupId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Group;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Group, Value);

        public bool Equals(GroupId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is GroupId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(GroupId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Group#" + Value : "<none>";

        public static bool operator ==(GroupId a, GroupId b) => a.Value == b.Value;

        public static bool operator !=(GroupId a, GroupId b) => a.Value != b.Value;
    }

    /// <summary>Identity of one character's employment relationship.</summary>
    public readonly struct EmploymentId : IRuntimeId, IEquatable<EmploymentId>, IComparable<EmploymentId>
    {
        public static readonly EmploymentId None = default;

        public EmploymentId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.Employment;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.Employment, Value);

        public bool Equals(EmploymentId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EmploymentId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(EmploymentId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Employment#" + Value : "<none>";

        public static bool operator ==(EmploymentId a, EmploymentId b) => a.Value == b.Value;

        public static bool operator !=(EmploymentId a, EmploymentId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a <see cref="Scheduling.ScheduledEvent"/>.</summary>
    public readonly struct ScheduledEventId : IRuntimeId, IEquatable<ScheduledEventId>, IComparable<ScheduledEventId>
    {
        public static readonly ScheduledEventId None = default;

        public ScheduledEventId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.ScheduledEvent;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.ScheduledEvent, Value);

        public bool Equals(ScheduledEventId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ScheduledEventId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(ScheduledEventId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Event#" + Value : "<none>";

        public static bool operator ==(ScheduledEventId a, ScheduledEventId b) => a.Value == b.Value;

        public static bool operator !=(ScheduledEventId a, ScheduledEventId b) => a.Value != b.Value;
    }

    /// <summary>Identity of a retained history entry (§37).</summary>
    public readonly struct HistoryEntryId : IRuntimeId, IEquatable<HistoryEntryId>, IComparable<HistoryEntryId>
    {
        public static readonly HistoryEntryId None = default;

        public HistoryEntryId(int value) => Value = value;

        public int Value { get; }

        public EntityKind Kind => EntityKind.HistoryEntry;

        public bool IsSet => Value > 0;

        public EntityRef ToRef() => new EntityRef(EntityKind.HistoryEntry, Value);

        public bool Equals(HistoryEntryId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is HistoryEntryId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(HistoryEntryId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "History#" + Value : "<none>";

        public static bool operator ==(HistoryEntryId a, HistoryEntryId b) => a.Value == b.Value;

        public static bool operator !=(HistoryEntryId a, HistoryEntryId b) => a.Value != b.Value;
    }
}
