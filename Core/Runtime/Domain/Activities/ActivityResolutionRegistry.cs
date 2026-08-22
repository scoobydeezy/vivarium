using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Explicit registry of resolution strategies and consequence handlers per activity definition (§29.6).
    /// <para>
    /// Registration is manual, like every other registry here: no reflection, no auto-discovery, so
    /// nothing about resolution depends on assembly load order (§12.1).
    /// </para>
    /// </summary>
    public sealed class ActivityResolutionRegistry
    {
        private static readonly IActivityConsequenceHandler[] NoConsequences = new IActivityConsequenceHandler[0];

        private readonly Dictionary<AuthoredId, IActivityResolutionStrategy> _strategies =
            new Dictionary<AuthoredId, IActivityResolutionStrategy>();

        private readonly Dictionary<AuthoredId, List<IActivityConsequenceHandler>> _consequences =
            new Dictionary<AuthoredId, List<IActivityConsequenceHandler>>();

        public void RegisterStrategy(IActivityResolutionStrategy strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (_strategies.ContainsKey(strategy.ActivityDefinitionId))
            {
                throw new InvalidOperationException($"A resolution strategy is already registered for '{strategy.ActivityDefinitionId}'.");
            }

            _strategies.Add(strategy.ActivityDefinitionId, strategy);
        }

        public void RegisterConsequence(IActivityConsequenceHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_consequences.TryGetValue(handler.ActivityDefinitionId, out List<IActivityConsequenceHandler> handlers))
            {
                handlers = new List<IActivityConsequenceHandler>();
                _consequences.Add(handler.ActivityDefinitionId, handlers);
            }

            handlers.Add(handler);
        }

        public bool TryGetStrategy(AuthoredId activityDefinitionId, out IActivityResolutionStrategy strategy) =>
            _strategies.TryGetValue(activityDefinitionId, out strategy);

        public IReadOnlyList<IActivityConsequenceHandler> ConsequencesFor(AuthoredId activityDefinitionId) =>
            _consequences.TryGetValue(activityDefinitionId, out List<IActivityConsequenceHandler> handlers)
                ? (IReadOnlyList<IActivityConsequenceHandler>)handlers
                : NoConsequences;

        /// <summary>
        /// Accepts a result and runs the consequence pipeline — the single funnel both automatic and
        /// player-provided outcomes pass through (§29.6, invariant 46).
        /// </summary>
        public void AcceptResult(
            WorldState world,
            ActivityInstance activity,
            ActivityPerformanceResult result,
            SimulationContext context)
        {
            activity.Complete(result, world.Clock.Now);

            IReadOnlyList<IActivityConsequenceHandler> handlers = ConsequencesFor(activity.DefinitionId);
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].Apply(world, activity, result, context);
            }

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record(
                    "activity-result",
                    $"{world.Clock.Now} {activity.Id} {activity.DefinitionId} → {result.Grade} ({result.Magnitude}) source {result.Source}");
            }

            world.Publish(new ActivityCompletedEvent(activity.CharacterId, activity.Id, result));
        }
    }
}
