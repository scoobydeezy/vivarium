using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Social
{
    public enum SocialFactorSourceKind
    {
        RelationshipChannel = 0,
        Familiarity = 1,
        SharedInterest = 2,
        SharedValue = 3,
        ObserverAffect = 4,
        ContextPressure = 5,
    }

    public sealed class SocialFactorRule
    {
        public SocialFactorRule(
            AuthoredId lensId,
            SocialFactorSourceKind sourceKind,
            AuthoredId sourceId,
            long coefficient,
            AuthoredId explanationId,
            AuthoredId requiredContextId = default)
        {
            LensId = lensId;
            SourceKind = sourceKind;
            SourceId = sourceId;
            Coefficient = coefficient;
            ExplanationId = explanationId;
            RequiredContextId = requiredContextId;
        }

        public AuthoredId LensId { get; }
        public SocialFactorSourceKind SourceKind { get; }
        public AuthoredId SourceId { get; }
        public long Coefficient { get; }
        public AuthoredId ExplanationId { get; }
        public AuthoredId RequiredContextId { get; }
    }

    public sealed class SocialPressureDefinition
    {
        public SocialPressureDefinition(AuthoredId id, IReadOnlyList<SocialFactorRule> rules)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A social pressure definition needs a stable id.", nameof(id));
            }
            Id = id;
            Rules = rules ?? new SocialFactorRule[0];
        }

        public AuthoredId Id { get; }
        public IReadOnlyList<SocialFactorRule> Rules { get; }
    }

    public sealed class CompositeSocialEvaluationResult
    {
        public CompositeSocialEvaluationResult(
            SocialEvaluationResult personalityAppraisal,
            long combinedLatentScore,
            long normalizedAppraisal,
            AppraisalStrength strength,
            IReadOnlyList<SocialContribution> additionalContributions)
        {
            PersonalityAppraisal = personalityAppraisal;
            CombinedLatentScore = combinedLatentScore;
            NormalizedAppraisal = normalizedAppraisal;
            Strength = strength;
            AdditionalContributions = additionalContributions;
        }

        public SocialEvaluationResult PersonalityAppraisal { get; }
        public long CombinedLatentScore { get; }
        public long NormalizedAppraisal { get; }
        public long LatentScoreVariance => PersonalityAppraisal.LatentScoreVariance;
        public long OutputVariance => PersonalityAppraisal.OutputVariance;
        public AppraisalStrength Strength { get; }
        public IReadOnlyList<SocialContribution> AdditionalContributions { get; }
    }

    /// <summary>
    /// Composes personality appraisal with distinct values/interests, directional history,
    /// familiarity, affect, and independent context pressures without rewriting the personality field.
    /// </summary>
    public sealed class SocialPressureEvaluator
    {
        private readonly SocialAppraisalEvaluator _personality = new SocialAppraisalEvaluator();

        public CompositeSocialEvaluationResult Evaluate(
            WorldState world,
            CharacterId observerId,
            CharacterId targetId,
            AuthoredId lensId,
            SocialEvaluationContext context,
            SocialPressureDefinition pressureDefinition,
            DefinitionCatalog catalog)
        {
            if (!world.Characters.TryGet(observerId, out Character observer) ||
                !world.Characters.TryGet(targetId, out Character target))
            {
                throw new InvalidOperationException("Social evaluation requires two existing characters.");
            }
            if (!observer.TryGetAppraisalField(lensId, out AppraisalField field))
            {
                throw new InvalidOperationException($"{observerId} has no appraisal field for {lensId}.");
            }
            if (!catalog.AppraisalCalibrations.TryGetValue(field.CalibrationProfileId, out AppraisalCalibrationProfile calibration))
            {
                throw new InvalidOperationException($"Missing appraisal calibration {field.CalibrationProfileId}.");
            }

            if (!world.Knowledge.TryGetSocialBelief(
                Vivarium.Domain.Knowledge.ObserverRef.Character(observerId),
                targetId,
                out BeliefDistribution belief))
            {
                belief = SocialBeliefUpdateService.BroadPrior();
            }

            SocialEvaluationResult personality = _personality.Evaluate(targetId, belief, field, context, calibration);
            long combined = personality.ExpectedLatentScore;
            var contributions = new List<SocialContribution>();
            DirectionalRelationshipState relationship = null;
            if (world.RelationshipIndex.TryGetBetween(observerId, targetId, out RelationshipId relationshipId))
            {
                relationship = world.Relationships.Get(relationshipId).From(observerId);
            }

            for (int i = 0; i < pressureDefinition.Rules.Count; i++)
            {
                SocialFactorRule rule = pressureDefinition.Rules[i];
                if (rule.LensId != lensId ||
                    (rule.RequiredContextId.IsSet && !context.Contains(rule.RequiredContextId)))
                {
                    continue;
                }

                long source = SourceValue(rule, observer, target, relationship, world);
                long amount = SocialNumeric.Multiply(rule.Coefficient, source);
                combined = checked(combined + amount);
                contributions.Add(new SocialContribution(
                    SocialContributionKind.Context,
                    rule.ExplanationId,
                    amount,
                    rule.ExplanationId.Value));
            }

            long normalized = SocialNumeric.BoundedResponse(combined);
            return new CompositeSocialEvaluationResult(
                personality,
                combined,
                normalized,
                calibration.Calibrate(normalized),
                contributions);
        }

        private static long SourceValue(
            SocialFactorRule rule,
            Character observer,
            Character target,
            DirectionalRelationshipState relationship,
            WorldState world)
        {
            switch (rule.SourceKind)
            {
                case SocialFactorSourceKind.RelationshipChannel:
                    return relationship?.ChannelAt(rule.SourceId, world.Clock.Now) ?? 0;
                case SocialFactorSourceKind.Familiarity:
                    return relationship?.FamiliarityAt(world.Clock.Now) ?? 0;
                case SocialFactorSourceKind.SharedInterest:
                    return SharedTag(observer.Interests.Intensity(rule.SourceId), target.Interests.Intensity(rule.SourceId));
                case SocialFactorSourceKind.SharedValue:
                    return SharedTag(observer.Values.Intensity(rule.SourceId), target.Values.Intensity(rule.SourceId));
                case SocialFactorSourceKind.ObserverAffect:
                    return observer.Affect.ValueAt(rule.SourceId, world.Clock.Now);
                case SocialFactorSourceKind.ContextPressure:
                    return SocialNumeric.Scale;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rule.SourceKind));
            }
        }

        private static long SharedTag(long observer, long target)
        {
            long magnitude = Math.Min(Math.Abs(observer), Math.Abs(target));
            return (observer < 0) != (target < 0) ? -magnitude : magnitude;
        }
    }
}
