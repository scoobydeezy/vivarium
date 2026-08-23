using System.Collections.Generic;

namespace Vivarium.Application.Queries
{
    public sealed class SocialContributionView
    {
        public SocialContributionView(string kind, string sourceId, long amount, string explanation)
        {
            Kind = kind;
            SourceId = sourceId;
            Amount = amount;
            Explanation = explanation;
        }

        public string Kind { get; }
        public string SourceId { get; }
        public long Amount { get; }
        public string Explanation { get; }
    }

    public sealed class SocialLensView
    {
        public SocialLensView(
            string lensId,
            long personalityAppraisal,
            long combinedAppraisal,
            string strength,
            long uncertaintyEffect,
            IReadOnlyList<SocialContributionView> contributions)
        {
            LensId = lensId;
            PersonalityAppraisal = personalityAppraisal;
            CombinedAppraisal = combinedAppraisal;
            Strength = strength;
            UncertaintyEffect = uncertaintyEffect;
            Contributions = contributions;
        }

        public string LensId { get; }
        public long PersonalityAppraisal { get; }
        public long CombinedAppraisal { get; }
        public string Strength { get; }
        public long UncertaintyEffect { get; }
        public IReadOnlyList<SocialContributionView> Contributions { get; }
    }

    public sealed class SocialEvaluationView
    {
        public SocialEvaluationView(
            int observerId,
            int targetId,
            IReadOnlyList<SocialLensView> lenses)
        {
            ObserverId = observerId;
            TargetId = targetId;
            Lenses = lenses;
        }

        public int ObserverId { get; }
        public int TargetId { get; }
        public IReadOnlyList<SocialLensView> Lenses { get; }
    }
}
