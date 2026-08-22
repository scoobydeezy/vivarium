using System;
using System.Collections.Generic;

namespace Vivarium.Domain.Events
{
    /// <summary>
    /// The internal reaction queue drained by the settlement loop (§12.1).
    /// <para>
    /// FIFO by publication order. Handlers may publish further Domain Events, and those reactions are
    /// processed as part of the <i>current</i> settlement cycle — which is why the runaway guard in
    /// §11.4 covers Domain Event reactions as well as scheduled work.
    /// </para>
    /// <para>
    /// This is an internal simulation mechanism, never an externally writable global bus (§12.1).
    /// External writes come in as commands (§2.2).
    /// </para>
    /// </summary>
    public sealed class DomainEventQueue
    {
        private readonly Queue<IDomainEvent> _pending = new Queue<IDomainEvent>();

        public int PendingCount => _pending.Count;

        public bool HasPending => _pending.Count > 0;

        public void Publish(IDomainEvent domainEvent)
        {
            if (domainEvent == null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            _pending.Enqueue(domainEvent);
        }

        public bool TryDequeue(out IDomainEvent domainEvent)
        {
            if (_pending.Count == 0)
            {
                domainEvent = null;
                return false;
            }

            domainEvent = _pending.Dequeue();
            return true;
        }

        public void Clear() => _pending.Clear();
    }
}
