using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Events
{
    /// <summary>A deterministic reaction to a Domain Event (§12.1).</summary>
    public interface IDomainEventHandler
    {
        AuthoredId EventType { get; }

        void Handle(IDomainEvent domainEvent, WorldState world, SimulationContext context);
    }

    /// <summary>Typed convenience base for Domain Event handlers.</summary>
    public abstract class DomainEventHandler<TEvent> : IDomainEventHandler
        where TEvent : IDomainEvent
    {
        protected DomainEventHandler(AuthoredId eventType)
        {
            if (!eventType.IsSet)
            {
                throw new ArgumentException("Handlers must declare a stable authored event type.", nameof(eventType));
            }

            EventType = eventType;
        }

        public AuthoredId EventType { get; }

        public void Handle(IDomainEvent domainEvent, WorldState world, SimulationContext context)
        {
            if (domainEvent is TEvent typed)
            {
                Handle(typed, world, context);
                return;
            }

            throw new InvalidOperationException(
                $"Handler for '{EventType}' expects '{typeof(TEvent).Name}' but received '{domainEvent?.GetType().Name ?? "null"}'.");
        }

        protected abstract void Handle(TEvent domainEvent, WorldState world, SimulationContext context);
    }
}
