using System;
using System.Collections.Generic;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Queries
{
    /// <summary>
    /// Publishes projections at quiescent boundaries (§13.1).
    /// <para>
    /// Presentation subscribes here rather than polling the world. Because this only ever fires at
    /// quiescence, Unity cannot observe an in-progress mutation — "Mina quit her job, but employment
    /// membership hasn't been removed yet" is simply not an externally reachable state (invariant 23).
    /// </para>
    /// <para>
    /// Subscribers run in registration order and must not mutate the world. They are readers.
    /// </para>
    /// </summary>
    public sealed class ProjectionPublisher : IQuiescenceObserver
    {
        private readonly List<Action<WorldState, SimulationContext>> _subscribers =
            new List<Action<WorldState, SimulationContext>>();

        /// <summary>Quiescent boundaries published so far, for diagnostics.</summary>
        public long PublishCount { get; private set; }

        public void Subscribe(Action<WorldState, SimulationContext> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            _subscribers.Add(subscriber);
        }

        public void Unsubscribe(Action<WorldState, SimulationContext> subscriber) => _subscribers.Remove(subscriber);

        public void OnQuiescence(WorldState world, SimulationContext context)
        {
            PublishCount++;

            for (int i = 0; i < _subscribers.Count; i++)
            {
                _subscribers[i](world, context);
            }
        }
    }
}
