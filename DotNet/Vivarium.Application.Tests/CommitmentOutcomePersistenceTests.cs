using System.Collections.Generic;
using System.Linq;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class CommitmentOutcomePersistenceTests
    {
        [Fact]
        public void CommitmentSnapshotsStakeholdersAndAccountabilityPolicyAcrossLoad()
        {
            TestWorld fixture = TestWorld.Create();
            var consequences = new CommitmentConsequenceSet(
                new CommitmentMemoryConsequence(
                    new AuthoredId("memory.kept"),
                    new AuthoredId("explanation.kept"),
                    RetentionTier.Legacy),
                new AuthoredId("evidence.kept"),
                new Dictionary<AuthoredId, long>
                {
                    [RelationshipChannels.Resentment] = 321,
                });
            var policy = new CommitmentAccountabilityPolicy(
                byOutcome: new Dictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>
                {
                    [CommitmentOutcomeKind.Relinquished] = consequences,
                },
                id: new AuthoredId("accountability.kept"));
            var commitment = new Commitment(
                fixture.Host.World.RuntimeIds.Commitments.Next(),
                fixture.Mina,
                new AuthoredId("commitment.snapshotted"),
                fixture.Host.World.Clock.Now,
                fixture.Host.World.Clock.Now.Plus(SimDuration.FromMinutes(5)),
                SimDuration.FromMinutes(10),
                fixture.Home,
                4,
                stakeholders: new[]
                {
                    new StakeholderRef(new CharacterId(99).ToRef(), StakeholderRole.Authority),
                },
                accountabilityPolicy: policy);
            fixture.Host.World.Commitments.Add(commitment.Id, commitment);

            SaveGameData save = fixture.Host.Session.Save("commitment-policy");
            WorldState restored = fixture.Host.SaveMapper.Restore(save);
            Commitment copy = restored.Commitments.Get(commitment.Id);

            Assert.Equal(policy.Id, copy.AccountabilityPolicy.Id);
            StakeholderRef stakeholder = Assert.Single(copy.Stakeholders);
            Assert.Equal(StakeholderRole.Authority, stakeholder.Role);
            CommitmentConsequenceSet restoredConsequences = copy.AccountabilityPolicy.ByOutcome[CommitmentOutcomeKind.Relinquished];
            Assert.Equal(RetentionTier.Legacy, restoredConsequences.Memory.RetentionTier);
            Assert.Equal(321, restoredConsequences.ChannelDeltas[RelationshipChannels.Resentment]);
        }

        [Fact]
        public void PendingWindowExpirationRoundTripsAndProducesOneMissedOutcome()
        {
            TestWorld fixture = TestWorld.Create();
            SimTime latest = fixture.Host.World.Clock.Now.Plus(SimDuration.FromMinutes(5));
            var commitment = new Commitment(
                fixture.Host.World.RuntimeIds.Commitments.Next(),
                fixture.Mina,
                new AuthoredId("commitment.expires"),
                fixture.Host.World.Clock.Now,
                latest,
                SimDuration.FromMinutes(10),
                fixture.Home,
                4);
            fixture.Host.World.Commitments.Add(commitment.Id, commitment);
            fixture.Host.World.Scheduler.Schedule(
                latest.Plus(SimDuration.FromMinutes(1)),
                SchedulePhase.Expiration,
                ScheduledEventTypes.CommitmentWindowExpired,
                new CommitmentWindowExpiredPayload(commitment.Id, fixture.Mina));

            SaveGameData save = fixture.Host.Session.Save("commitment-expiration");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, save.LastCommandSequence, 1,
                null, fixture.Store, fixture.Clock);
            restored.Session.Advance(SimDuration.FromMinutes(6));

            Assert.Equal(CommitmentStatus.Missed, restored.World.Commitments.Get(commitment.Id).Status);
            CommitmentOutcome outcome = Assert.Single(restored.World.CommitmentOutcomes.All);
            Assert.Equal(CommitmentOutcomeKind.Missed, outcome.Outcome);
            Assert.Equal(CommitmentOutcomeCauseKind.WindowExpired, outcome.Cause.Kind);
            Assert.Equal(1, restored.World.RuntimeIds.Snapshot().CommitmentOutcomes);
        }
    }
}
