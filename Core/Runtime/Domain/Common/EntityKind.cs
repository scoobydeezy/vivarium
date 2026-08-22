namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Identifies which runtime-id family an <see cref="EntityRef"/> refers to (§7.1).
    /// <para>
    /// Values are persisted, so existing members must keep their numbers forever. Append only.
    /// </para>
    /// </summary>
    public enum EntityKind
    {
        None = 0,
        Character = 1,
        ActivityInstance = 2,
        Commitment = 3,
        Relationship = 4,
        Decision = 5,
        Location = 6,
        Group = 7,
        Household = 8,
        Employment = 9,
        ScheduledEvent = 10,
        HistoryEntry = 11,
    }
}
