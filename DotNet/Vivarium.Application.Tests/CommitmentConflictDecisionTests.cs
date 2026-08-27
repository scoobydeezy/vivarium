using System.Linq;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.History;
using Vivarium.Domain.Time;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Simulation;
using Vivarium.Infrastructure.Bootstrap;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class CommitmentConflictDecisionTests
    {
        [Fact]
        public void ScheduleChangeGeneratesOnePlanValuedDecisionAndDeduplicatesIt()
        {
            TestWorld fixture = TestWorld.Create();
            (Commitment first, Commitment second) = AddConflict(fixture);

            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision decision = fixture.Host.World.Decisions.All.Single(d => d.CommitmentConflictKey != null);

            Assert.Equal(2, decision.Options.Count);
            Assert.Equal(first.Id, decision.Options[0].CommitmentResolutionPlan.Preserve.Single());
            Assert.Equal(second.Id, decision.Options[0].CommitmentResolutionPlan.Relinquish.Single());
            Assert.Equal(2, decision.Influences.Count(i => i.OptionId == decision.Options[0].Id));
            Assert.Equal(2, decision.Influences.Where(i => i.OptionId == decision.Options[0].Id)
                .Select(i => i.Subject).Distinct().Count());
            Assert.Equal(new SimTime(fixture.Host.World.Clock.Now.TotalMinutes + 48), decision.LatestResolutionAt);

            PublishScheduleChange(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.Single(fixture.Host.World.Decisions.All, d => d.CommitmentConflictKey != null);
        }

        [Fact]
        public void TimelineUsesAuthoritativeTravelFeasibilityConflictNotOnlyClockOverlap()
        {
            TestWorld fixture = TestWorld.Create();
            long now = fixture.Host.World.Clock.Now.TotalMinutes;
            var atHome = new Commitment(
                fixture.Host.World.RuntimeIds.Commitments.Next(), fixture.Mina,
                new AuthoredId("commitment.at_home"),
                new SimTime(now + 60), new SimTime(now + 60),
                SimDuration.FromMinutes(30), fixture.Home, 4);
            var atBakery = new Commitment(
                fixture.Host.World.RuntimeIds.Commitments.Next(), fixture.Mina,
                new AuthoredId("commitment.at_bakery"),
                new SimTime(now + 95), new SimTime(now + 95),
                SimDuration.FromMinutes(30), fixture.Bakery, 6);
            fixture.Host.World.Commitments.Add(atHome.Id, atHome);
            fixture.Host.World.Commitments.Add(atBakery.Id, atBakery);
            PublishScheduleChange(fixture);

            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.False(atHome.OverlapsWindowOf(atBakery));
            Assert.Single(fixture.Host.World.Decisions.All, d => d.CommitmentConflictKey != null && d.IsActive);
            ScheduleView timeline = new ScheduleProjector().Project(fixture.Host.World, fixture.Mina);
            Assert.Equal(2, timeline.ConflictCount);
            Assert.All(timeline.Entries, entry => Assert.True(entry.Conflicts));
            Assert.Contains(atBakery.Id.Value, timeline.Entries[0].ConflictingCommitmentIds);
            Assert.Contains(atHome.Id.Value, timeline.Entries[1].ConflictingCommitmentIds);
        }

        [Fact]
        public void HeldConflictStillResolvesAtItsHardDeadlineAndRelinquishesOnlyTheSacrificedIntent()
        {
            TestWorld fixture = TestWorld.Create();
            (Commitment first, Commitment second) = AddConflict(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision decision = fixture.Host.World.Decisions.All.Single(d => d.CommitmentConflictKey != null);
            // A benign schedule revision keeps the same conflict epoch but refreshes deadline dependencies.
            PublishScheduleChange(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);
            Assert.True(fixture.Host.Session.Execute(new ReleaseDecisionCommand(decision.Id)).IsSuccess);
            Assert.Single(fixture.Host.World.Scheduler.PendingEvents,
                e => e.EventType == ScheduledEventTypes.AutoResolveCommitmentConflict);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(48));

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.False(fixture.Host.World.Attention.IsHeld(decision.Id));
            DecisionOption chosen = decision.Options.Single(o => o.Id == decision.Resolution.ChosenOptionId);
            CommitmentId preserved = chosen.CommitmentResolutionPlan.Preserve.Single();
            CommitmentId relinquished = chosen.CommitmentResolutionPlan.Relinquish.Single();
            Assert.Equal(CommitmentStatus.Planned, fixture.Host.World.Commitments.Get(preserved).Status);
            Assert.Equal(CommitmentStatus.Relinquished, fixture.Host.World.Commitments.Get(relinquished).Status);
            Assert.Contains(first.Id, new[] { preserved, relinquished });
            Assert.Contains(second.Id, new[] { preserved, relinquished });
        }

        [Fact]
        public void InvalidatedHeldDecisionDissolvesRefundsItsInterventionAndCreatesEphemeralRecap()
        {
            TestWorld fixture = TestWorld.Create();
            (_, Commitment second) = AddConflict(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision decision = fixture.Host.World.Decisions.All.Single(d => d.CommitmentConflictKey != null);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);
            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionStepUp, decision.Influences[0].Id)).IsSuccess);
            Assert.Equal(2, fixture.Host.World.Nudges.Balance);

            new CommitmentLifecycleService().Cancel(
                fixture.Host.World,
                second,
                CommitmentOutcomeCauseKind.ExternalCancellation);
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(DecisionStatus.Dissolved, decision.Status);
            Assert.False(fixture.Host.World.Attention.IsHeld(decision.Id));
            Assert.Equal(3, fixture.Host.World.Nudges.Balance);
            HistoryEntry recap = fixture.Host.World.HistoryLedger.Entries
                .Single(e => e.Kind == DecisionDissolvedHistoryHandler.HistoryKind);
            Assert.Equal(RetentionTier.Ephemeral, recap.Tier);
            Assert.Contains("refunded 1", recap.Summary);
        }

        [Fact]
        public void ActiveConflictPlanDeadlineAndDerivedIndexRoundTripThenResolveIdentically()
        {
            TestWorld fixture = TestWorld.Create();
            AddConflict(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision originalDecision = fixture.Host.World.Decisions.All.Single(d => d.CommitmentConflictKey != null);
            SaveGameData save = fixture.Host.Session.Save("commitment-conflict");

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, save.LastCommandSequence, 1, null, fixture.Store, fixture.Clock);
            Decision restoredDecision = restored.World.Decisions.Get(originalDecision.Id);

            Assert.Equal(originalDecision.LatestResolutionAt, restoredDecision.LatestResolutionAt);
            Assert.Equal(originalDecision.Options[0].CommitmentResolutionPlan.Preserve,
                restoredDecision.Options[0].CommitmentResolutionPlan.Preserve);
            Assert.True(restored.World.CommitmentConflicts.TryFindByParticipants(
                fixture.Mina, restoredDecision.CommitmentConflictKey.ParticipatingCommitmentIds, out DecisionId indexed));
            Assert.Equal(restoredDecision.Id, indexed);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(48));
            restored.Session.Advance(SimDuration.FromMinutes(48));
            Assert.Equal(originalDecision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Equal(originalDecision.Resolution.Rolls.Select(r => r.Rolled),
                restoredDecision.Resolution.Rolls.Select(r => r.Rolled));
        }

        private static (Commitment, Commitment) AddConflict(TestWorld fixture)
        {
            long now = fixture.Host.World.Clock.Now.TotalMinutes;
            var first = new Commitment(fixture.Host.World.RuntimeIds.Commitments.Next(), fixture.Mina,
                new AuthoredId("commitment.dinner"), new SimTime(now + 60), new SimTime(now + 60),
                SimDuration.FromMinutes(60), fixture.Home, 4);
            var second = new Commitment(fixture.Host.World.RuntimeIds.Commitments.Next(), fixture.Mina,
                new AuthoredId("commitment.rehearsal"), new SimTime(now + 60), new SimTime(now + 60),
                SimDuration.FromMinutes(60), fixture.Bakery, 6);
            fixture.Host.World.Commitments.Add(first.Id, first);
            fixture.Host.World.Commitments.Add(second.Id, second);
            PublishScheduleChange(fixture);
            return (first, second);
        }

        private static void PublishScheduleChange(TestWorld fixture)
        {
            int revision = fixture.Host.World.BumpRevision(
                new RevisionKey(fixture.Mina.ToRef(), RevisionAspects.Schedule));
            fixture.Host.World.Publish(new CommitmentScheduleChangedEvent(fixture.Mina, revision));
        }
    }
}
