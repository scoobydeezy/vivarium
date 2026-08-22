using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    /// <summary>
    /// Tests for the primitive every continuous value depends on (§10.1).
    /// <para>
    /// The threshold-crossing cases matter most: §10.2 makes scheduling the next crossing mandatory, so
    /// an off-by-one here would mean behaviour silently fires a minute early or late — or never.
    /// </para>
    /// </summary>
    public sealed class AnalyticalProgressionTests
    {
        [Fact]
        public void ValueIsComputedFromTimeRatherThanTicked()
        {
            // The brief's own example: hunger 4100 at 14:00, +12/minute, read at 15:00.
            var hunger = AnalyticalProgression.Linear(4100, SimTime.FromClockTime(0, 14, 0), 12, 1, 0, 10000);

            Assert.Equal(4100 + (60 * 12), hunger.ValueAt(SimTime.FromClockTime(0, 15, 0)));
        }

        [Fact]
        public void FractionalRatesStayExactInIntegerMath()
        {
            // One unit every three minutes: no float drift, and no rounding until a whole unit accrues.
            var drift = AnalyticalProgression.Linear(0, SimTime.Epoch, 1, 3);

            Assert.Equal(0, drift.ValueAt(new SimTime(2)));
            Assert.Equal(1, drift.ValueAt(new SimTime(3)));
            Assert.Equal(33, drift.ValueAt(new SimTime(100)));
        }

        [Fact]
        public void ValueClampsToItsConfiguredRange()
        {
            var hunger = AnalyticalProgression.Linear(9000, SimTime.Epoch, 100, 1, 0, 10000);

            Assert.Equal(10000, hunger.ValueAt(new SimTime(1000)));
        }

        [Fact]
        public void CrossingTimeIsTheFirstMinuteTheThresholdIsActuallyReached()
        {
            var hunger = AnalyticalProgression.Linear(0, SimTime.Epoch, 1, 3);

            Assert.True(hunger.TryTimeOfCrossing(10, out SimTime crossing));

            // Exact, not approximate: the value must be below the threshold the minute before.
            Assert.True(hunger.ValueAt(crossing) >= 10);
            Assert.True(hunger.ValueAt(new SimTime(crossing.TotalMinutes - 1)) < 10);
        }

        [Fact]
        public void DecreasingProgressionCrossesDownwards()
        {
            var energy = AnalyticalProgression.Linear(1000, SimTime.Epoch, -7, 2, 0, 1000);

            Assert.True(energy.TryTimeOfCrossing(500, out SimTime crossing));
            Assert.True(energy.ValueAt(crossing) <= 500);
            Assert.True(energy.ValueAt(new SimTime(crossing.TotalMinutes - 1)) > 500);
        }

        [Fact]
        public void StaticProgressionNeverCrosses()
        {
            var stable = AnalyticalProgression.Constant(500, SimTime.Epoch);

            Assert.False(stable.TryTimeOfCrossing(600, out _));
        }

        [Fact]
        public void ThresholdBeyondTheClampIsUnreachable()
        {
            var hunger = AnalyticalProgression.Linear(0, SimTime.Epoch, 10, 1, 0, 5000);

            Assert.False(hunger.TryTimeOfCrossing(6000, out _));
        }

        [Fact]
        public void AlreadySatisfiedThresholdReportsTheAnchor()
        {
            var hunger = AnalyticalProgression.Linear(8000, SimTime.FromClockTime(0, 9, 0), 10);

            Assert.True(hunger.TryTimeOfCrossing(7000, out SimTime crossing));
            Assert.Equal(SimTime.FromClockTime(0, 9, 0), crossing);
        }

        [Fact]
        public void ChangingRateMaterializesTheValueAccumulatedSoFar()
        {
            // §29.7: the boss is present for twenty minutes, so it counts for twenty minutes.
            var performance = AnalyticalProgression.Linear(0, SimTime.FromClockTime(0, 9, 0), 10);

            AnalyticalProgression slowed = performance.WithRate(SimTime.FromClockTime(0, 11, 20), 4);
            Assert.Equal(1400, slowed.ValueAtAnchor);

            AnalyticalProgression restored = slowed.WithRate(SimTime.FromClockTime(0, 11, 40), 10);
            Assert.Equal(1400 + (20 * 4), restored.ValueAtAnchor);

            // And the final value reflects the reduced stretch rather than pretending it never happened.
            Assert.Equal(1480 + (200 * 10), restored.ValueAt(SimTime.FromClockTime(0, 15, 0)));
        }

        [Fact]
        public void DurationBasedProgressionCompletesExactlyOnTime()
        {
            SimTime start = SimTime.FromClockTime(0, 14, 5);
            var travel = AnalyticalProgression.OverDuration(0, 10000, start, SimDuration.FromMinutes(12));

            Assert.Equal(5000, travel.ValueAt(SimTime.FromClockTime(0, 14, 11)));
            Assert.True(travel.TryTimeOfCompletion(out SimTime arrives));
            Assert.Equal(SimTime.FromClockTime(0, 14, 17), arrives);
        }
    }
}
