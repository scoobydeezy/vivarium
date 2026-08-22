using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// How an Activity produces an outcome (§29.6).
    /// <para>
    /// <b>Automatic resolution is always available. Interactive resolution is an optional alternate
    /// input path, never a requirement for simulation progress.</b> Ten thousand characters cannot
    /// wait for the player to play their shifts (invariant 45).
    /// </para>
    /// </summary>
    public interface IActivityResolutionStrategy
    {
        /// <summary>The authored activity definition this strategy resolves.</summary>
        AuthoredId ActivityDefinitionId { get; }

        /// <summary>Whether content offers a richer interactive path when Attention makes it eligible.</summary>
        bool SupportsInteractiveResolution { get; }

        /// <summary>
        /// The autonomous path. Must always be implementable without player input, and must use the
        /// deterministic oracle for any randomness (§14).
        /// </summary>
        ActivityPerformanceResult ResolveAutomatic(WorldState world, ActivityInstance activity, SimulationContext context);
    }

    /// <summary>
    /// Applies an accepted <see cref="ActivityPerformanceResult"/> to the world (§29.6).
    /// <para>
    /// One pipeline, two inlets: automatic resolution and player-provided results both arrive here, so
    /// consequences can never diverge between the two paths.
    /// </para>
    /// </summary>
    public interface IActivityConsequenceHandler
    {
        AuthoredId ActivityDefinitionId { get; }

        void Apply(WorldState world, ActivityInstance activity, ActivityPerformanceResult result, SimulationContext context);
    }
}
