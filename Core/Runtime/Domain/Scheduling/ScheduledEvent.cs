using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// Marker for scheduled-event payloads. Payloads are <b>pure serializable data</b> (§11.3) —
    /// no behaviour, no entity references, no delegates. Handlers own behaviour.
    /// </summary>
    public interface IScheduledEventPayload
    {
    }

    /// <summary>
    /// Something that may happen in the future: "Mina's shift ends at 3:42" (§11, §12).
    /// <para>
    /// Persistent save state (§40) and immutable — rescheduling produces a new instance with a later
    /// <see cref="EventSequence"/> so causal ordering inside an instant is never violated.
    /// </para>
    /// </summary>
    public sealed class ScheduledEvent
    {
        private static readonly EventDependency[] NoDependencies = new EventDependency[0];

        public ScheduledEvent(
            ScheduledEventId id,
            SimTime dueAt,
            SchedulePhase phase,
            long eventSequence,
            AuthoredId eventType,
            IScheduledEventPayload payload,
            IReadOnlyList<EventDependency> dependencies = null)
        {
            if (!eventType.IsSet)
            {
                throw new ArgumentException("A scheduled event needs a stable authored event type.", nameof(eventType));
            }

            Id = id;
            DueAt = dueAt;
            Phase = phase;
            EventSequence = eventSequence;
            EventType = eventType;
            Payload = payload;
            Dependencies = dependencies == null || dependencies.Count == 0
                ? NoDependencies
                : CopyOf(dependencies);
        }

        public ScheduledEventId Id { get; }

        public SimTime DueAt { get; }

        public SchedulePhase Phase { get; }

        /// <summary>Scheduler-local tie-break within the same <see cref="DueAt"/> and <see cref="Phase"/>.</summary>
        public long EventSequence { get; }

        /// <summary>Stable authored type used to resolve the handler, e.g. <c>event.activity.complete</c>.</summary>
        public AuthoredId EventType { get; }

        public IScheduledEventPayload Payload { get; }

        /// <summary>Aspect-scoped revisions this event depends on (§11.2.1). Often empty.</summary>
        public IReadOnlyList<EventDependency> Dependencies { get; }

        /// <summary>Cheap stale check. Never a substitute for the handler's semantic validation.</summary>
        public bool DependenciesSatisfied(RevisionRegistry revisions)
        {
            for (int i = 0; i < Dependencies.Count; i++)
            {
                if (!Dependencies[i].IsSatisfiedBy(revisions))
                {
                    return false;
                }
            }

            return true;
        }

        internal ScheduledEvent WithSchedule(SimTime dueAt, SchedulePhase phase, long eventSequence) =>
            new ScheduledEvent(Id, dueAt, phase, eventSequence, EventType, Payload, Dependencies);

        public override string ToString() => $"{DueAt} [{Phase}/{EventSequence}] {EventType} ({Id})";

        private static EventDependency[] CopyOf(IReadOnlyList<EventDependency> source)
        {
            var copy = new EventDependency[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
