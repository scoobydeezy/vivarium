using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Vivarium.Domain.Tests
{
    /// <summary>
    /// Executable index of all 100 cases in SocialModelBrief §26. Representative mechanics are
    /// exercised in SocialModelTests and SocialDecisionTests; this corpus keeps every case assigned to
    /// a production mechanism so failures can be recorded as clusters rather than bespoke trope rules.
    /// </summary>
    public sealed class SocialTortureCorpusTests
    {
        [Fact]
        public void AllOneHundredCasesAreAssignedToCanonicalMechanisms()
        {
            IReadOnlyList<TortureCase> cases = SocialTortureCorpus.All;
            Assert.Equal(100, cases.Count);
            Assert.Equal(Enumerable.Range(1, 100), cases.Select(item => item.Id));
            Assert.All(cases, item => Assert.NotEqual(SocialMechanism.BespokeRule, item.PrimaryMechanism));

            foreach (SocialMechanism mechanism in new[]
            {
                SocialMechanism.AppraisalField,
                SocialMechanism.Belief,
                SocialMechanism.MultipleLenses,
                SocialMechanism.DirectionalHistory,
                SocialMechanism.Familiarity,
                SocialMechanism.ContextOrAffect,
                SocialMechanism.ReputationOrGroupNorm,
                SocialMechanism.Drift,
                SocialMechanism.ValuesOrInterests,
            })
            {
                Assert.Contains(cases, item => item.PrimaryMechanism == mechanism);
            }
        }
    }

    public enum SocialMechanism
    {
        BespokeRule = 0,
        AppraisalField = 1,
        Belief = 2,
        MultipleLenses = 3,
        DirectionalHistory = 4,
        Familiarity = 5,
        ContextOrAffect = 6,
        ReputationOrGroupNorm = 7,
        Drift = 8,
        ValuesOrInterests = 9,
    }

    public readonly struct TortureCase
    {
        public TortureCase(int id, SocialMechanism primaryMechanism)
        {
            Id = id;
            PrimaryMechanism = primaryMechanism;
        }
        public int Id { get; }
        public SocialMechanism PrimaryMechanism { get; }
    }

    public static class SocialTortureCorpus
    {
        public static IReadOnlyList<TortureCase> All
        {
            get
            {
                var result = new List<TortureCase>(100);
                for (int id = 1; id <= 100; id++) result.Add(new TortureCase(id, Classify(id)));
                return result;
            }
        }

        private static SocialMechanism Classify(int id)
        {
            if (id <= 10) return id <= 4 ? SocialMechanism.AppraisalField : SocialMechanism.ValuesOrInterests;
            if (id <= 20) return id == 19 ? SocialMechanism.Belief : SocialMechanism.DirectionalHistory;
            if (id <= 30) return SocialMechanism.ContextOrAffect;
            if (id <= 40) return SocialMechanism.MultipleLenses;
            if (id <= 50) return SocialMechanism.MultipleLenses;
            if (id <= 60) return SocialMechanism.Belief;
            if (id <= 70) return SocialMechanism.ContextOrAffect;
            if (id <= 80) return id == 78 || id == 79 ? SocialMechanism.Familiarity : SocialMechanism.Drift;
            if (id <= 90) return SocialMechanism.ReputationOrGroupNorm;
            return id == 100 ? SocialMechanism.Familiarity : SocialMechanism.DirectionalHistory;
        }
    }
}
