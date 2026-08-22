using System;
using System.Collections.Generic;

namespace Vivarium.Application.Commands
{
    /// <summary>
    /// The deterministic command ingress queue owned by the session layer (§2.2.1).
    /// <para>
    /// Commands are stamped with a monotonic <c>CommandSequence</c> on entry and executed strictly in
    /// that order, one at a time, at quiescent simulation boundaries. A command never interleaves
    /// authoritative mutation with another external command (invariant 3).
    /// </para>
    /// </summary>
    public sealed class CommandQueue
    {
        private readonly Queue<CommandEnvelope> _pending = new Queue<CommandEnvelope>();
        private long _issued;

        public CommandQueue(long alreadyIssued = 0)
        {
            if (alreadyIssued < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alreadyIssued));
            }

            _issued = alreadyIssued;
        }

        /// <summary>The last sequence issued. Persisted so a reloaded session keeps counting up.</summary>
        public long LastIssuedSequence => _issued;

        public int PendingCount => _pending.Count;

        public bool HasPending => _pending.Count > 0;

        /// <summary>Accepts a command into the queue and assigns its ingress order.</summary>
        public CommandEnvelope Enqueue(ICommand command, string diagnostics = null)
        {
            var envelope = new CommandEnvelope(++_issued, command, diagnostics);
            _pending.Enqueue(envelope);
            return envelope;
        }

        public bool TryDequeue(out CommandEnvelope envelope)
        {
            if (_pending.Count == 0)
            {
                envelope = null;
                return false;
            }

            envelope = _pending.Dequeue();
            return true;
        }
    }
}
