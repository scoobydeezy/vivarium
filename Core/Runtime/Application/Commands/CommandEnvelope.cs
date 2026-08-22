using System;

namespace Vivarium.Application.Commands
{
    /// <summary>
    /// A command plus its deterministic ingress ordering (§2.2.1, §34).
    /// <para>
    /// <see cref="CommandSequence"/> is monotonic within the session and is <b>not</b> the scheduler's
    /// <c>EventSequence</c>: separate counters, separate scope, separate lifetime (invariant 18).
    /// </para>
    /// </summary>
    public sealed class CommandEnvelope
    {
        public CommandEnvelope(long commandSequence, ICommand command, string diagnostics = null)
        {
            CommandSequence = commandSequence;
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Diagnostics = diagnostics;
        }

        public long CommandSequence { get; }

        public ICommand Command { get; }

        /// <summary>
        /// Optional input metadata for diagnostics — which control was clicked, which device. Never
        /// authoritative: the Domain understands no mouse buttons or screen coordinates (§46).
        /// </summary>
        public string Diagnostics { get; }

        public override string ToString() => $"#{CommandSequence} {Command.GetType().Name}";
    }
}
