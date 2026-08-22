using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Time
{
    /// <summary>
    /// The reusable primitive for every value that changes continuously over simulation time (§10.1):
    /// hunger, fatigue, rent accrual, production, recovery, opinion drift, activity progress, travel.
    /// <para>
    /// Shape is always <c>(value at anchor, anchor time, rate)</c>, so the value at any
    /// <see cref="SimTime"/> is <i>computed</i>, never ticked. Sixty hunger events per hour is exactly
    /// what this exists to prevent (§50).
    /// </para>
    /// <para>
    /// The rate is a rational <c>numerator/denominator</c> per simulation minute so that slow drifts
    /// stay exact in integer math (§16) instead of accumulating float error.
    /// </para>
    /// <para>
    /// Immutable: changing a rate produces a new progression via <see cref="WithRate"/>, which
    /// materializes the current value first. Callers must then bump the relevant aspect-scoped
    /// revision and recompute the next threshold event (§10.2).
    /// </para>
    /// </summary>
    public readonly struct AnalyticalProgression
    {
        private AnalyticalProgression(
            long valueAtAnchor,
            SimTime anchoredAt,
            long ratePerMinuteNumerator,
            long ratePerMinuteDenominator,
            long minValue,
            long maxValue)
        {
            if (ratePerMinuteDenominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ratePerMinuteDenominator), "Rate denominator must be positive.");
            }

            if (minValue > maxValue)
            {
                throw new ArgumentException("minValue must not exceed maxValue.", nameof(minValue));
            }

            ValueAtAnchor = IntegerMath.Clamp(valueAtAnchor, minValue, maxValue);
            AnchoredAt = anchoredAt;
            RatePerMinuteNumerator = ratePerMinuteNumerator;
            RatePerMinuteDenominator = ratePerMinuteDenominator;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>The materialized value at <see cref="AnchoredAt"/>.</summary>
        public long ValueAtAnchor { get; }

        /// <summary>The reference timestamp the value was last materialized at.</summary>
        public SimTime AnchoredAt { get; }

        public long RatePerMinuteNumerator { get; }

        public long RatePerMinuteDenominator { get; }

        public long MinValue { get; }

        public long MaxValue { get; }

        public bool IsStatic => RatePerMinuteNumerator == 0;

        public bool IsIncreasing => RatePerMinuteNumerator > 0;

        /// <summary>A value that does not move until something changes its rate.</summary>
        public static AnalyticalProgression Constant(long value, SimTime anchoredAt, long minValue = long.MinValue, long maxValue = long.MaxValue) =>
            new AnalyticalProgression(value, anchoredAt, 0, 1, minValue, maxValue);

        /// <summary>A linear progression of <paramref name="numerator"/>/<paramref name="denominator"/> units per simulation minute.</summary>
        public static AnalyticalProgression Linear(
            long valueAtAnchor,
            SimTime anchoredAt,
            long numerator,
            long denominator = 1,
            long minValue = long.MinValue,
            long maxValue = long.MaxValue) =>
            new AnalyticalProgression(valueAtAnchor, anchoredAt, numerator, denominator, minValue, maxValue);

        /// <summary>
        /// A progression that travels from <paramref name="from"/> to <paramref name="to"/> over
        /// <paramref name="duration"/> — the shape used by timed Activities and travel (§29.2).
        /// </summary>
        public static AnalyticalProgression OverDuration(long from, long to, SimTime anchoredAt, SimDuration duration)
        {
            if (duration.TotalMinutes <= 0)
            {
                return new AnalyticalProgression(to, anchoredAt, 0, 1, Math.Min(from, to), Math.Max(from, to));
            }

            return new AnalyticalProgression(
                from,
                anchoredAt,
                to - from,
                duration.TotalMinutes,
                Math.Min(from, to),
                Math.Max(from, to));
        }

        /// <summary>The value at <paramref name="at"/>, clamped to the configured range.</summary>
        public long ValueAt(SimTime at)
        {
            long elapsed = at.TotalMinutes - AnchoredAt.TotalMinutes;
            if (elapsed == 0 || RatePerMinuteNumerator == 0)
            {
                return ValueAtAnchor;
            }

            long travelled = IntegerMath.FloorDiv(elapsed * RatePerMinuteNumerator, RatePerMinuteDenominator);
            return IntegerMath.Clamp(ValueAtAnchor + travelled, MinValue, MaxValue);
        }

        /// <summary>Materializes the current value and re-anchors, leaving the rate untouched (§10.1 step 1–2).</summary>
        public AnalyticalProgression Reanchored(SimTime at) =>
            new AnalyticalProgression(ValueAt(at), at, RatePerMinuteNumerator, RatePerMinuteDenominator, MinValue, MaxValue);

        /// <summary>
        /// Materializes at <paramref name="at"/> and applies a new rate (§10.1 steps 1–3).
        /// The caller still owes steps 4–5: bump the aspect revision, reschedule the next crossing.
        /// </summary>
        public AnalyticalProgression WithRate(SimTime at, long numerator, long denominator = 1) =>
            new AnalyticalProgression(ValueAt(at), at, numerator, denominator, MinValue, MaxValue);

        /// <summary>Applies an instantaneous offset — eating, sleeping, a one-off payment.</summary>
        public AnalyticalProgression WithOffset(SimTime at, long offset)
        {
            long materialized = ValueAt(at);
            return new AnalyticalProgression(materialized + offset, at, RatePerMinuteNumerator, RatePerMinuteDenominator, MinValue, MaxValue);
        }

        public AnalyticalProgression WithBounds(SimTime at, long minValue, long maxValue) =>
            new AnalyticalProgression(ValueAt(at), at, RatePerMinuteNumerator, RatePerMinuteDenominator, minValue, maxValue);

        /// <summary>
        /// First simulation time at or after <see cref="AnchoredAt"/> where the value reaches
        /// <paramref name="threshold"/> in the direction of travel.
        /// <para>
        /// This is what makes §10.2 possible: any analytical value whose threshold can change
        /// simulation behaviour must schedule its next crossing as a real event, because nothing else
        /// will notice the crossing on its own.
        /// </para>
        /// </summary>
        /// <returns><c>false</c> when the threshold is unreachable — static rate, wrong direction, or beyond the clamp.</returns>
        public bool TryTimeOfCrossing(long threshold, out SimTime crossingTime)
        {
            crossingTime = SimTime.Never;

            if (RatePerMinuteNumerator == 0)
            {
                return false;
            }

            bool increasing = RatePerMinuteNumerator > 0;

            // Already there at the anchor.
            if (increasing ? ValueAtAnchor >= threshold : ValueAtAnchor <= threshold)
            {
                crossingTime = AnchoredAt;
                return true;
            }

            // Beyond the clamp in the direction of travel: never reached.
            if (increasing ? threshold > MaxValue : threshold < MinValue)
            {
                return false;
            }

            long delta = threshold - ValueAtAnchor;
            long elapsed;

            if (increasing)
            {
                // Need floor(e*num/den) >= delta  <=>  e >= ceil(delta*den/num).
                elapsed = IntegerMath.CeilDiv(delta * RatePerMinuteDenominator, RatePerMinuteNumerator);
            }
            else
            {
                // Need floor(e*num/den) <= delta  <=>  e*num < (delta+1)*den  <=>  e > ((delta+1)*den)/num.
                elapsed = IntegerMath.FloorDiv((delta + 1) * RatePerMinuteDenominator, RatePerMinuteNumerator) + 1;
            }

            if (elapsed < 0)
            {
                elapsed = 0;
            }

            crossingTime = new SimTime(AnchoredAt.TotalMinutes + elapsed);
            return true;
        }

        /// <summary>
        /// Convenience for timed work: the time the progression reaches <see cref="MaxValue"/> when
        /// increasing, or <see cref="MinValue"/> when decreasing.
        /// </summary>
        public bool TryTimeOfCompletion(out SimTime completionTime) =>
            TryTimeOfCrossing(IsIncreasing ? MaxValue : MinValue, out completionTime);

        public override string ToString() =>
            $"{ValueAtAnchor}@{AnchoredAt} {(RatePerMinuteNumerator >= 0 ? "+" : string.Empty)}{RatePerMinuteNumerator}/{RatePerMinuteDenominator}min [{MinValue}..{MaxValue}]";
    }
}
