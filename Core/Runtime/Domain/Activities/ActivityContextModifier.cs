using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// A time-bounded contextual effect on an in-progress Activity (§29.7).
    /// <para>
    /// The hated boss enters at 11:20 and leaves at 11:40. Both moments materialize accumulated
    /// performance and change the rate, so his presence counts for exactly twenty minutes — rather than
    /// being ignored because he was absent when the shift ended at 15:00 (invariant 48).
    /// </para>
    /// <para>
    /// Short subordinate interactions live here too: Mina can keep <c>Working</c> while talking to
    /// Glen, and that conversation modifies her work context without becoming a second primary
    /// Activity.
    /// </para>
    /// </summary>
    public readonly struct ActivityContextModifier
    {
        public ActivityContextModifier(
            AuthoredId modifierId,
            SimTime appliedAt,
            long performanceRateNumerator,
            long performanceRateDenominator,
            EntityRef cause = default)
        {
            ModifierId = modifierId;
            AppliedAt = appliedAt;
            PerformanceRateNumerator = performanceRateNumerator;
            PerformanceRateDenominator = performanceRateDenominator;
            Cause = cause;
        }

        /// <summary>Authored modifier id, e.g. <c>activity_modifier.disliked_colleague_present</c>.</summary>
        public AuthoredId ModifierId { get; }

        public SimTime AppliedAt { get; }

        /// <summary>The performance rate that applies while this modifier is active.</summary>
        public long PerformanceRateNumerator { get; }

        public long PerformanceRateDenominator { get; }

        /// <summary>What caused it — usually the other character. Weak reference (§7.1).</summary>
        public EntityRef Cause { get; }

        public override string ToString() =>
            $"{ModifierId} from {AppliedAt} ({PerformanceRateNumerator}/{PerformanceRateDenominator})";
    }
}
