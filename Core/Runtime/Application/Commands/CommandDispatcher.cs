using System;
using System.Collections.Generic;

namespace Vivarium.Application.Commands
{
    /// <summary>
    /// In-house command dispatcher (§34).
    /// <para>
    /// Explicit registration, no reflection scanning: the set of legal external writes should be
    /// something you can read off the composition root (§47).
    /// </para>
    /// </summary>
    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly Dictionary<Type, ICommandHandler> _handlers = new Dictionary<Type, ICommandHandler>();

        public void Register(ICommandHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.ContainsKey(handler.CommandType))
            {
                throw new InvalidOperationException($"A handler is already registered for {handler.CommandType.Name}.");
            }

            _handlers.Add(handler.CommandType, handler);
        }

        public bool CanDispatch(Type commandType) => _handlers.ContainsKey(commandType);

        public TResult Dispatch<TResult>(ICommand<TResult> command, CommandContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Type type = command.GetType();
            if (!_handlers.TryGetValue(type, out ICommandHandler handler))
            {
                throw new KeyNotFoundException($"No handler registered for command {type.Name}. Registration happens in the composition root (§47).");
            }

            return (TResult)handler.HandleUntyped(command, context);
        }

        /// <summary>
        /// Dispatches an envelope whose result type is not known statically — the path the session pump
        /// uses when draining the queue.
        /// </summary>
        public object DispatchUntyped(ICommand command, CommandContext context)
        {
            Type type = command.GetType();
            if (!_handlers.TryGetValue(type, out ICommandHandler handler))
            {
                throw new KeyNotFoundException($"No handler registered for command {type.Name}.");
            }

            return handler.HandleUntyped(command, context);
        }
    }
}
