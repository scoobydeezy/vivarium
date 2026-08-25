using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Spatial
{
    /// <summary>
    /// Identifies a stretch of a journey, so two travellers can become interaction candidates without
    /// scanning every traveller in the world (§30, §32).
    /// </summary>
    public readonly struct TravelSegmentKey : IComparable<TravelSegmentKey>, IEquatable<TravelSegmentKey>
    {
        public TravelSegmentKey(LocationId from, LocationId to)
        {
            From = from;
            To = to;
        }

        public LocationId From { get; }

        public LocationId To { get; }

        public int CompareTo(TravelSegmentKey other)
        {
            int byFrom = From.CompareTo(other.From);
            return byFrom != 0 ? byFrom : To.CompareTo(other.To);
        }

        public bool Equals(TravelSegmentKey other) => From == other.From && To == other.To;

        public override bool Equals(object obj) => obj is TravelSegmentKey other && Equals(other);

        public override int GetHashCode() => (From.Value * 397) ^ To.Value;

        public override string ToString() => $"{From}→{To}";
    }

    /// <summary>
    /// Occupancy indexes derived from Activity spatial contexts (§30).
    /// <para>
    /// Maintained on Activity transitions, never by scanning all current Activities per query
    /// (§50). Answers "who is in this room / building / district?", "who is travelling?", and
    /// "how many are within this settlement?" without touching the population.
    /// </para>
    /// <para>
    /// <b>Direct occupancy excludes Traveling Activities</b> unless the travel context is itself an
    /// occupiable location (§30). Rebuildable from canonical state after load (§40).
    /// </para>
    /// </summary>
    public sealed class SpatialIndexes
    {
        private static readonly CharacterId[] NoCharacters = new CharacterId[0];

        private readonly LocationHierarchy _hierarchy;
        private readonly IndexedMembership<LocationId, CharacterId> _direct = new IndexedMembership<LocationId, CharacterId>();
        private readonly IndexedMembership<LocationId, CharacterId> _withinAncestors = new IndexedMembership<LocationId, CharacterId>();
        private readonly IndexedMembership<TravelSegmentKey, CharacterId> _travelSegments = new IndexedMembership<TravelSegmentKey, CharacterId>();
        private readonly IndexedMembership<LocationId, CharacterId> _travelDestinations = new IndexedMembership<LocationId, CharacterId>();
        private readonly SortedSet<CharacterId> _travelers = new SortedSet<CharacterId>();

        public SpatialIndexes(LocationHierarchy hierarchy)
        {
            _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        }

        /// <summary>
        /// Moves a character from one spatial context to another. The single entry point for keeping
        /// the indexes in agreement with Activity state — the invariant tests in §51 assert exactly this.
        /// </summary>
        public void ApplyTransition(CharacterId character, ActivitySpatialContext? previous, ActivitySpatialContext? next)
        {
            if (previous.HasValue)
            {
                Withdraw(character, previous.Value);
            }

            if (next.HasValue)
            {
                Admit(character, next.Value);
            }
        }

        /// <summary>Occupants standing directly in this location, ascending. Excludes travellers.</summary>
        public IReadOnlyCollection<CharacterId> DirectOccupantsOf(LocationId location) => _direct.MembersOf(location);

        /// <summary>
        /// Occupants anywhere beneath this location — the building, the district, the settlement.
        /// Includes those directly here.
        /// </summary>
        public IReadOnlyCollection<CharacterId> OccupantsWithin(LocationId location) => _withinAncestors.MembersOf(location);

        public int CountWithin(LocationId location) => _withinAncestors.CountIn(location);

        public int CountDirectlyIn(LocationId location) => _direct.CountIn(location);

        /// <summary>Everyone currently in transit, ascending (§30).</summary>
        public IReadOnlyCollection<CharacterId> Travelers => _travelers;

        /// <summary>Travellers sharing a journey stretch — a bounded candidate pool, not a pair scan (§32).</summary>
        public IReadOnlyCollection<CharacterId> TravelersOn(TravelSegmentKey segment) => _travelSegments.MembersOf(segment);

        /// <summary>Travellers whose committed route currently ends here, ascending.</summary>
        public IReadOnlyCollection<CharacterId> TravelersTo(LocationId destination) => _travelDestinations.MembersOf(destination);

        /// <summary>The location a character directly occupies, if they are not travelling.</summary>
        public bool TryGetDirectLocation(CharacterId character, out LocationId location)
        {
            IReadOnlyCollection<LocationId> containers = _direct.ContainersOf(character);
            foreach (LocationId candidate in containers)
            {
                location = candidate;
                return true;
            }

            location = LocationId.None;
            return false;
        }

        /// <summary>Drops all index content. Call before a full rebuild after load (§40).</summary>
        public void Clear()
        {
            _direct.Clear();
            _withinAncestors.Clear();
            _travelSegments.Clear();
            _travelDestinations.Clear();
            _travelers.Clear();
        }

        private void Admit(CharacterId character, ActivitySpatialContext context)
        {
            if (context.IsTraveling)
            {
                _travelers.Add(character);
                _travelSegments.Add(new TravelSegmentKey(context.Transit.OriginLocationId, context.Transit.DestinationLocationId), character);
                _travelDestinations.Add(context.Transit.DestinationLocationId, character);
                return;
            }

            LocationId location = context.DirectOccupancy;
            if (!location.IsSet)
            {
                return;
            }

            _direct.Add(location, character);
            _withinAncestors.Add(location, character);
            IReadOnlyList<LocationId> ancestors = _hierarchy.AncestorsOf(location);
            for (int i = 0; i < ancestors.Count; i++)
            {
                _withinAncestors.Add(ancestors[i], character);
            }
        }

        private void Withdraw(CharacterId character, ActivitySpatialContext context)
        {
            if (context.IsTraveling)
            {
                _travelers.Remove(character);
                _travelSegments.Remove(new TravelSegmentKey(context.Transit.OriginLocationId, context.Transit.DestinationLocationId), character);
                _travelDestinations.Remove(context.Transit.DestinationLocationId, character);
                return;
            }

            LocationId location = context.DirectOccupancy;
            if (!location.IsSet)
            {
                return;
            }

            _direct.Remove(location, character);
            _withinAncestors.Remove(location, character);
            IReadOnlyList<LocationId> ancestors = _hierarchy.AncestorsOf(location);
            for (int i = 0; i < ancestors.Count; i++)
            {
                _withinAncestors.Remove(ancestors[i], character);
            }
        }
    }
}
