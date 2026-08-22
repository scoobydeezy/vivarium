using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Spatial
{
    /// <summary>
    /// A planned route: origin, destination, and the legs between (§28, §29.2).
    /// <para>
    /// A Traveling Activity commits this plan's total duration when it starts. Multi-leg routing
    /// complexity is deferred (§57) — legs can grow richer without the Activity abstraction changing.
    /// </para>
    /// </summary>
    public sealed class TravelPlan
    {
        private static readonly TravelConnection[] NoLegs = new TravelConnection[0];

        public TravelPlan(LocationId origin, LocationId destination, IReadOnlyList<TravelConnection> legs)
        {
            Origin = origin;
            Destination = destination;
            Legs = legs ?? NoLegs;

            long total = 0;
            for (int i = 0; i < Legs.Count; i++)
            {
                total += Legs[i].Cost.TotalMinutes;
            }

            TotalCost = new SimDuration(total);
        }

        public LocationId Origin { get; }

        public LocationId Destination { get; }

        public IReadOnlyList<TravelConnection> Legs { get; }

        public SimDuration TotalCost { get; }

        /// <summary>The mode of the first leg, used as the Activity's headline travel mode.</summary>
        public AuthoredId PrimaryTravelModeId => Legs.Count > 0 ? Legs[0].TravelModeId : AuthoredId.None;

        public static TravelPlan Trivial(LocationId at) => new TravelPlan(at, at, NoLegs);

        public bool IsTrivial => Legs.Count == 0;

        public override string ToString() => $"{Origin}→{Destination} ({Legs.Count} legs, {TotalCost})";
    }
}
