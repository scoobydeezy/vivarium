using Vivarium.Domain.Scheduling;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Owns every runtime-id allocator in the world (§7, §8). Persisted verbatim so a reloaded
    /// world continues allocating exactly the ids the original run would have allocated.
    /// </summary>
    public sealed class RuntimeIdState
    {
        public RuntimeIdState(RuntimeIdCounters restoredCounters = default)
        {
            Characters = new MonotonicIdAllocator<CharacterId>(v => new CharacterId(v), restoredCounters.Characters);
            Activities = new MonotonicIdAllocator<ActivityInstanceId>(v => new ActivityInstanceId(v), restoredCounters.Activities);
            Commitments = new MonotonicIdAllocator<CommitmentId>(v => new CommitmentId(v), restoredCounters.Commitments);
            Relationships = new MonotonicIdAllocator<RelationshipId>(v => new RelationshipId(v), restoredCounters.Relationships);
            Decisions = new MonotonicIdAllocator<DecisionId>(v => new DecisionId(v), restoredCounters.Decisions);
            Locations = new MonotonicIdAllocator<LocationId>(v => new LocationId(v), restoredCounters.Locations);
            Groups = new MonotonicIdAllocator<GroupId>(v => new GroupId(v), restoredCounters.Groups);
            ScheduledEvents = new MonotonicIdAllocator<ScheduledEventId>(v => new ScheduledEventId(v), restoredCounters.ScheduledEvents);
            HistoryEntries = new MonotonicIdAllocator<HistoryEntryId>(v => new HistoryEntryId(v), restoredCounters.HistoryEntries);
            EventSequence = new EventSequenceAllocator(restoredCounters.EventSequence);
        }

        public IIdAllocator<CharacterId> Characters { get; }

        public IIdAllocator<ActivityInstanceId> Activities { get; }

        public IIdAllocator<CommitmentId> Commitments { get; }

        public IIdAllocator<RelationshipId> Relationships { get; }

        public IIdAllocator<DecisionId> Decisions { get; }

        public IIdAllocator<LocationId> Locations { get; }

        public IIdAllocator<GroupId> Groups { get; }

        public IIdAllocator<ScheduledEventId> ScheduledEvents { get; }

        public IIdAllocator<HistoryEntryId> HistoryEntries { get; }

        /// <summary>
        /// Scheduler-local tie-break counter (§11). Deliberately distinct from the Application's
        /// <c>CommandSequence</c>: different scope, different lifetime.
        /// </summary>
        public EventSequenceAllocator EventSequence { get; }

        public RuntimeIdCounters Snapshot() => new RuntimeIdCounters(
            Characters.IssuedCount,
            Activities.IssuedCount,
            Commitments.IssuedCount,
            Relationships.IssuedCount,
            Decisions.IssuedCount,
            Locations.IssuedCount,
            Groups.IssuedCount,
            ScheduledEvents.IssuedCount,
            HistoryEntries.IssuedCount,
            EventSequence.Issued);
    }

    /// <summary>Flat, serialisable snapshot of every allocator counter.</summary>
    public readonly struct RuntimeIdCounters
    {
        public RuntimeIdCounters(
            int characters,
            int activities,
            int commitments,
            int relationships,
            int decisions,
            int locations,
            int groups,
            int scheduledEvents,
            int historyEntries,
            long eventSequence)
        {
            Characters = characters;
            Activities = activities;
            Commitments = commitments;
            Relationships = relationships;
            Decisions = decisions;
            Locations = locations;
            Groups = groups;
            ScheduledEvents = scheduledEvents;
            HistoryEntries = historyEntries;
            EventSequence = eventSequence;
        }

        public int Characters { get; }

        public int Activities { get; }

        public int Commitments { get; }

        public int Relationships { get; }

        public int Decisions { get; }

        public int Locations { get; }

        public int Groups { get; }

        public int ScheduledEvents { get; }

        public int HistoryEntries { get; }

        public long EventSequence { get; }
    }
}
