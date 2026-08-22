using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// Maps stable authored event types to handlers (§11.3).
    /// <para>
    /// Registration is explicit. Nothing here reflects over assemblies or auto-discovers handlers,
    /// because assembly load order and reflection enumeration order are exactly the kind of incidental
    /// ordering the architecture forbids (§12.1).
    /// </para>
    /// </summary>
    public sealed class ScheduledEventHandlerRegistry
    {
        private readonly Dictionary<AuthoredId, IScheduledEventHandler> _handlers =
            new Dictionary<AuthoredId, IScheduledEventHandler>();

        public void Register(IScheduledEventHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.ContainsKey(handler.EventType))
            {
                throw new InvalidOperationException($"A handler is already registered for event type '{handler.EventType}'.");
            }

            _handlers.Add(handler.EventType, handler);
        }

        public bool TryResolve(AuthoredId eventType, out IScheduledEventHandler handler) =>
            _handlers.TryGetValue(eventType, out handler);

        public IScheduledEventHandler Resolve(AuthoredId eventType) =>
            _handlers.TryGetValue(eventType, out IScheduledEventHandler handler)
                ? handler
                : throw new KeyNotFoundException($"No handler registered for event type '{eventType}'. Registration happens in the composition root (§47).");

        public int Count => _handlers.Count;
    }
}
