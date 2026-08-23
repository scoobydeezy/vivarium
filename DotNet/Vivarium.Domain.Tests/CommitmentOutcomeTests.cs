using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class CommitmentOutcomeTests
    {
        [Fact]
        public void TerminalTransitionAllocatesExactlyOneImmutableOutcome()
        {
            var world = new WorldState(7, new SimTime(20));
            Commitment commitment = Planned(world);
            var lifecycle = new CommitmentLifecycleService();

            lifecycle.Start(world, commitment, new ActivityInstanceId(4));
            CommitmentOutcome outcome = lifecycle.Fulfill(world, commitment);
            int nextAfterOutcome = world.RuntimeIds.Snapshot().CommitmentOutcomes;

            Assert.Equal(new CommitmentOutcomeId(1), outcome.Id);
            Assert.Equal(CommitmentStatus.Active, outcome.PreviousStatus);
            Assert.Equal(CommitmentStatus.Fulfilled, outcome.NewStatus);
            Assert.Equal(CommitmentOutcomeCauseKind.None, outcome.Cause.Kind);
            Assert.Same(outcome, Assert.Single(world.CommitmentOutcomes.All));
            Assert.Throws<InvalidOperationException>(() => lifecycle.Fulfill(world, commitment));
            Assert.Equal(nextAfterOutcome, world.RuntimeIds.Snapshot().CommitmentOutcomes);
            Assert.Same(outcome, Assert.Single(world.CommitmentOutcomes.All));
        }

        [Fact]
        public void OutcomeCausePairingsAndExternalNonAttributionAreEnforced()
        {
            var world = new WorldState(7, new SimTime(0));
            Commitment commitment = Planned(world);
            var lifecycle = new CommitmentLifecycleService();

            Assert.Throws<ArgumentException>(() => lifecycle.Cancel(
                world, commitment, CommitmentOutcomeCauseKind.ConflictResolution));
            CommitmentOutcome cancelled = lifecycle.Cancel(
                world, commitment, CommitmentOutcomeCauseKind.ExternalCancellation);
            KnownCommitmentAttribution attribution = CommitmentAttributionMapper.Observe(cancelled);

            Assert.Equal(PerceivedCommitmentCause.NotAttributedToActor, attribution.PerceivedCause);
            Assert.False(attribution.ActorAccountable);
        }

        [Fact]
        public void AccountabilityPolicyUsesMostSpecificApplicableRule()
        {
            var fallback = new CommitmentConsequenceSet(evidenceActionId: new AuthoredId("evidence.default"));
            var outcomeRule = new CommitmentConsequenceSet(evidenceActionId: new AuthoredId("evidence.outcome"));
            var roleRule = new CommitmentConsequenceSet(evidenceActionId: new AuthoredId("evidence.role"));
            var roleOverride = new CommitmentConsequenceSet(evidenceActionId: new AuthoredId("evidence.override"));
            var causeOverride = new CommitmentConsequenceSet(evidenceActionId: new AuthoredId("evidence.cause"));
            var policy = new CommitmentAccountabilityPolicy(
                fallback,
                new Dictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>
                {
                    [CommitmentOutcomeKind.Relinquished] = outcomeRule,
                },
                new Dictionary<StakeholderRole, CommitmentConsequenceSet>
                {
                    [StakeholderRole.Counterparty] = roleRule,
                },
                new[]
                {
                    new CommitmentAccountabilityOverride(
                        CommitmentOutcomeKind.Relinquished, StakeholderRole.Counterparty, roleOverride),
                    new CommitmentAccountabilityOverride(
                        CommitmentOutcomeKind.Relinquished, StakeholderRole.Counterparty, causeOverride,
                        PerceivedCommitmentCause.RelinquishedByActor),
                });
            var attribution = new KnownCommitmentAttribution(
                CommitmentOutcomeKind.Relinquished,
                PerceivedCommitmentCause.RelinquishedByActor,
                new SimTime(0),
                new CommitmentOutcomeId(1),
                true);

            Assert.Same(causeOverride, policy.Resolve(attribution, StakeholderRole.Counterparty));
            var unknown = new KnownCommitmentAttribution(
                CommitmentOutcomeKind.Relinquished,
                PerceivedCommitmentCause.Unknown,
                new SimTime(0),
                new CommitmentOutcomeId(1),
                true);
            Assert.Same(roleOverride, policy.Resolve(unknown, StakeholderRole.Counterparty));
        }

        private static Commitment Planned(WorldState world)
        {
            var commitment = new Commitment(
                world.RuntimeIds.Commitments.Next(),
                new CharacterId(1),
                new AuthoredId("commitment.test"),
                new SimTime(0),
                new SimTime(10),
                SimDuration.FromMinutes(5),
                new LocationId(1),
                1);
            world.Commitments.Add(commitment.Id, commitment);
            return commitment;
        }
    }
}
