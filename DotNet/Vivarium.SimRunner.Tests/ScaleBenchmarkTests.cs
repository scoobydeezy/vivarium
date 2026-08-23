using System;
using Vivarium.Domain.Time;
using Xunit;
using Xunit.Abstractions;

namespace Vivarium.SimRunner.Tests
{
    public sealed class ScaleBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ScaleBenchmarkTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void FixedScaleWorkloadIsDeterministicAndStructurallyBounded()
        {
            var budget = new ScaleBudget(
                population: 250,
                duration: SimDuration.FromHours(6),
                maximumBuildMilliseconds: long.MaxValue,
                maximumRunMilliseconds: long.MaxValue,
                maximumManagedMegabytes: long.MaxValue,
                maximumWorkPerCharacter: 60,
                maximumActivitiesPerCharacter: 8,
                maximumPendingEventsPerCharacter: 3);

            ScaleBenchmarkResult first = ScaleBenchmark.Run(budget.Population, budget.Duration);
            ScaleBenchmarkResult second = ScaleBenchmark.Run(budget.Population, budget.Duration);
            Report("first", first);
            Report("second", second);

            Assert.Equal(first.Signature, second.Signature);
            Assert.Equal(first.InstantsSettled, second.InstantsSettled);
            Assert.Equal(first.WorkProcessed, second.WorkProcessed);
            Assert.Equal(first.ActivitiesCreated, second.ActivitiesCreated);
            Assert.Equal(first.PendingEvents, second.PendingEvents);
            Assert.True(
                ScaleBenchmark.MeetsStructuralBudget(first, budget),
                $"Structural scale budget exceeded: work={first.WorkPerCharacter}, activities={first.ActivitiesPerCharacter}, pending={first.PendingEventsPerCharacter} per character.");
        }

        [Fact]
        public void StandardMeasuredBudgetIsOptIn()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("VIVARIUM_ENFORCE_PERFORMANCE_BUDGETS"),
                "1",
                StringComparison.Ordinal))
            {
                _output.WriteLine("Skipped measurement: set VIVARIUM_ENFORCE_PERFORMANCE_BUDGETS=1 to enforce the 1,000-character/one-day gate.");
                return;
            }

            ScaleBudget budget = ScaleBudget.StandardOneDay;
            ScaleBenchmarkResult result = ScaleBenchmark.Run(budget.Population, budget.Duration);
            Report("standard", result);

            Assert.True(ScaleBenchmark.MeetsStructuralBudget(result, budget));
            Assert.True(
                ScaleBenchmark.MeetsMeasuredBudget(result, budget),
                $"Measured scale budget exceeded: build={result.BuildMilliseconds}ms, run={result.RunMilliseconds}ms, heap={result.ManagedMegabytes}MB.");
        }

        private void Report(string label, ScaleBenchmarkResult result) => _output.WriteLine(
            $"{label}: population={result.Population} duration={result.Duration} build={result.BuildMilliseconds}ms " +
            $"run={result.RunMilliseconds}ms heap={result.ManagedMegabytes}MB instants={result.InstantsSettled} " +
            $"work={result.WorkProcessed} activities={result.ActivitiesCreated} pending={result.PendingEvents} hash={result.Signature}");
    }
}
