using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Events
{
    /// <summary>
    /// Explicitly ordered handler chains per Domain Event type (§12.1).
    /// <para>
    /// Handler order is registered with an explicit numeric order and must <b>never</b> depend on
    /// incidental subscription order, assembly load order, reflection enumeration, or content load
    /// order. Ties fall back to registration index only so that adding a handler cannot silently
    /// reorder existing ones.
    /// </para>
    /// </summary>
    public sealed class OrderedDomainEventHandlerRegistry
    {
        private readonly struct Registration
        {
            public Registration(int order, int index, IDomainEventHandler handler)
            {
                Order = order;
                Index = index;
                Handler = handler;
            }

            public int Order { get; }

            public int Index { get; }

            public IDomainEventHandler Handler { get; }
        }

        private static readonly IDomainEventHandler[] NoHandlers = new IDomainEventHandler[0];

        private readonly Dictionary<AuthoredId, List<Registration>> _registrations =
            new Dictionary<AuthoredId, List<Registration>>();

        private readonly Dictionary<AuthoredId, IDomainEventHandler[]> _resolved =
            new Dictionary<AuthoredId, IDomainEventHandler[]>();

        private int _registrationCounter;

        /// <summary>
        /// Registers a handler at an explicit position in the chain. Lower <paramref name="order"/>
        /// runs first. Use spaced values (100, 200, 300) so a handler can later be inserted between
        /// two existing ones without renumbering.
        /// </summary>
        public void Register(IDomainEventHandler handler, int order)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_registrations.TryGetValue(handler.EventType, out List<Registration> chain))
            {
                chain = new List<Registration>();
                _registrations.Add(handler.EventType, chain);
            }

            foreach (Registration existing in chain)
            {
                if (existing.Order == order)
                {
                    throw new InvalidOperationException(
                        $"Handler order {order} is already taken for event '{handler.EventType}' by {existing.Handler.GetType().Name}. Orders must be explicit and unique so the chain is unambiguous (§12.1).");
                }
            }

            chain.Add(new Registration(order, _registrationCounter++, handler));
            _resolved.Remove(handler.EventType);
        }

        /// <summary>The handler chain for an event type, in stable execution order. Empty if none registered.</summary>
        public IReadOnlyList<IDomainEventHandler> HandlersFor(AuthoredId eventType)
        {
            if (_resolved.TryGetValue(eventType, out IDomainEventHandler[] cached))
            {
                return cached;
            }

            if (!_registrations.TryGetValue(eventType, out List<Registration> chain))
            {
                return NoHandlers;
            }

            chain.Sort((a, b) =>
            {
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : a.Index.CompareTo(b.Index);
            });

            var ordered = new IDomainEventHandler[chain.Count];
            for (int i = 0; i < chain.Count; i++)
            {
                ordered[i] = chain[i].Handler;
            }

            _resolved[eventType] = ordered;
            return ordered;
        }
    }
}
