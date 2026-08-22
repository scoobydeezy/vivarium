using System;

namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// Raised when same-instant settlement work exceeds
    /// <see cref="SimulationContext.MaxSettlementWorkPerSimulationInstant"/> (§11.4).
    /// <para>
    /// This is a deliberate loud failure. Silently deferring the remainder of a cascade to the next
    /// scheduler pass would quietly change simulation semantics based on an arbitrary implementation
    /// limit — discovering "Event A caused B caused A ten thousand times" as a crash is strictly better
    /// than it becoming different gameplay.
    /// </para>
    /// <para>
    /// Development and test builds should fail immediately with the diagnostic trace. Production should
    /// pause authoritative advancement, capture diagnostics, and recover per game-level policy.
    /// </para>
    /// </summary>
    public sealed class SimulationCascadeLimitExceeded : Exception
    {
        public SimulationCascadeLimitExceeded(long instantMinutes, int workPerformed, int limit, string lastWorkDescription)
            : base($"Same-instant settlement at minute {instantMinutes} performed {workPerformed} units of work, exceeding the limit of {limit}. Last work: {lastWorkDescription}. This indicates a runaway cascade (§11.4).")
        {
            InstantMinutes = instantMinutes;
            WorkPerformed = workPerformed;
            Limit = limit;
            LastWorkDescription = lastWorkDescription;
        }

        public long InstantMinutes { get; }

        public int WorkPerformed { get; }

        public int Limit { get; }

        /// <summary>The last unit of work processed, as a starting point for diagnosis.</summary>
        public string LastWorkDescription { get; }
    }
}
