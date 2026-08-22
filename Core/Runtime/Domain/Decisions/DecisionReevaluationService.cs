using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Recomputes the world-derived influences of one decision type when a dependency changes (§17.2).
    /// <para>
    /// Content implements this. It may add, retract, or re-weight influences — <b>but must not touch
    /// definition-derived semantics</b>, which were snapshotted at construction (§42.1).
    /// </para>
    /// </summary>
    public interface IDecisionInfluenceReevaluator
    {
        AuthoredId DecisionDefinitionId { get; }

        void Reevaluate(WorldState world, Decision decision, DecisionDependencyKey changedKey, SimulationContext context);
    }

    /// <summary>
    /// Drives targeted reevaluation of open decisions (§17.2).
    /// <para>
    /// An open Decision is not a frozen snapshot: Mina is considering moving in with Darius, a better
    /// apartment opens beside her job, and <c>Good Location d6</c> becomes
    /// <c>Excellent Location d10</c> — before she has decided.
    /// </para>
    /// <para>
    /// Reached through <see cref="DecisionDependencyIndex"/>, so only decisions that registered an
    /// interest are touched. This runs as a deterministic Domain reaction, never as polling
    /// (invariant 38).
    /// </para>
    /// </summary>
    public sealed class DecisionReevaluationService
    {
        private readonly Dictionary<AuthoredId, IDecisionInfluenceReevaluator> _reevaluators =
            new Dictionary<AuthoredId, IDecisionInfluenceReevaluator>();

        public void Register(IDecisionInfluenceReevaluator reevaluator)
        {
            if (reevaluator == null)
            {
                throw new ArgumentNullException(nameof(reevaluator));
            }

            if (_reevaluators.ContainsKey(reevaluator.DecisionDefinitionId))
            {
                throw new InvalidOperationException($"A reevaluator is already registered for '{reevaluator.DecisionDefinitionId}'.");
            }

            _reevaluators.Add(reevaluator.DecisionDefinitionId, reevaluator);
        }

        /// <summary>
        /// Reevaluates every active decision registered against <paramref name="changedKey"/>.
        /// </summary>
        /// <returns>How many decisions actually changed.</returns>
        public int ReevaluateDependents(SimulationContext context, DecisionDependencyKey changedKey)
        {
            WorldState world = context.World;
            IReadOnlyCollection<DecisionId> dependents = world.DecisionDependencies.DecisionsDependingOn(changedKey);
            int changed = 0;

            // Ascending DecisionId — reevaluation order is deterministic (§15).
            foreach (DecisionId id in dependents)
            {
                if (!world.Decisions.TryGet(id, out Decision decision) || !decision.IsActive)
                {
                    continue;
                }

                if (!_reevaluators.TryGetValue(decision.DefinitionId, out IDecisionInfluenceReevaluator reevaluator))
                {
                    continue;
                }

                int before = decision.InfluenceRevision;
                reevaluator.Reevaluate(world, decision, changedKey, context);

                if (decision.InfluenceRevision == before)
                {
                    continue;
                }

                changed++;
                world.BumpRevision(decision.InfluenceRevisionKey);
                world.Publish(new DecisionInfluencesChangedEvent(decision.Id, decision.InfluenceRevision));

                if (context.Trace.IsEnabled)
                {
                    context.Trace.Record(
                        "decision-reevaluated",
                        $"{world.Clock.Now} {decision.Id} influences → revision {decision.InfluenceRevision} (trigger {changedKey})");
                }
            }

            return changed;
        }
    }
}
