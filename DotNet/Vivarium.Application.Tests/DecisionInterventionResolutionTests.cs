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
    public sealed class DecisionInterventionResolutionTests
    {
        [Fact]
        public void AuthoredFixedDieSubstitutionIsFrozenAndNormalResolutionStillChoosesTheWinner()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            DecisionInfluence target = decision.Influences[0];

            Result applied = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionLoadedTwenty, target.Id));
            Assert.True(applied.IsSuccess, applied.ToString());
            Assert.Equal(new Die(20, 20), target.CurrentDie);
            Assert.Equal(0, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReplacementDie).Value.Balance);

            SaveGameData save = fixture.Host.Session.Save("fixed-substitution");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, save.LastCommandSequence, 1, null, fixture.Store, fixture.Clock);
            decision = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(new Die(20, 20), decision.Influences[0].CurrentDie);

            restored.Session.Advance(SimDuration.FromHours(8));

            InfluenceRoll roll = decision.Resolution.Rolls.Single(item => item.InfluenceId == target.Id);
            Assert.Equal(20, roll.Rolled);
            Assert.True(roll.Die.IsFixed);
            Assert.Equal(TestWorld.OptionAccept, decision.Resolution.ChosenOptionId);
        }

        [Fact]
        public void KnownRollRerollsOnlyItsScopedStreamAndSurvivesSaveLoadWithDiscardedEvidence()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            DecisionInfluence target = decision.Influences[1];
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);
            Assert.True(fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id)).IsSuccess);
            InfluenceRoll initial = decision.PendingResolution.AcceptedRolls.Single(item => item.InfluenceId == target.Id);

            Result rerolled = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, target.Id));
            Assert.True(rerolled.IsSuccess, rerolled.ToString());
            InfluenceRoll accepted = decision.PendingResolution.AcceptedRolls.Single(item => item.InfluenceId == target.Id);
            Assert.Equal(initial.RollIndex + 1, accepted.RollIndex);
            Assert.Equal(initial.Rolled, Assert.Single(decision.PendingResolution.SupersededRolls).Rolled);
            Assert.All(decision.PendingResolution.AcceptedRolls.Where(item => item.InfluenceId != target.Id),
                item => Assert.Equal(0, item.RollIndex));

            SaveGameData save = fixture.Host.Session.Save("pending-reroll");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, save.LastCommandSequence, 1, null, fixture.Store, fixture.Clock);
            Decision loaded = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(accepted.Rolled, loaded.PendingResolution.AcceptedRolls.Single(item => item.InfluenceId == target.Id).Rolled);
            Assert.Equal(initial.Rolled, Assert.Single(loaded.PendingResolution.SupersededRolls).Rolled);
            Assert.Equal(0, restored.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);

            Assert.True(restored.Session.Execute(new CommitDecisionResolutionCommand(loaded.Id)).IsSuccess);
            Assert.Equal(accepted.Rolled, loaded.Resolution.Rolls.Single(item => item.InfluenceId == target.Id).Rolled);
            Assert.Equal(initial.Rolled, Assert.Single(loaded.Resolution.SupersededRolls).Rolled);
        }

        [Fact]
        public void OfflineExpiryCommitsPendingRollsWithoutConsumingRerollAvailability()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id));
            fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(15), SimulationMode.OfflineCatchUp);

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);
        }

        [Fact]
        public void InvalidRerollTimingMutatesAndSpendsNothing()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            DecisionInfluence target = decision.Influences[0];

            Result result = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, target.Id));

            Assert.True(result.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonRollsNotProduced, result.Reason);
            Assert.Equal(0, target.RollIndex);
            Assert.Empty(decision.Interventions);
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);
        }

        [Fact]
        public void DuplicateHiddenAndPostRollSubstitutionCommandsSpendNothing()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id));
            fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id));

            Result hidden = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, decision.Influences[3].Id));
            Assert.True(hidden.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonInfluenceHidden, hidden.Reason);
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);

            Result lateSubstitution = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionLoadedTwenty, decision.Influences[0].Id));
            Assert.True(lateSubstitution.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonRollsAlreadyProduced, lateSubstitution.Reason);
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReplacementDie).Value.Balance);

            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, decision.Influences[0].Id)).IsSuccess);
            int accepted = decision.PendingResolution.AcceptedRolls
                .Single(item => item.InfluenceId == decision.Influences[0].Id).Rolled;
            Result duplicate = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, decision.Influences[0].Id));
            Assert.True(duplicate.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonAlreadyApplied, duplicate.Reason);
            Assert.Equal(accepted, decision.PendingResolution.AcceptedRolls
                .Single(item => item.InfluenceId == decision.Influences[0].Id).Rolled);
        }

        [Fact]
        public void RerollAllowanceRefreshesOncePerWorldDayWithoutBanking()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id));
            fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id));
            fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, TestWorld.InterventionReroll, decision.Influences[0].Id));
            fixture.Host.Session.Execute(new CommitDecisionResolutionCommand(decision.Id));

            fixture.Host.Session.Advance(SimDuration.FromDays(1));
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);
            fixture.Host.Session.Advance(SimDuration.FromDays(1));
            Assert.Equal(1, fixture.Host.World.InterventionResources
                .All.Single(pair => pair.Key == InterventionResourceKind.ReRoll).Value.Balance);
        }
    }
}
