using System.Linq;
using Xunit;

namespace Vivarium.SimRunner.Tests
{
    public sealed class MvpExperienceAuditTests
    {
        [Fact]
        public void TwoDayControlAndInfluencedContinuationsRemainLegibleAndEquivalent()
        {
            MvpExperienceAuditResult audit = MvpExperienceAudit.Run();

            Assert.True(audit.ContinuationsEquivalent);
            Assert.True(audit.Passed);
            Assert.Equal(3, audit.Branches.Count);
            Assert.All(audit.Branches, branch =>
            {
                Assert.Equal(10, branch.Characters.Count);
                Assert.Empty(branch.Issues);
                Assert.True(branch.ActivityCount > 10);
                Assert.NotEmpty(branch.Decisions);
                Assert.InRange(branch.DecisionCount, 1, 40);
                Assert.All(branch.Characters, character =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(character.Activity));
                    Assert.False(string.IsNullOrWhiteSpace(character.Location));
                });
                Assert.All(branch.Decisions, decision =>
                {
                    Assert.True(decision.OptionCount >= 2);
                    if (decision.Status == "Resolved") Assert.True(decision.FrozenReasonCount > 0);
                });
            });

            MvpExperienceBranchReport control = audit.Branches.Single(
                branch => branch.Name == "intervention-free/live");
            MvpExperienceBranchReport live = audit.Branches.Single(
                branch => branch.Name == "intervention-heavy/live");
            MvpExperienceBranchReport offline = audit.Branches.Single(
                branch => branch.Name == "intervention-heavy/offline");

            Assert.DoesNotContain(control.Decisions, decision => decision.InterventionCount > 0);
            Assert.Contains(control.Decisions, decision =>
                decision.DefinitionId == SampleContent.DecisionCommitmentConflict.Value &&
                decision.Status == "Resolved");
            Assert.Contains(control.Decisions, decision =>
                decision.DefinitionId == SampleContent.DecisionSeekCompany.Value);
            Assert.Contains(control.Decisions, decision =>
                decision.DefinitionId == SampleContent.DecisionRelyOnPerson.Value);
            Assert.InRange(control.Decisions.Count(decision =>
                decision.DefinitionId == SampleContent.DecisionSeekCompany.Value ||
                decision.DefinitionId == SampleContent.DecisionRelyOnPerson.Value), 2, 16);
            Assert.Contains(live.Decisions, decision => decision.InterventionCount > 0);
            Assert.Equal(live.ContinuationFingerprint, offline.ContinuationFingerprint);
            Assert.Equal(live.FinalTime, offline.FinalTime);
            Assert.NotEmpty(offline.Recap);
            Assert.Contains(offline.Recap, entry => entry.Category == "Decision");
        }
    }
}
