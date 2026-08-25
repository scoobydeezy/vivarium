using System.Linq;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class NudgeEconomyTests
    {
        [Fact]
        public void NewWorldStartsAtCapAndProjectsNextEightHourBoundary()
        {
            TestWorld fixture = TestWorld.Create();

            NudgeEconomyView view = new NudgeEconomyProjector().Project(fixture.Host.World);

            Assert.Equal(3, view.Balance);
            Assert.Equal(3, view.Cap);
            Assert.Equal(SimTime.FromClockTime(0, 16, 0).ToString(), view.NextRegenerationAt);
            Assert.Single(
                fixture.Host.World.Scheduler.PendingEvents,
                e => e.EventType == PlayerAgencyScheduledEventTypes.NudgeRegeneration);
        }

        [Fact]
        public void ValidInterventionsSpendAndInsufficientBalanceDisablesProjectionAndMutatesNothing()
        {
            TestWorld fixture = TestWorld.Create();
            Decision first = fixture.CreateDecision();
            Decision second = fixture.CreateDecision();

            AssertSpend(fixture, first, 0, 2);
            AssertSpend(fixture, first, 1, 1);
            AssertSpend(fixture, first, 2, 0);

            DecisionInfluence target = second.Influences[0];
            Die before = target.CurrentDie;
            DecisionView view = new DecisionProjector(fixture.Catalog.Interventions)
                .Project(fixture.Host.World, second);
            InfluenceView influence = FindInfluence(view, target.Id.Value);
            InterventionAvailabilityView availability = Assert.Single(
                influence.Interventions, item => item.InterventionDefinitionId == TestWorld.InterventionStepUp.Value);

            Assert.True(influence.CanBeIntervenedOn); // another independently funded intervention remains available
            Assert.False(availability.IsAvailable);
            Assert.Equal(1, availability.Cost);
            Assert.Equal(InterventionResourceKind.Nudge.ToString(), availability.ResourceKind);
            Assert.Equal(DecisionInterventionRules.ReasonInsufficientNudges.Value, availability.UnavailableReason);

            Result failed = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                second.Id,
                TestWorld.InterventionStepUp,
                target.Id));

            Assert.True(failed.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonInsufficientNudges, failed.Reason);
            Assert.Equal(before, target.CurrentDie);
            Assert.Empty(second.Interventions);
            Assert.Equal(0, fixture.Host.World.Nudges.Balance);
        }

        [Fact]
        public void RegenerationUsesBoundariesAndDoesNotBankTimeAtCap()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            AssertSpend(fixture, decision, 0, 2);

            fixture.Host.Session.Advance(SimDuration.FromHours(8));
            Assert.Equal(3, fixture.Host.World.Nudges.Balance);

            fixture.Host.Session.Advance(SimDuration.FromHours(16));
            Assert.Equal(3, fixture.Host.World.Nudges.Balance);

            Decision later = fixture.CreateDecision();
            AssertSpend(fixture, later, 0, 2);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.Equal(2, fixture.Host.World.Nudges.Balance);
        }

        [Fact]
        public void SaveLoadAndOfflineCatchUpPreserveBalanceAndRegenerateIdentically()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            AssertSpend(fixture, decision, 0, 2);
            SaveGameData save = fixture.Host.Session.Save("nudge-offline");

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                save.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);

            AppliedIntervention restoredIntervention = Assert.Single(
                restored.World.Decisions.Get(decision.Id).Interventions);
            Assert.Equal(InterventionResourceKind.Nudge, restoredIntervention.ResourceKind);
            Assert.Equal(1, restoredIntervention.ResourceCost);
            Assert.Equal(2, restored.World.Nudges.Balance);

            SimDuration elapsed = SimDuration.FromHours(16);
            fixture.Host.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);
            restored.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);

            Assert.Equal(3, fixture.Host.World.Nudges.Balance);
            Assert.Equal(fixture.Host.World.Nudges.Balance, restored.World.Nudges.Balance);
            Assert.Equal(fixture.Host.World.Nudges.Revision, restored.World.Nudges.Revision);
            Assert.Equal(
                fixture.Host.World.Scheduler.PendingEvents.Single(
                    e => e.EventType == PlayerAgencyScheduledEventTypes.NudgeRegeneration).DueAt,
                restored.World.Scheduler.PendingEvents.Single(
                    e => e.EventType == PlayerAgencyScheduledEventTypes.NudgeRegeneration).DueAt);
        }

        [Fact]
        public void PerEventClampingMakesCoincidentRefundAndRegenerationOrderIndependent()
        {
            var refundFirst = new NudgeAccount(2);
            refundFirst.Refund(1);
            refundFirst.Regenerate();

            var regenerationFirst = new NudgeAccount(2);
            regenerationFirst.Regenerate();
            regenerationFirst.Refund(1);

            Assert.Equal(3, refundFirst.Balance);
            Assert.Equal(refundFirst.Balance, regenerationFirst.Balance);
        }

        private static void AssertSpend(TestWorld fixture, Decision decision, int influenceIndex, int expectedBalance)
        {
            Result result = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id,
                TestWorld.InterventionStepUp,
                decision.Influences[influenceIndex].Id));
            Assert.True(result.IsSuccess, result.ToString());
            Assert.Equal(expectedBalance, fixture.Host.World.Nudges.Balance);
        }

        private static InfluenceView FindInfluence(DecisionView view, int influenceId) =>
            view.Options.SelectMany(option => option.Influences).Single(influence => influence.InfluenceId == influenceId);
    }
}
