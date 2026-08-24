using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Characters
{
    /// <summary>
    /// Immutable content description of a trait (§6, §41).
    /// <para>
    /// Authored in Unity as a ScriptableObject, converted into this Unity-free definition before the
    /// simulation ever sees it. Runtime entities reference it by <see cref="AuthoredId"/> so saves
    /// never contain Unity object references (§39).
    /// </para>
    /// </summary>
    public sealed class TraitDefinition
    {
        public TraitDefinition(
            AuthoredId id,
            string displayName,
            IReadOnlyList<DiscoveryChannel> discoverableThrough = null,
            bool hotReloadSafe = true,
            long projectionBias = 0,
            IReadOnlyList<SocialLinearTerm> projectionLinearTerms = null,
            IReadOnlyList<SocialPairwiseTerm> projectionPairwiseTerms = null,
            long projectionThreshold = 5000)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            Id = id;
            DisplayName = displayName;
            DiscoverableThrough = discoverableThrough ?? new DiscoveryChannel[0];
            HotReloadSafe = hotReloadSafe;
            ProjectionBias = projectionBias;
            ProjectionLinearTerms = projectionLinearTerms ?? new SocialLinearTerm[0];
            ProjectionPairwiseTerms = projectionPairwiseTerms ?? new SocialPairwiseTerm[0];
            ProjectionThreshold = projectionThreshold;
        }

        public AuthoredId Id { get; }

        public string DisplayName { get; }

        /// <summary>
        /// How the player may come to know this trait — career decisions, repeated work behaviour,
        /// conversation (§24). Discovery is configured by content, not hard-coded per system.
        /// </summary>
        public IReadOnlyList<DiscoveryChannel> DiscoverableThrough { get; }

        /// <summary>
        /// Whether reapplying this definition mid-session is a balance-only change (§42). Structural
        /// or save-affecting changes require a restart or migration.
        /// </summary>
        public bool HotReloadSafe { get; }

        public long ProjectionBias { get; }

        public IReadOnlyList<SocialLinearTerm> ProjectionLinearTerms { get; }

        public IReadOnlyList<SocialPairwiseTerm> ProjectionPairwiseTerms { get; }

        public long ProjectionThreshold { get; }

        public bool IsProjection => ProjectionLinearTerms.Count > 0 || ProjectionPairwiseTerms.Count > 0;

        public override string ToString() => Id.ToString();
    }

    /// <summary>Immutable content description of a need (§6).</summary>
    public sealed class NeedDefinition
    {
        public NeedDefinition(
            AuthoredId id,
            string displayName,
            long minValue,
            long maxValue,
            long defaultRateNumerator,
            long defaultRateDenominator = 1,
            IReadOnlyList<long> behaviouralThresholds = null,
            NeedRestRoutineDefinition restRoutine = null,
            NeedSatisfactionRoutineDefinition satisfactionRoutine = null)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            Id = id;
            DisplayName = displayName;
            MinValue = minValue;
            MaxValue = maxValue;
            DefaultRateNumerator = defaultRateNumerator;
            DefaultRateDenominator = defaultRateDenominator;
            BehaviouralThresholds = behaviouralThresholds ?? new long[0];
            RestRoutine = restRoutine;
            SatisfactionRoutine = satisfactionRoutine;
        }

        public AuthoredId Id { get; }

        public string DisplayName { get; }

        /// <summary>Integral range, e.g. hunger 0–10,000 (§16).</summary>
        public long MinValue { get; }

        public long MaxValue { get; }

        public long DefaultRateNumerator { get; }

        public long DefaultRateDenominator { get; }

        /// <summary>
        /// Ascending values at which behaviour can change. Only these get scheduled crossings — the
        /// scaling win of §10 survives precisely because this list stays short.
        /// </summary>
        public IReadOnlyList<long> BehaviouralThresholds { get; }

        /// <summary>Optional content-backed recovery routine, used by reserve Needs such as Energy.</summary>
        public NeedRestRoutineDefinition RestRoutine { get; }

        /// <summary>Optional ordinary Activity that applies an instantaneous satisfying offset.</summary>
        public NeedSatisfactionRoutineDefinition SatisfactionRoutine { get; }

        public override string ToString() => Id.ToString();
    }

    /// <summary>Stable ids for production Needs shared by headless and Unity content.</summary>
    public static class WellKnownNeeds
    {
        public static readonly AuthoredId Energy = new AuthoredId("need.energy");
    }
}
