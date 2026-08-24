using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Immutable content description of an Activity (§6, §41).
    /// <para>
    /// Runtime instances snapshot the outcome-affecting values they need at construction (§42.1), so
    /// reloading this definition changes future Activities without rewriting ones already underway.
    /// </para>
    /// </summary>
    public sealed class ActivityDefinition
    {
        public ActivityDefinition(
            AuthoredId id,
            string displayName,
            SimDuration defaultDuration,
            bool producesOutcome,
            bool supportsInteractiveResolution = false,
            bool isTravel = false)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            Id = id;
            DisplayName = displayName;
            DefaultDuration = defaultDuration;
            ProducesOutcome = producesOutcome;
            SupportsInteractiveResolution = supportsInteractiveResolution;
            IsTravel = isTravel;
        }

        public AuthoredId Id { get; }

        public string DisplayName { get; }

        public SimDuration DefaultDuration { get; }

        /// <summary>
        /// Whether this Activity yields an <see cref="ActivityPerformanceResult"/>. Every one that does
        /// needs an autonomous resolution path (invariant 45).
        /// </summary>
        public bool ProducesOutcome { get; }

        /// <summary>Whether content offers an optional interactive path (§29.6).</summary>
        public bool SupportsInteractiveResolution { get; }

        /// <summary>
        /// Whether this is the system-provided travel kind. Travel is an Activity, not a peer transit
        /// subsystem (invariant 41).
        /// </summary>
        public bool IsTravel { get; }

        public override string ToString() => Id.ToString();
    }

    /// <summary>Authored ids for the system-provided activities the architecture assumes exist.</summary>
    public static class WellKnownActivities
    {
        /// <summary>
        /// The travel Activity (§29.2). Its route and timing come from the TravelNetwork; there is no
        /// parallel transit subsystem.
        /// </summary>
        public static readonly AuthoredId Traveling = new AuthoredId("activity.traveling");

        /// <summary>The fallback Activity, so "exactly one primary Activity" always holds (invariant 39).</summary>
        public static readonly AuthoredId Waiting = new AuthoredId("activity.waiting");

        /// <summary>The production sleep Activity used by Energy recovery routines.</summary>
        public static readonly AuthoredId Sleeping = new AuthoredId("activity.sleeping");
    }
}
