using System;
using Vivarium.Domain.Randomness;

namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// Optional authoritative trace sink (§53).
    /// <para>
    /// Tracing must be optional so release performance is unaffected, and every entry should be able to
    /// carry the content/rules/random versions — a trace without them cannot distinguish input
    /// divergence from an intentional rule change.
    /// </para>
    /// </summary>
    public interface ISimulationTrace
    {
        bool IsEnabled { get; }

        void Record(string category, string message);
    }

    /// <summary>The no-op trace used in release builds.</summary>
    public sealed class NullSimulationTrace : ISimulationTrace
    {
        public static readonly NullSimulationTrace Instance = new NullSimulationTrace();

        private NullSimulationTrace()
        {
        }

        public bool IsEnabled => false;

        public void Record(string category, string message)
        {
        }
    }

    /// <summary>
    /// Everything a simulation step needs that is not world state itself (§21, §54).
    /// <para>
    /// Carries the explicit <see cref="SimulationMode"/>, the deterministic random oracle, the version
    /// metadata that scopes reproduction (§15), and the optional trace.
    /// </para>
    /// </summary>
    public sealed class SimulationContext
    {
        public SimulationContext(
            WorldState world,
            IRandomOracle random,
            SimulationMode mode,
            int contentVersion,
            int simulationRulesVersion,
            ISimulationTrace trace = null,
            int maxSettlementWorkPerInstant = 100000)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Mode = mode;
            ContentVersion = contentVersion;
            SimulationRulesVersion = simulationRulesVersion;
            Trace = trace ?? NullSimulationTrace.Instance;
            MaxSettlementWorkPerSimulationInstant = maxSettlementWorkPerInstant;
        }

        public WorldState World { get; }

        public IRandomOracle Random { get; }

        public SimulationMode Mode { get; }

        /// <summary>Content version, recorded in traces and saves (§38, §42.1, §53).</summary>
        public int ContentVersion { get; }

        /// <summary>
        /// Rules version. A different ruleset may intentionally produce a different future from the same
        /// saved state; this exists to make that difference diagnosable, not to pretend rules never
        /// evolve (§15).
        /// </summary>
        public int SimulationRulesVersion { get; }

        public int RandomAlgorithmVersion => Random.AlgorithmVersion;

        public ISimulationTrace Trace { get; }

        /// <summary>
        /// Ceiling on total same-instant settlement work — scheduled events plus Domain Event
        /// reactions. Set generously high; exceeding it is a bug, not a load condition (§11.4).
        /// </summary>
        public int MaxSettlementWorkPerSimulationInstant { get; }

        /// <summary>Whether held decisions may remain held in this mode (§21).</summary>
        public bool AllowsHeldDecisions => Mode != SimulationMode.OfflineCatchUp;

        /// <summary>Whether notifications should surface immediately or be batched into a recap (§21).</summary>
        public bool EmitsImmediateNotifications => Mode == SimulationMode.Live;

        public SimulationContext WithMode(SimulationMode mode) => new SimulationContext(
            World,
            Random,
            mode,
            ContentVersion,
            SimulationRulesVersion,
            Trace,
            MaxSettlementWorkPerSimulationInstant);
    }
}
