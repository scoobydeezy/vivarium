using System;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class CommitmentFeasibilityTests
    {
        [Fact]
        public void EvaluatesTheWholeSetInsteadOfAcceptingPairwiseCompatibility()
        {
            var world = new WorldState(7, new SimTime(0));
            var actor = new CharacterId(1);
            var location = new LocationId(1);
            Commitment a = CommitmentAt(1, actor, location);
            Commitment b = CommitmentAt(2, actor, location);
            Commitment c = CommitmentAt(3, actor, location);
            var service = new CommitmentFeasibilityService();

            Assert.True(service.Evaluate(world, actor, new[] { a, b }).IsJointlyFeasible);
            Assert.True(service.Evaluate(world, actor, new[] { a, c }).IsJointlyFeasible);
            Assert.True(service.Evaluate(world, actor, new[] { b, c }).IsJointlyFeasible);
            Assert.False(service.Evaluate(world, actor, new[] { a, b, c }).IsJointlyFeasible);
        }

        [Fact]
        public void ResolutionPlanCanonicalizesSetsAndRejectsOverlappingIntent()
        {
            var plan = new CommitmentResolutionPlan(
                new AuthoredId("plan.test"),
                new[] { new CommitmentId(3), new CommitmentId(1) },
                Array.Empty<CommitmentId>(),
                new[] { new CommitmentId(2) });

            Assert.Equal(new CommitmentId(1), plan.Preserve[0]);
            Assert.Equal(new CommitmentId(3), plan.Preserve[1]);
            Assert.Throws<ArgumentException>(() => new CommitmentResolutionPlan(
                new AuthoredId("plan.invalid"),
                new[] { new CommitmentId(1) },
                Array.Empty<CommitmentId>(),
                new[] { new CommitmentId(1) }));
        }

        private static Commitment CommitmentAt(int id, CharacterId actor, LocationId location) =>
            new Commitment(
                new CommitmentId(id), actor, new AuthoredId("commitment.test"),
                new SimTime(0), new SimTime(3), SimDuration.FromMinutes(2), location, 1);
    }
}
