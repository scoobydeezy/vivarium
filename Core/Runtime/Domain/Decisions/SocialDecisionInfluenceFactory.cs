using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Decisions
{
    public sealed class SocialDecisionInfluenceSpec
    {
        public SocialDecisionInfluenceSpec(
            AuthoredId positiveOptionId,
            AuthoredId negativeOptionId,
            AuthoredId categoryId,
            AuthoredId positiveLabelId,
            AuthoredId negativeLabelId,
            InfluenceVisibility visibility)
        {
            PositiveOptionId = positiveOptionId;
            NegativeOptionId = negativeOptionId;
            CategoryId = categoryId;
            PositiveLabelId = positiveLabelId;
            NegativeLabelId = negativeLabelId;
            Visibility = visibility;
        }

        public AuthoredId PositiveOptionId { get; }
        public AuthoredId NegativeOptionId { get; }
        public AuthoredId CategoryId { get; }
        public AuthoredId PositiveLabelId { get; }
        public AuthoredId NegativeLabelId { get; }
        public InfluenceVisibility Visibility { get; }
    }

    /// <summary>Converts calibrated social pressure into a normal, stable-identity Decision influence.</summary>
    public sealed class SocialDecisionInfluenceFactory
    {
        public static readonly AuthoredId BeliefContext = new AuthoredId("decision.context.social_belief");
        public static readonly AuthoredId AppraisalContext = new AuthoredId("decision.context.social_appraisal");
        public static readonly AuthoredId RelationshipContext = new AuthoredId("decision.context.social_relationship");

        public DecisionInfluence Add(
            Decision decision,
            CharacterId targetId,
            AuthoredId lensId,
            CompositeSocialEvaluationResult evaluation,
            SocialDecisionInfluenceSpec spec)
        {
            if (decision == null || evaluation == null || spec == null)
            {
                throw new ArgumentNullException("Decision, evaluation, and influence spec are required.");
            }
            if (evaluation.Strength == AppraisalStrength.Negligible)
            {
                return null;
            }

            bool positive = evaluation.NormalizedAppraisal >= 0;
            AuthoredId option = positive ? spec.PositiveOptionId : spec.NegativeOptionId;
            AuthoredId label = positive ? spec.PositiveLabelId : spec.NegativeLabelId;
            var beliefDependency = new DecisionDependencyKey(
                RevisionAspects.Scoped(BeliefContext, new AuthoredId("target." + targetId.Value)),
                decision.CharacterId.ToRef());

            DecisionInfluence influence = decision.AddInfluence(
                option,
                spec.CategoryId,
                label,
                DieFor(evaluation.Strength),
                spec.Visibility,
                beliefDependency,
                targetId.ToRef());

            decision.RegisterDependency(new DecisionDependencyKey(
                RevisionAspects.Scoped(AppraisalContext, lensId),
                decision.CharacterId.ToRef()));
            decision.RegisterDependency(new DecisionDependencyKey(
                RevisionAspects.Scoped(RelationshipContext, new AuthoredId("target." + targetId.Value)),
                decision.CharacterId.ToRef()));
            return influence;
        }

        public static Die DieFor(AppraisalStrength strength)
        {
            switch (strength)
            {
                case AppraisalStrength.Minor: return Die.D4;
                case AppraisalStrength.Moderate: return Die.D6;
                case AppraisalStrength.Strong: return Die.D8;
                case AppraisalStrength.Extreme: return Die.D10;
                default: return Die.None;
            }
        }
    }
}
