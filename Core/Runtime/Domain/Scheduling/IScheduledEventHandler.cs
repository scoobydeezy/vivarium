using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// Behaviour for one stable event type (§11.3). Serialized payloads contain no behaviour; handlers do.
    /// </summary>
    public interface IScheduledEventHandler
    {
        /// <summary>The stable authored event type this handler is registered against.</summary>
        AuthoredId EventType { get; }

        /// <summary>
        /// Authoritative semantic validation, run every time the event executes (§11.1).
        /// Revision checks are only an optimization — this is the real gate. If Mina is still
        /// travelling when work should begin, she is late, not magically present (§29.5).
        /// </summary>
        bool CanExecute(WorldState world, ScheduledEvent scheduled);

        void Execute(WorldState world, ScheduledEvent scheduled, SimulationContext context);
    }

    /// <summary>
    /// Convenience base that unpacks the payload, so concrete handlers work with typed data while the
    /// registry stays a simple <c>eventType → handler</c> map.
    /// </summary>
    public abstract class ScheduledEventHandler<TPayload> : IScheduledEventHandler
        where TPayload : IScheduledEventPayload
    {
        protected ScheduledEventHandler(AuthoredId eventType)
        {
            if (!eventType.IsSet)
            {
                throw new ArgumentException("Handlers must declare a stable authored event type.", nameof(eventType));
            }

            EventType = eventType;
        }

        public AuthoredId EventType { get; }

        public bool CanExecute(WorldState world, ScheduledEvent scheduled) => CanExecute(world, Unpack(scheduled));

        public void Execute(WorldState world, ScheduledEvent scheduled, SimulationContext context) =>
            Execute(world, Unpack(scheduled), context);

        protected abstract bool CanExecute(WorldState world, TPayload payload);

        protected abstract void Execute(WorldState world, TPayload payload, SimulationContext context);

        private static TPayload Unpack(ScheduledEvent scheduled)
        {
            if (scheduled.Payload is TPayload typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                $"Event {scheduled.EventType} carried payload '{scheduled.Payload?.GetType().Name ?? "null"}' but its handler expects '{typeof(TPayload).Name}'.");
        }
    }
}
