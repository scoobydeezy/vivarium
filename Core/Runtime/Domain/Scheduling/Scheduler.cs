using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// The persistent discrete-event scheduler — foundational simulation infrastructure (§11).
    /// <para>
    /// Ordering is total and deterministic: <c>DueAt → Phase → EventSequence</c>. Two events can
    /// therefore never have undefined relative order (§11).
    /// </para>
    /// <para>
    /// The scheduler stores and orders work; it does not execute it. Handler resolution, revision
    /// checks, and semantic validation belong to <see cref="Vivarium.Domain.Simulation.SettlementLoop"/>,
    /// which keeps scheduled-event data free of behaviour (§11.3).
    /// </para>
    /// </summary>
    public sealed class Scheduler
    {
        private sealed class DeterministicOrder : IComparer<ScheduledEvent>
        {
            public static readonly DeterministicOrder Instance = new DeterministicOrder();

            public int Compare(ScheduledEvent a, ScheduledEvent b)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }

                int byDue = a.DueAt.CompareTo(b.DueAt);
                if (byDue != 0)
                {
                    return byDue;
                }

                int byPhase = ((int)a.Phase).CompareTo((int)b.Phase);
                return byPhase != 0 ? byPhase : a.EventSequence.CompareTo(b.EventSequence);
            }
        }

        private readonly SortedSet<ScheduledEvent> _queue = new SortedSet<ScheduledEvent>(DeterministicOrder.Instance);
        private readonly Dictionary<ScheduledEventId, ScheduledEvent> _byId = new Dictionary<ScheduledEventId, ScheduledEvent>();
        private readonly IIdAllocator<ScheduledEventId> _ids;
        private readonly EventSequenceAllocator _sequence;

        private bool _isExecuting;
        private SimTime _executingInstant;
        private SchedulePhase _executingPhase;

        public Scheduler(IIdAllocator<ScheduledEventId> ids, EventSequenceAllocator sequence)
        {
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        }

        public int PendingCount => _queue.Count;

        /// <summary>Pending events in execution order — the canonical form for persistence (§38, §40).</summary>
        public IEnumerable<ScheduledEvent> PendingEvents => _queue;

        /// <summary>Schedules new future work.</summary>
        public ScheduledEvent Schedule(
            SimTime dueAt,
            SchedulePhase phase,
            AuthoredId eventType,
            IScheduledEventPayload payload,
            IReadOnlyList<EventDependency> dependencies = null)
        {
            GuardScheduleTarget(dueAt, phase);

            var scheduled = new ScheduledEvent(
                _ids.Next(),
                dueAt,
                phase,
                _sequence.Next(),
                eventType,
                payload,
                dependencies);

            Insert(scheduled);
            return scheduled;
        }

        /// <summary>
        /// Reinstates an event deserialized from a save, preserving its id and sequence exactly.
        /// Restoring must never mint new ids or sequences (§38).
        /// </summary>
        public void Restore(ScheduledEvent scheduled)
        {
            if (scheduled == null)
            {
                throw new ArgumentNullException(nameof(scheduled));
            }

            Insert(scheduled);
        }

        public bool Contains(ScheduledEventId id) => _byId.ContainsKey(id);

        public bool TryGet(ScheduledEventId id, out ScheduledEvent scheduled) => _byId.TryGetValue(id, out scheduled);

        /// <summary>
        /// Cancels a pending event. Used when a future event becomes obsolete outright — Mina lost the
        /// job at 3:20, so <c>MinaLeavesWork</c> at 3:42 should never fire (§11.1).
        /// </summary>
        public bool Cancel(ScheduledEventId id)
        {
            if (!_byId.TryGetValue(id, out ScheduledEvent scheduled))
            {
                return false;
            }

            _queue.Remove(scheduled);
            _byId.Remove(id);
            return true;
        }

        /// <summary>
        /// Moves an event to a new time, keeping its identity but taking a later
        /// <see cref="ScheduledEvent.EventSequence"/> so it cannot jump ahead of its own cause.
        /// </summary>
        public ScheduledEvent Reschedule(ScheduledEventId id, SimTime newDueAt, SchedulePhase? newPhase = null)
        {
            if (!_byId.TryGetValue(id, out ScheduledEvent existing))
            {
                throw new KeyNotFoundException($"Cannot reschedule unknown event {id}.");
            }

            SchedulePhase phase = newPhase ?? existing.Phase;
            GuardScheduleTarget(newDueAt, phase);

            _queue.Remove(existing);
            ScheduledEvent replacement = existing.WithSchedule(newDueAt, phase, _sequence.Next());
            _byId[id] = replacement;
            _queue.Add(replacement);
            return replacement;
        }

        /// <summary>The next event in deterministic order, or <c>null</c> when the queue is empty.</summary>
        public ScheduledEvent PeekNext() => _queue.Count == 0 ? null : _queue.Min;

        /// <summary>
        /// Removes and returns the next event due at or before <paramref name="instant"/>.
        /// The settlement loop calls this repeatedly until it returns <c>false</c>, which is one half
        /// of reaching quiescence (§11.4).
        /// </summary>
        public bool TryTakeNextDueAt(SimTime instant, out ScheduledEvent scheduled)
        {
            ScheduledEvent next = PeekNext();
            if (next == null || next.DueAt > instant)
            {
                scheduled = null;
                return false;
            }

            _queue.Remove(next);
            _byId.Remove(next.Id);
            scheduled = next;
            return true;
        }

        /// <summary>
        /// Declares that <paramref name="scheduled"/> is now executing, enabling the §11.4 guard
        /// against scheduling same-instant work into an earlier phase.
        /// </summary>
        internal void EnterExecution(ScheduledEvent scheduled)
        {
            _isExecuting = true;
            _executingInstant = scheduled.DueAt;
            _executingPhase = scheduled.Phase;
        }

        internal void ExitExecution()
        {
            _isExecuting = false;
        }

        private void GuardScheduleTarget(SimTime dueAt, SchedulePhase phase)
        {
            if (!_isExecuting || dueAt != _executingInstant)
            {
                return;
            }

            if ((int)phase < (int)_executingPhase)
            {
                throw new InvalidOperationException(
                    $"An event executing in phase {_executingPhase} at {dueAt} cannot schedule same-instant work into the earlier phase {phase}; that would insert work before its own cause (§11.4).");
            }
        }

        private void Insert(ScheduledEvent scheduled)
        {
            if (_byId.ContainsKey(scheduled.Id))
            {
                throw new InvalidOperationException($"Event {scheduled.Id} is already scheduled.");
            }

            _byId.Add(scheduled.Id, scheduled);
            _queue.Add(scheduled);
        }
    }
}
