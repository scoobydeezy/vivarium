using System;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Payload for a decision reaching its resolution time (§17).</summary>
    public sealed class DecisionResolvePayload : IScheduledEventPayload
    {
        public DecisionResolvePayload(DecisionId decisionId, CharacterId characterId)
        {
            DecisionId = decisionId;
            CharacterId = characterId;
        }

        public DecisionId DecisionId { get; }

        public CharacterId CharacterId { get; }
    }

    public sealed class DecisionPendingCommitPayload : IScheduledEventPayload
    {
        public DecisionPendingCommitPayload(DecisionId decisionId, CharacterId characterId)
        { DecisionId = decisionId; CharacterId = characterId; }
        public DecisionId DecisionId { get; }
        public CharacterId CharacterId { get; }
    }

    public static class DecisionResolutionCommitter
    {
        public static void Commit(WorldState world, Decision decision, DecisionResolution resolution, SimulationContext context)
        {
            if (decision.PendingResolution != null)
                world.Scheduler.Cancel(decision.PendingResolution.ExpiryEventId);
            decision.Resolve(resolution);
            world.Attention.Release(decision.Id);
            world.Attention.SetPolicy(decision.Id, AttentionPolicy.Normal);
            world.DecisionDependencies.Unregister(decision.Id);
            world.CommitmentConflicts.Unregister(decision.Id);
            HistoryEntry history = world.HistoryLedger.Record(
                new AuthoredId("history.decision_resolved"), world.Clock.Now, RetentionTier.Significant,
                $"{decision.DefinitionId} → {resolution.ChosenOptionId} ({resolution.Degree})",
                new[] { decision.CharacterId.ToRef(), decision.Id.ToRef() });
            decision.LinkResolutionHistory(history.Id);
            world.Publish(new DecisionResolvedEvent(decision.Id, decision.CharacterId, resolution));
        }
    }

    /// <summary>
    /// Resolves a Decision when its time comes — unless the player is holding it and the current mode
    /// permits holding (§20, §21).
    /// <para>
    /// A held decision does not freeze the rest of the world (invariant 34); it simply defers its own
    /// resolution. During offline catch-up, holding is not available, so decisions resolve according to
    /// explicit policy rather than piling up while the player is away.
    /// </para>
    /// </summary>
    public sealed class DecisionResolveHandler : ScheduledEventHandler<DecisionResolvePayload>
    {
        private readonly DecisionResolutionService _resolution;
        private readonly DecisionHoldPolicy _holdPolicy;

        public DecisionResolveHandler(DecisionResolutionService resolution, DecisionHoldPolicy holdPolicy)
            : base(Activities.ScheduledEventTypes.DecisionResolve)
        {
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
            _holdPolicy = holdPolicy ?? throw new ArgumentNullException(nameof(holdPolicy));
        }

        protected override bool CanExecute(WorldState world, DecisionResolvePayload payload) =>
            world.Decisions.TryGet(payload.DecisionId, out Decision decision) && decision.IsActive;

        protected override void Execute(WorldState world, DecisionResolvePayload payload, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(payload.DecisionId);

            bool held = world.Attention.IsHeld(decision.Id)
                && world.Attention.PolicyFor(decision.Id) == AttentionPolicy.Hold
                && context.AllowsHeldDecisions;

            if (held && !HoldCapacityExceeded(world, decision))
            {
                // Stay open. Re-arming is the caller's job when the player releases it, so a held
                // decision consumes no further scheduler work.
                if (context.Trace.IsEnabled)
                {
                    context.Trace.Record("decision", $"{world.Clock.Now} {decision.Id} remains held");
                }

                return;
            }

            DecisionResolution resolution = decision.IsAwaitingCommit
                ? _resolution.Complete(decision, decision.PendingResolution.AcceptedRolls, world.Clock.Now,
                    decision.PendingResolution.SupersededRolls)
                : _resolution.Resolve(decision, context);
            DecisionResolutionCommitter.Commit(world, decision, resolution, context);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record(
                    "decision",
                    $"{world.Clock.Now} resolved {decision.Id} → {resolution.ChosenOptionId} ({resolution.Degree}); seed {world.WorldSeed}, content {context.ContentVersion}, rules {context.SimulationRulesVersion}, rng {context.RandomAlgorithmVersion}");
            }

        }

        /// <summary>
        /// Whether this character is over their held-decision allowance, counting every one of their
        /// concurrent decisions (§17.1, §20).
        /// </summary>
        private bool HoldCapacityExceeded(WorldState world, Decision decision)
        {
            if (_holdPolicy.GlobalCapacityExceeded(world.Attention.HeldCount))
            {
                return true;
            }

            int heldForCharacter = 0;
            foreach (DecisionId heldId in world.Attention.HeldDecisions)
            {
                if (world.Decisions.TryGet(heldId, out Decision held) && held.CharacterId == decision.CharacterId)
                {
                    heldForCharacter++;
                }
            }

            return _holdPolicy.CharacterCapacityExceeded(heldForCharacter);
        }
    }


    /// <summary>Commits frozen rolls when the authored Re-roll window expires.</summary>
    public sealed class DecisionPendingCommitHandler : ScheduledEventHandler<DecisionPendingCommitPayload>
    {
        private readonly DecisionResolutionService _resolution;
        public DecisionPendingCommitHandler(DecisionResolutionService resolution)
            : base(Activities.ScheduledEventTypes.DecisionPendingCommit) => _resolution = resolution;
        protected override bool CanExecute(WorldState world, DecisionPendingCommitPayload payload) =>
            world.Decisions.TryGet(payload.DecisionId, out Decision decision) && decision.IsAwaitingCommit;
        protected override void Execute(WorldState world, DecisionPendingCommitPayload payload, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(payload.DecisionId);
            DecisionResolution result = _resolution.Complete(decision, decision.PendingResolution.AcceptedRolls, world.Clock.Now,
                decision.PendingResolution.SupersededRolls);
            DecisionResolutionCommitter.Commit(world, decision, result, context);
        }
    }

    /// <summary>A hard feasibility deadline. Holding never permits this Decision to cross it.</summary>
    public sealed class CommitmentConflictAutoResolveHandler : ScheduledEventHandler<DecisionResolvePayload>
    {
        private readonly DecisionResolutionService _resolution;
        public CommitmentConflictAutoResolveHandler(DecisionResolutionService resolution)
            : base(Activities.ScheduledEventTypes.AutoResolveCommitmentConflict) =>
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));

        protected override bool CanExecute(WorldState world, DecisionResolvePayload payload) =>
            world.Decisions.TryGet(payload.DecisionId, out Decision decision) && decision.IsActive &&
            decision.CommitmentConflictKey != null;

        protected override void Execute(WorldState world, DecisionResolvePayload payload, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(payload.DecisionId);
            DecisionResolution resolution = decision.IsAwaitingCommit
                ? _resolution.Complete(decision, decision.PendingResolution.AcceptedRolls, world.Clock.Now,
                    decision.PendingResolution.SupersededRolls)
                : _resolution.Resolve(decision, context);
            DecisionResolutionCommitter.Commit(world, decision, resolution, context);
        }
    }
}
