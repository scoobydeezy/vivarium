using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    public enum SocialContributionKind
    {
        Bias = 0,
        Linear = 1,
        Pairwise = 2,
        IdealTolerance = 3,
        Context = 4,
    }

    public readonly struct SocialContribution
    {
        public SocialContribution(SocialContributionKind kind, AuthoredId sourceId, long amount, string explanation)
        {
            Kind = kind;
            SourceId = sourceId;
            Amount = amount;
            Explanation = explanation;
        }

        public SocialContributionKind Kind { get; }
        public AuthoredId SourceId { get; }
        public long Amount { get; }
        public string Explanation { get; }
    }

    public sealed class SocialEvaluationResult
    {
        public SocialEvaluationResult(
            CharacterId observerId,
            CharacterId targetId,
            AuthoredId lensId,
            long pointLatentScore,
            long expectedLatentScore,
            long normalizedAppraisal,
            AppraisalStrength strength,
            IReadOnlyList<SocialContribution> contributions)
        {
            ObserverId = observerId;
            TargetId = targetId;
            LensId = lensId;
            PointLatentScore = pointLatentScore;
            ExpectedLatentScore = expectedLatentScore;
            NormalizedAppraisal = normalizedAppraisal;
            Strength = strength;
            Contributions = contributions;
        }

        public CharacterId ObserverId { get; }
        public CharacterId TargetId { get; }
        public AuthoredId LensId { get; }
        public long PointLatentScore { get; }
        public long ExpectedLatentScore { get; }
        public long UncertaintyEffect => ExpectedLatentScore - PointLatentScore;
        public long NormalizedAppraisal { get; }
        public AppraisalStrength Strength { get; }
        public IReadOnlyList<SocialContribution> Contributions { get; }
    }

    /// <summary>Canonical deterministic evaluation of an uncertain target against a sparse appraisal field.</summary>
    public sealed class SocialAppraisalEvaluator
    {
        public SocialEvaluationResult Evaluate(
            CharacterId targetId,
            BeliefDistribution belief,
            AppraisalField field,
            SocialEvaluationContext context,
            AppraisalCalibrationProfile calibration)
        {
            if (belief == null || field == null || calibration == null)
            {
                throw new ArgumentNullException("Belief, field, and calibration are required.");
            }
            if (field.CalibrationProfileId != calibration.Id)
            {
                throw new InvalidOperationException($"Field {field.LensId} requires calibration {field.CalibrationProfileId}, not {calibration.Id}.");
            }

            MergedField merged = Merge(field, context ?? new SocialEvaluationContext());
            var contributions = new List<SocialContribution>();
            long point = merged.Bias;
            long expected = merged.Bias;
            contributions.Add(new SocialContribution(SocialContributionKind.Bias, field.LensId, field.Bias, "authored baseline"));
            for (int i = 0; i < merged.ContextBiasContributions.Count; i++)
            {
                contributions.Add(merged.ContextBiasContributions[i]);
            }

            foreach (KeyValuePair<AuthoredId, long> term in merged.Linear)
            {
                long amount = SocialNumeric.Multiply(term.Value, belief.Mean[term.Key]);
                point = checked(point + amount);
                expected = checked(expected + amount);
                contributions.Add(new SocialContribution(
                    SocialContributionKind.Linear,
                    SourceId(merged.LinearProvenance[term.Key], term.Key),
                    amount,
                    "linear " + term.Key.Value + ProvenanceText(merged.LinearProvenance[term.Key])));
            }

            foreach (KeyValuePair<SocialDimensionPair, long> term in merged.Pairwise)
            {
                long meanProduct = SocialNumeric.Multiply(belief.Mean[term.Key.First], belief.Mean[term.Key.Second]);
                long pointAmount = SocialNumeric.Multiply(term.Value, meanProduct);
                long uncertaintyAmount = SocialNumeric.MultiplyCovariance(
                    term.Value,
                    belief.Covariance(term.Key.First, term.Key.Second));
                point = checked(point + pointAmount);
                expected = checked(expected + pointAmount + uncertaintyAmount);
                contributions.Add(new SocialContribution(
                    SocialContributionKind.Pairwise,
                    SourceId(
                        merged.PairwiseProvenance[term.Key],
                        new AuthoredId("social.term." + term.Key.First.Value + "." + term.Key.Second.Value)),
                    pointAmount + uncertaintyAmount,
                    "pairwise " + term.Key + ProvenanceText(merged.PairwiseProvenance[term.Key])));
            }

            foreach (KeyValuePair<AuthoredId, SortedDictionary<AuthoredId, long>> factor in merged.IdealFactors)
            {
                long projectedMean = 0;
                foreach (KeyValuePair<AuthoredId, long> coefficient in factor.Value)
                {
                    long centered = belief.Mean[coefficient.Key] - merged.IdealPoint.Get(coefficient.Key);
                    projectedMean = checked(projectedMean + SocialNumeric.Multiply(coefficient.Value, centered));
                }

                long pointPenalty = -(SocialNumeric.Square(projectedMean) / 2);
                long projectedVariance = 0;
                foreach (KeyValuePair<AuthoredId, long> left in factor.Value)
                {
                    foreach (KeyValuePair<AuthoredId, long> right in factor.Value)
                    {
                        long coefficientProduct = SocialNumeric.Multiply(left.Value, right.Value);
                        projectedVariance = checked(projectedVariance + SocialNumeric.MultiplyCovariance(
                            coefficientProduct,
                            belief.Covariance(left.Key, right.Key)));
                    }
                }

                long expectedPenalty = pointPenalty - (projectedVariance / 2);
                point = checked(point + pointPenalty);
                expected = checked(expected + expectedPenalty);
                contributions.Add(new SocialContribution(
                    SocialContributionKind.IdealTolerance,
                    SourceId(merged.FactorProvenance[factor.Key], factor.Key),
                    expectedPenalty,
                    "distance from preferred region" + ProvenanceText(merged.FactorProvenance[factor.Key])));
            }

            long normalized = SocialNumeric.BoundedResponse(expected);
            return new SocialEvaluationResult(
                field.ObserverId,
                targetId,
                field.LensId,
                point,
                expected,
                normalized,
                calibration.Calibrate(normalized),
                contributions);
        }

        private static MergedField Merge(AppraisalField field, SocialEvaluationContext context)
        {
            var merged = new MergedField { Bias = field.Bias, IdealPoint = field.IdealPoint.Copy() };
            AddLinear(merged.Linear, merged.LinearProvenance, field.LinearTerms);
            AddPairwise(merged.Pairwise, merged.PairwiseProvenance, field.PairwiseTerms);
            AddFactors(merged.IdealFactors, merged.FactorProvenance, field.IdealFactors);

            for (int i = 0; i < field.ContextModifiers.Count; i++)
            {
                AppraisalContextModifier modifier = field.ContextModifiers[i];
                if (!context.Contains(modifier.ContextId))
                {
                    continue;
                }

                merged.Bias = checked(merged.Bias + modifier.BiasDelta);
                if (modifier.BiasDelta != 0)
                {
                    AuthoredId source = modifier.Provenance.IsSet ? modifier.Provenance : modifier.ContextId;
                    merged.ContextBiasContributions.Add(new SocialContribution(
                        SocialContributionKind.Context,
                        source,
                        modifier.BiasDelta,
                        "context " + modifier.ContextId.Value + " bias"));
                }
                AddLinear(merged.Linear, merged.LinearProvenance, modifier.LinearDeltas, modifier.Provenance);
                AddPairwise(merged.Pairwise, merged.PairwiseProvenance, modifier.PairwiseDeltas, modifier.Provenance);
                foreach (KeyValuePair<AuthoredId, long> delta in modifier.IdealPointDelta.All)
                {
                    merged.IdealPoint.Set(delta.Key, merged.IdealPoint.Get(delta.Key) + delta.Value);
                }
                AddFactors(merged.IdealFactors, merged.FactorProvenance, modifier.IdealFactorDeltas, modifier.Provenance);
            }

            return merged;
        }

        private static void AddLinear(
            SortedDictionary<AuthoredId, long> target,
            SortedDictionary<AuthoredId, SortedSet<AuthoredId>> provenance,
            IReadOnlyList<SocialLinearTerm> terms,
            AuthoredId inheritedProvenance = default)
        {
            for (int i = 0; i < terms.Count; i++)
            {
                SocialLinearTerm term = terms[i];
                target[term.Dimension] = target.TryGetValue(term.Dimension, out long current)
                    ? checked(current + term.Coefficient)
                    : term.Coefficient;
                AddProvenance(provenance, term.Dimension, term.Provenance.IsSet ? term.Provenance : inheritedProvenance);
            }
        }

        private static void AddPairwise(
            SortedDictionary<SocialDimensionPair, long> target,
            SortedDictionary<SocialDimensionPair, SortedSet<AuthoredId>> provenance,
            IReadOnlyList<SocialPairwiseTerm> terms,
            AuthoredId inheritedProvenance = default)
        {
            for (int i = 0; i < terms.Count; i++)
            {
                SocialPairwiseTerm term = terms[i];
                target[term.Pair] = target.TryGetValue(term.Pair, out long current)
                    ? checked(current + term.Coefficient)
                    : term.Coefficient;
                AddProvenance(provenance, term.Pair, term.Provenance.IsSet ? term.Provenance : inheritedProvenance);
            }
        }

        private static void AddFactors(
            SortedDictionary<AuthoredId, SortedDictionary<AuthoredId, long>> target,
            SortedDictionary<AuthoredId, SortedSet<AuthoredId>> provenance,
            IReadOnlyList<IdealFactor> factors,
            AuthoredId inheritedProvenance = default)
        {
            for (int i = 0; i < factors.Count; i++)
            {
                IdealFactor factor = factors[i];
                AddProvenance(provenance, factor.Id, factor.Provenance.IsSet ? factor.Provenance : inheritedProvenance);
                if (!target.TryGetValue(factor.Id, out SortedDictionary<AuthoredId, long> coefficients))
                {
                    coefficients = new SortedDictionary<AuthoredId, long>();
                    target.Add(factor.Id, coefficients);
                }

                for (int c = 0; c < factor.Coefficients.Count; c++)
                {
                    SocialLinearTerm coefficient = factor.Coefficients[c];
                    coefficients[coefficient.Dimension] = coefficients.TryGetValue(coefficient.Dimension, out long current)
                        ? checked(current + coefficient.Coefficient)
                        : coefficient.Coefficient;
                }
            }
        }

        private static void AddProvenance<TKey>(
            SortedDictionary<TKey, SortedSet<AuthoredId>> target,
            TKey key,
            AuthoredId source)
        {
            if (!target.TryGetValue(key, out SortedSet<AuthoredId> sources))
            {
                sources = new SortedSet<AuthoredId>();
                target.Add(key, sources);
            }
            if (source.IsSet)
            {
                sources.Add(source);
            }
        }

        private static AuthoredId SourceId(SortedSet<AuthoredId> sources, AuthoredId fallback)
        {
            foreach (AuthoredId source in sources)
            {
                return source;
            }
            return fallback;
        }

        private static string ProvenanceText(SortedSet<AuthoredId> sources) =>
            sources.Count == 0 ? string.Empty : " [" + string.Join(", ", sources) + "]";

        private sealed class MergedField
        {
            public long Bias;
            public readonly SortedDictionary<AuthoredId, long> Linear = new SortedDictionary<AuthoredId, long>();
            public readonly SortedDictionary<AuthoredId, SortedSet<AuthoredId>> LinearProvenance =
                new SortedDictionary<AuthoredId, SortedSet<AuthoredId>>();
            public readonly SortedDictionary<SocialDimensionPair, long> Pairwise = new SortedDictionary<SocialDimensionPair, long>();
            public readonly SortedDictionary<SocialDimensionPair, SortedSet<AuthoredId>> PairwiseProvenance =
                new SortedDictionary<SocialDimensionPair, SortedSet<AuthoredId>>();
            public SocialVector IdealPoint;
            public readonly SortedDictionary<AuthoredId, SortedDictionary<AuthoredId, long>> IdealFactors =
                new SortedDictionary<AuthoredId, SortedDictionary<AuthoredId, long>>();
            public readonly SortedDictionary<AuthoredId, SortedSet<AuthoredId>> FactorProvenance =
                new SortedDictionary<AuthoredId, SortedSet<AuthoredId>>();
            public readonly List<SocialContribution> ContextBiasContributions = new List<SocialContribution>();
        }
    }
}
