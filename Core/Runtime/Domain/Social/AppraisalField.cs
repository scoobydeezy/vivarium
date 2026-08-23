using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    public static class AppraisalLenses
    {
        public static readonly AuthoredId Affiliation = new AuthoredId("social.lens.affiliation");
        public static readonly AuthoredId Respect = new AuthoredId("social.lens.respect");
        public static readonly AuthoredId Comfort = new AuthoredId("social.lens.comfort");
        public static readonly AuthoredId Attraction = new AuthoredId("social.lens.attraction");
        public static readonly AuthoredId Reliance = new AuthoredId("social.lens.reliance");
    }

    public readonly struct SocialLinearTerm
    {
        public SocialLinearTerm(AuthoredId dimension, long coefficient, AuthoredId provenance = default)
        {
            if (!dimension.IsSet)
            {
                throw new ArgumentException("A linear term needs a dimension.", nameof(dimension));
            }

            Dimension = dimension;
            Coefficient = coefficient;
            Provenance = provenance;
        }

        public AuthoredId Dimension { get; }
        public long Coefficient { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>
    /// One sparse symmetric quadratic contribution. For off-diagonal pairs the coefficient is the
    /// complete authored contribution to xᵢxⱼ; callers do not add a second mirrored term.
    /// </summary>
    public readonly struct SocialPairwiseTerm
    {
        public SocialPairwiseTerm(AuthoredId first, AuthoredId second, long coefficient, AuthoredId provenance = default)
        {
            Pair = new SocialDimensionPair(first, second);
            Coefficient = coefficient;
            Provenance = provenance;
        }

        public SocialDimensionPair Pair { get; }
        public long Coefficient { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>One row of the ideal/tolerance factor L, identified so context can alter it sparsely.</summary>
    public sealed class IdealFactor
    {
        public IdealFactor(AuthoredId id, IReadOnlyList<SocialLinearTerm> coefficients, AuthoredId provenance = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An ideal factor needs a stable id.", nameof(id));
            }

            Id = id;
            Coefficients = coefficients ?? new SocialLinearTerm[0];
            Provenance = provenance;
        }

        public AuthoredId Id { get; }
        public IReadOnlyList<SocialLinearTerm> Coefficients { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>Sparse coefficient deltas activated by one authored context tag.</summary>
    public sealed class AppraisalContextModifier
    {
        public AppraisalContextModifier(
            AuthoredId contextId,
            long biasDelta = 0,
            IReadOnlyList<SocialLinearTerm> linearDeltas = null,
            IReadOnlyList<SocialPairwiseTerm> pairwiseDeltas = null,
            SocialVector idealPointDelta = null,
            IReadOnlyList<IdealFactor> idealFactorDeltas = null,
            AuthoredId provenance = default)
        {
            if (!contextId.IsSet)
            {
                throw new ArgumentException("A context modifier needs a stable context id.", nameof(contextId));
            }

            ContextId = contextId;
            BiasDelta = biasDelta;
            LinearDeltas = linearDeltas ?? new SocialLinearTerm[0];
            PairwiseDeltas = pairwiseDeltas ?? new SocialPairwiseTerm[0];
            IdealPointDelta = idealPointDelta ?? new SocialVector();
            IdealFactorDeltas = idealFactorDeltas ?? new IdealFactor[0];
            Provenance = provenance;
        }

        public AuthoredId ContextId { get; }
        public long BiasDelta { get; }
        public IReadOnlyList<SocialLinearTerm> LinearDeltas { get; }
        public IReadOnlyList<SocialPairwiseTerm> PairwiseDeltas { get; }
        public SocialVector IdealPointDelta { get; }
        public IReadOnlyList<IdealFactor> IdealFactorDeltas { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>A directional, runtime appraisal field owned by one character for one lens.</summary>
    public sealed class AppraisalField
    {
        public AppraisalField(
            CharacterId observerId,
            AuthoredId lensId,
            long bias,
            IReadOnlyList<SocialLinearTerm> linearTerms,
            IReadOnlyList<SocialPairwiseTerm> pairwiseTerms,
            SocialVector idealPoint,
            IReadOnlyList<IdealFactor> idealFactors,
            IReadOnlyList<AppraisalContextModifier> contextModifiers,
            AuthoredId calibrationProfileId,
            int revision = 0)
        {
            if (!observerId.IsSet)
            {
                throw new ArgumentException("An appraisal field needs an observer.", nameof(observerId));
            }
            if (!lensId.IsSet || !calibrationProfileId.IsSet)
            {
                throw new ArgumentException("Lens and calibration profile ids must be stable authored ids.");
            }

            ObserverId = observerId;
            LensId = lensId;
            Bias = bias;
            LinearTerms = linearTerms ?? new SocialLinearTerm[0];
            PairwiseTerms = pairwiseTerms ?? new SocialPairwiseTerm[0];
            IdealPoint = idealPoint ?? new SocialVector();
            IdealFactors = idealFactors ?? new IdealFactor[0];
            ContextModifiers = contextModifiers ?? new AppraisalContextModifier[0];
            CalibrationProfileId = calibrationProfileId;
            Revision = revision;
        }

        public CharacterId ObserverId { get; }
        public AuthoredId LensId { get; }
        public long Bias { get; }
        public IReadOnlyList<SocialLinearTerm> LinearTerms { get; }
        public IReadOnlyList<SocialPairwiseTerm> PairwiseTerms { get; }
        public SocialVector IdealPoint { get; }
        public IReadOnlyList<IdealFactor> IdealFactors { get; }
        public IReadOnlyList<AppraisalContextModifier> ContextModifiers { get; }
        public AuthoredId CalibrationProfileId { get; }
        public int Revision { get; private set; }

        public void MarkDrifted() => Revision++;
    }

    public sealed class SocialEvaluationContext
    {
        private readonly SortedSet<AuthoredId> _tags = new SortedSet<AuthoredId>();

        public SocialEvaluationContext(IEnumerable<AuthoredId> tags = null)
        {
            if (tags == null)
            {
                return;
            }

            foreach (AuthoredId tag in tags)
            {
                if (tag.IsSet)
                {
                    _tags.Add(tag);
                }
            }
        }

        public IEnumerable<AuthoredId> Tags => _tags;
        public bool Contains(AuthoredId tag) => _tags.Contains(tag);
    }
}
