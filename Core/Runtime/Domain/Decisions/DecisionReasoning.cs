using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Decisions
{
    public static class ConsiderationIds
    {
        public static readonly AuthoredId InterpersonalComfort =
            new AuthoredId("decision.consideration.interpersonal_comfort");
    }

    public static class ReasonChannelIds
    {
        public static readonly AuthoredId InterpersonalComfort =
            new AuthoredId("decision.reason_channel.interpersonal_comfort");
    }

    public static class SocialDecisionDependencies
    {
        public static readonly AuthoredId BeliefContext = new AuthoredId("decision.context.social_belief");
        public static readonly AuthoredId AppraisalContext = new AuthoredId("decision.context.social_appraisal");
        public static readonly AuthoredId RelationshipContext = new AuthoredId("decision.context.social_relationship");
    }

    public enum ReasonChannelConsolidationPolicy
    {
        NonStacking = 0,
        AllowStacking = 1,
    }

    public readonly struct DecisionSignalEvidence
    {
        public DecisionSignalEvidence(AuthoredId signalId, long mean, long variance, SignalApplicability applicability, int sourceRevision)
        {
            SignalId = signalId; Mean = mean; Variance = variance; Applicability = applicability; SourceRevision = sourceRevision;
        }
        public AuthoredId SignalId { get; }
        public long Mean { get; }
        public long Variance { get; }
        public SignalApplicability Applicability { get; }
        public int SourceRevision { get; }
    }

    public readonly struct DecisionContributionEvidence
    {
        public DecisionContributionEvidence(int kind, AuthoredId sourceId, long amount)
        {
            Kind = kind; SourceId = sourceId; Amount = amount;
        }
        public int Kind { get; }
        public AuthoredId SourceId { get; }
        public long Amount { get; }
    }

    public sealed class DecisionReasonEvaluation
    {
        public DecisionReasonEvaluation(
            long expectedScore,
            long outputVariance,
            IReadOnlyList<DecisionSignalEvidence> signals = null,
            IReadOnlyList<DecisionContributionEvidence> contributions = null)
        {
            ExpectedScore = expectedScore;
            OutputVariance = outputVariance;
            Signals = signals ?? new DecisionSignalEvidence[0];
            Contributions = contributions ?? new DecisionContributionEvidence[0];
        }
        public long ExpectedScore { get; }
        public long OutputVariance { get; }
        public IReadOnlyList<DecisionSignalEvidence> Signals { get; }
        public IReadOnlyList<DecisionContributionEvidence> Contributions { get; }
    }

    /// <summary>Defines how semantically correlated candidates become playable reasons.</summary>
    public sealed class ReasonChannelDefinition
    {
        public ReasonChannelDefinition(
            AuthoredId id,
            ReasonChannelConsolidationPolicy consolidationPolicy = ReasonChannelConsolidationPolicy.NonStacking)
        {
            if (!id.IsSet) throw new ArgumentException("A reason channel needs a stable id.", nameof(id));
            Id = id;
            ConsolidationPolicy = consolidationPolicy;
        }

        public AuthoredId Id { get; }
        public ReasonChannelConsolidationPolicy ConsolidationPolicy { get; }
    }

    /// <summary>A semantic reason proposed by a Consideration. It is deliberately not yet a die.</summary>
    public sealed class CandidateReason
    {
        public CandidateReason(
            AuthoredId optionId,
            AuthoredId considerationId,
            ReasonChannelDefinition channel,
            long expectedScore,
            long outputVariance,
            Die gameplayDie,
            InfluencePolarity polarity,
            AuthoredId categoryId,
            AuthoredId labelId,
            InfluenceVisibility visibility,
            DecisionDependencyKey dependencyKey,
            EntityRef subject,
            AuthoredId appraisalLensId = default,
            IReadOnlyList<DecisionDependencyKey> additionalDependencies = null,
            AuthoredId bindingId = default,
            DecisionReasonEvaluation evaluation = null)
        {
            OptionId = optionId;
            ConsiderationId = considerationId;
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            ExpectedScore = expectedScore;
            OutputVariance = outputVariance;
            GameplayDie = gameplayDie;
            Polarity = polarity;
            CategoryId = categoryId;
            LabelId = labelId;
            Visibility = visibility;
            DependencyKey = dependencyKey;
            Subject = subject;
            AppraisalLensId = appraisalLensId;
            AdditionalDependencies = additionalDependencies ?? new DecisionDependencyKey[0];
            BindingId = bindingId.IsSet ? bindingId : considerationId;
            Evaluation = evaluation ?? new DecisionReasonEvaluation(expectedScore, outputVariance);
        }

        public AuthoredId OptionId { get; }
        public AuthoredId ConsiderationId { get; }
        public ReasonChannelDefinition Channel { get; }
        public long ExpectedScore { get; }
        public long OutputVariance { get; }
        public Die GameplayDie { get; }
        public InfluencePolarity Polarity { get; }
        public AuthoredId CategoryId { get; }
        public AuthoredId LabelId { get; }
        public InfluenceVisibility Visibility { get; }
        public DecisionDependencyKey DependencyKey { get; }
        public EntityRef Subject { get; }
        public AuthoredId AppraisalLensId { get; }
        public IReadOnlyList<DecisionDependencyKey> AdditionalDependencies { get; }
        public AuthoredId BindingId { get; }
        public DecisionReasonEvaluation Evaluation { get; }
    }

    /// <summary>
    /// First real Consideration. The underlying social appraisal has already run through the generic
    /// SignalField evaluator; this layer gives that result option-relative Decision meaning.
    /// </summary>
    public sealed class InterpersonalComfortConsideration
    {
        private static readonly ReasonChannelDefinition Channel =
            new ReasonChannelDefinition(ReasonChannelIds.InterpersonalComfort);

        public CandidateReason Evaluate(
            Decision decision,
            CharacterId targetId,
            CompositeSocialEvaluationResult evaluation,
            SocialDecisionInfluenceSpec spec)
        {
            if (decision == null || evaluation == null || spec == null)
            {
                throw new ArgumentNullException("Decision, social evaluation, and influence semantics are required.");
            }
            if (evaluation.Strength == AppraisalStrength.Negligible)
            {
                return null;
            }

            bool positive = evaluation.NormalizedAppraisal >= 0;
            AuthoredId option = positive ? spec.PositiveOptionId : spec.NegativeOptionId;
            AuthoredId label = positive ? spec.PositiveLabelId : spec.NegativeLabelId;
            var dependency = new DecisionDependencyKey(
                RevisionAspects.Scoped(
                    SocialDecisionDependencies.BeliefContext,
                    new AuthoredId("target." + targetId.Value)),
                decision.CharacterId.ToRef());

            return new CandidateReason(
                option,
                ConsiderationIds.InterpersonalComfort,
                Channel,
                evaluation.NormalizedAppraisal,
                evaluation.OutputVariance,
                DecisionInfluenceScale.DieFor(evaluation.Strength),
                InfluencePolarity.Supporting,
                spec.CategoryId,
                label,
                spec.Visibility,
                dependency,
                targetId.ToRef(),
                evaluation.PersonalityAppraisal.LensId);
        }
    }

    /// <summary>Default non-stacking consolidation: one option gets one reason per semantic channel.</summary>
    public sealed class ReasonConsolidator
    {
        public IReadOnlyList<CandidateReason> Consolidate(IReadOnlyList<CandidateReason> candidates)
        {
            var selected = new SortedDictionary<string, CandidateReason>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                CandidateReason candidate = candidates[i];
                if (candidate == null) continue;
                string key = candidate.OptionId.Value + "\n" + candidate.Channel.Id.Value;
                if (candidate.Channel.ConsolidationPolicy == ReasonChannelConsolidationPolicy.AllowStacking)
                {
                    key += "\n" + candidate.ConsiderationId.Value;
                }

                if (!selected.TryGetValue(key, out CandidateReason existing) || Prefer(candidate, existing))
                {
                    selected[key] = candidate;
                }
            }
            return new List<CandidateReason>(selected.Values);
        }

        private static bool Prefer(CandidateReason candidate, CandidateReason existing)
        {
            long candidateMagnitude = Math.Abs(candidate.ExpectedScore);
            long existingMagnitude = Math.Abs(existing.ExpectedScore);
            return candidateMagnitude > existingMagnitude ||
                   (candidateMagnitude == existingMagnitude && candidate.ConsiderationId.CompareTo(existing.ConsiderationId) < 0);
        }
    }

    public static class DecisionInfluenceScale
    {
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

    /// <summary>Turns consolidated semantic reasons into stable, playable influences.</summary>
    public sealed class DecisionReasoningInfluenceFactory
    {
        public DecisionInfluence Add(Decision decision, CandidateReason reason)
        {
            if (decision == null || reason == null)
            {
                throw new ArgumentNullException("Decision and consolidated reason are required.");
            }

            DecisionInfluence influence = decision.AddInfluence(
                reason.OptionId,
                reason.CategoryId,
                reason.LabelId,
                reason.GameplayDie,
                reason.Visibility,
                reason.DependencyKey,
                reason.Subject,
                reason.Polarity,
                reason.Channel.Id,
                reason.BindingId,
                reason.Evaluation);
            // These dependencies belong to the migrated social-appraisal path. Generic compiled
            // Considerations supply their own provider dependencies and must not acquire phantom
            // social routing merely because they share the final Influence factory.
            if (reason.AppraisalLensId.IsSet)
            {
                decision.RegisterDependency(new DecisionDependencyKey(
                    RevisionAspects.Scoped(SocialDecisionDependencies.AppraisalContext, reason.AppraisalLensId),
                    decision.CharacterId.ToRef()));
                decision.RegisterDependency(new DecisionDependencyKey(
                    RevisionAspects.Scoped(
                        SocialDecisionDependencies.RelationshipContext,
                        new AuthoredId("target." + reason.Subject.RuntimeId)),
                    decision.CharacterId.ToRef()));
            }
            for (int i = 0; i < reason.AdditionalDependencies.Count; i++)
            {
                decision.RegisterDependency(reason.AdditionalDependencies[i]);
            }
            return influence;
        }
    }
}
