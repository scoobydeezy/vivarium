using System;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Commands
{
    /// <summary>
    /// Marker for an external write request (§2.2).
    /// <para>
    /// <b>Commands are the only external write door.</b> Player, UI, and platform requests all enter
    /// this way. That is not the same as saying every internal state change must be a command —
    /// simulation systems, Domain Event handlers, and scheduled-event handlers mutate state directly as
    /// part of authoritative execution.
    /// </para>
    /// </summary>
    public interface ICommand
    {
    }

    /// <summary>A command returning <typeparamref name="TResult"/>.</summary>
    public interface ICommand<TResult> : ICommand
    {
    }

    /// <summary>
    /// What a handler gets: the simulation context plus the ingress sequence of the command being
    /// executed (§2.2.1). The sequence is recorded on anything the command produces, so a trace can tie
    /// world changes back to the exact input that caused them (§53).
    /// </summary>
    public sealed class CommandContext
    {
        public CommandContext(SimulationContext simulation, long commandSequence)
        {
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            CommandSequence = commandSequence;
        }

        public SimulationContext Simulation { get; }

        public WorldState World => Simulation.World;

        /// <summary>Monotonic external ingress order. Distinct from the scheduler's EventSequence (§34).</summary>
        public long CommandSequence { get; }
    }

    /// <summary>Non-generic handler face, so the dispatcher can hold a heterogeneous registry.</summary>
    public interface ICommandHandler
    {
        Type CommandType { get; }

        object HandleUntyped(ICommand command, CommandContext context);
    }

    /// <summary>Typed base for command handlers.</summary>
    public abstract class CommandHandler<TCommand, TResult> : ICommandHandler
        where TCommand : ICommand<TResult>
    {
        public Type CommandType => typeof(TCommand);

        public object HandleUntyped(ICommand command, CommandContext context)
        {
            if (command is TCommand typed)
            {
                return Handle(typed, context);
            }

            throw new InvalidOperationException(
                $"Handler for {typeof(TCommand).Name} received {command?.GetType().Name ?? "null"}.");
        }

        public abstract TResult Handle(TCommand command, CommandContext context);
    }

    /// <summary>
    /// Routes a command to its handler (§34).
    /// <para>
    /// A simple in-house dispatcher on purpose. A mediator or DI framework should not be installed
    /// merely because one may eventually be useful — the convention is architectural, the
    /// implementation can evolve.
    /// </para>
    /// </summary>
    public interface ICommandDispatcher
    {
        TResult Dispatch<TResult>(ICommand<TResult> command, CommandContext context);
    }
}
