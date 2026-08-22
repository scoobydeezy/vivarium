using System;
using System.Collections.Generic;
using Vivarium.Domain.Events;
using Vivarium.Domain.Scheduling;

namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// Drives one simulation instant to quiescence (§11.4, §12.1).
    /// <para>
    /// Handling an event may schedule more work at the same time, and a Domain Event handler may emit
    /// further Domain Events. Both settle <b>together</b> before the instant is considered complete:
    /// </para>
    /// <code>
    /// advance to 10:00 → process work → new 10:00 work appears → process it → … → nothing left → QUIESCENT
    /// </code>
    /// <para>
    /// Only after quiescence may projections be published (§13.1). Unity can never observe "Mina quit
    /// her job, but employment membership hasn't been removed yet" — that intermediate state simply is
    /// not externally observable.
    /// </para>
    /// </summary>
    public sealed class SettlementLoop
    {
        private readonly ScheduledEventHandlerRegistry _scheduledHandlers;
        private readonly OrderedDomainEventHandlerRegistry _domainHandlers;

        public SettlementLoop(
            ScheduledEventHandlerRegistry scheduledHandlers,
            OrderedDomainEventHandlerRegistry domainHandlers)
        {
            _scheduledHandlers = scheduledHandlers ?? throw new ArgumentNullException(nameof(scheduledHandlers));
            _domainHandlers = domainHandlers ?? throw new ArgumentNullException(nameof(domainHandlers));
        }

        /// <summary>Number of scheduled events discarded as stale since construction (diagnostics, §53).</summary>
        public int StaleEventsDiscarded { get; private set; }

        /// <summary>
        /// Settles all work at the world's current instant.
        /// </summary>
        /// <returns>Units of work performed — one per scheduled event and one per Domain Event handled.</returns>
        public int SettleCurrentInstant(WorldState world, SimulationContext context)
        {
            int work = 0;
            string lastWork = "<none>";

            while (true)
            {
                // Domain Event reactions first: they are consequences of work already done at this
                // instant, so settling them keeps cause ahead of effect (§12.1).
                if (world.DomainEvents.TryDequeue(out IDomainEvent domainEvent))
                {
                    lastWork = "DomainEvent " + domainEvent.EventType;
                    Dispatch(domainEvent, world, context);
                    work++;
                }
                else if (world.Scheduler.TryTakeNextDueAt(world.Clock.Now, out ScheduledEvent scheduled))
                {
                    lastWork = "ScheduledEvent " + scheduled;
                    Execute(scheduled, world, context);
                    work++;
                }
                else
                {
                    // Quiescent: no pending Domain Events, no scheduled work due at this instant.
                    return work;
                }

                if (work > context.MaxSettlementWorkPerSimulationInstant)
                {
                    throw new SimulationCascadeLimitExceeded(
                        world.Clock.Now.TotalMinutes,
                        work,
                        context.MaxSettlementWorkPerSimulationInstant,
                        lastWork);
                }
            }
        }

        private void Dispatch(IDomainEvent domainEvent, WorldState world, SimulationContext context)
        {
            IReadOnlyList<IDomainEventHandler> handlers = _domainHandlers.HandlersFor(domainEvent.EventType);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record("domain-event", $"{world.Clock.Now} {domainEvent.EventType} → {handlers.Count} handler(s)");
            }

            // Explicitly registered order. Never subscription or load order (§12.1).
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].Handle(domainEvent, world, context);
            }
        }

        private void Execute(ScheduledEvent scheduled, WorldState world, SimulationContext context)
        {
            IScheduledEventHandler handler = _scheduledHandlers.Resolve(scheduled.EventType);

            // Revision check: cheap stale detection from the aspect-scoped dependencies the event
            // actually declared (§11.2.1). A mismatch means the state it depended on has moved on.
            if (!scheduled.DependenciesSatisfied(world.Revisions))
            {
                StaleEventsDiscarded++;
                if (context.Trace.IsEnabled)
                {
                    context.Trace.Record("stale-event", $"{world.Clock.Now} discarded {scheduled} (revision mismatch)");
                }

                return;
            }

            // Semantic validation is authoritative — matching revisions never excuse skipping it (§11.2).
            if (!handler.CanExecute(world, scheduled))
            {
                StaleEventsDiscarded++;
                if (context.Trace.IsEnabled)
                {
                    context.Trace.Record("invalid-event", $"{world.Clock.Now} discarded {scheduled} (semantic validation)");
                }

                return;
            }

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record("scheduled-event", $"{world.Clock.Now} executing {scheduled}");
            }

            // Bracketed so the scheduler can reject same-instant work aimed at an earlier phase (§11.4).
            world.Scheduler.EnterExecution(scheduled);
            try
            {
                handler.Execute(world, scheduled, context);
            }
            finally
            {
                world.Scheduler.ExitExecution();
            }
        }
    }
}
