using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>Which of the two legal spatial shapes an Activity has (§29.1).</summary>
    public enum SpatialContextKind
    {
        /// <summary>Stationary in a location.</summary>
        Located = 0,

        /// <summary>In transit between locations.</summary>
        Traveling = 1,
    }

    /// <summary>
    /// Route and timing of a Traveling Activity (§29.2).
    /// <para>
    /// These parameters are <b>committed</b> when travel begins and are not re-derived afterwards: if
    /// <c>WalkingSpeed</c> is hot-reloaded mid-journey, this journey keeps the arrival time it started
    /// with (§42.1).
    /// </para>
    /// </summary>
    public readonly struct TransitDetails
    {
        public TransitDetails(
            LocationId originLocationId,
            LocationId destinationLocationId,
            SimTime departedAt,
            SimTime arrivesAt,
            AuthoredId travelModeId,
            int travelPlanId = 0)
        {
            OriginLocationId = originLocationId;
            DestinationLocationId = destinationLocationId;
            DepartedAt = departedAt;
            ArrivesAt = arrivesAt;
            TravelModeId = travelModeId;
            TravelPlanId = travelPlanId;
        }

        public LocationId OriginLocationId { get; }

        public LocationId DestinationLocationId { get; }

        public SimTime DepartedAt { get; }

        public SimTime ArrivesAt { get; }

        /// <summary>Authored travel mode, e.g. <c>travel_mode.walking</c> or <c>travel_mode.elevator</c>.</summary>
        public AuthoredId TravelModeId { get; }

        /// <summary>
        /// Optional handle to a multi-leg route. Legs can be added later without changing the Activity
        /// abstraction (§29.2); routing complexity is explicitly deferred (§57).
        /// </summary>
        public int TravelPlanId { get; }

        public SimDuration TotalDuration => ArrivesAt.Since(DepartedAt);

        /// <summary>
        /// Progress in basis points, derived analytically (§10.1). Presentation may interpolate this
        /// visually at frame rate; the Domain never ticks a position.
        /// </summary>
        public int ProgressBasisPointsAt(SimTime at)
        {
            long total = TotalDuration.TotalMinutes;
            if (total <= 0)
            {
                return 10000;
            }

            long elapsed = at.TotalMinutes - DepartedAt.TotalMinutes;
            return (int)IntegerMath.Clamp(elapsed * 10000L / total, 0, 10000);
        }

        public override string ToString() => $"{OriginLocationId}→{DestinationLocationId} by {TravelModeId} (arrives {ArrivesAt})";
    }

    /// <summary>
    /// Where an Activity is happening (§29.1, §29.2).
    /// <para>
    /// Exactly one shape is valid at a time — <c>Located</c> or <c>Traveling</c> — and this is the only
    /// authoritative answer to "where is Mina?". There is deliberately no separate mutable presence
    /// field that could drift out of sync (invariant 40).
    /// </para>
    /// </summary>
    public readonly struct ActivitySpatialContext
    {
        private ActivitySpatialContext(SpatialContextKind kind, LocationId locationId, TransitDetails transit)
        {
            Kind = kind;
            LocationId = locationId;
            Transit = transit;
        }

        public SpatialContextKind Kind { get; }

        /// <summary>The occupied location when <see cref="Kind"/> is <see cref="SpatialContextKind.Located"/>.</summary>
        public LocationId LocationId { get; }

        /// <summary>Route details when <see cref="Kind"/> is <see cref="SpatialContextKind.Traveling"/>.</summary>
        public TransitDetails Transit { get; }

        public bool IsLocated => Kind == SpatialContextKind.Located;

        public bool IsTraveling => Kind == SpatialContextKind.Traveling;

        public static ActivitySpatialContext Located(LocationId locationId)
        {
            if (!locationId.IsSet)
            {
                throw new ArgumentException("A Located context needs a real location.", nameof(locationId));
            }

            return new ActivitySpatialContext(SpatialContextKind.Located, locationId, default);
        }

        public static ActivitySpatialContext Traveling(TransitDetails transit) =>
            new ActivitySpatialContext(SpatialContextKind.Traveling, LocationId.None, transit);

        /// <summary>
        /// The location this context occupies for direct-occupancy indexing, or
        /// <see cref="LocationId.None"/> while travelling — direct occupancy excludes travellers (§30).
        /// </summary>
        public LocationId DirectOccupancy => IsLocated ? LocationId : LocationId.None;

        public override string ToString() => IsLocated ? $"at {LocationId}" : Transit.ToString();
    }
}
