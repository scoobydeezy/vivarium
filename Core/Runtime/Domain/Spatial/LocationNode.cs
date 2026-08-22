using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Spatial
{
    /// <summary>
    /// One node in the generic containment hierarchy (§27).
    /// <para>
    /// The world cannot assume a tower. World → Region → Town → District → Building → Floor → Room and
    /// World → Ship → Deck → Section → Compartment → Station are the same model with different
    /// authored <see cref="LocationKindId"/> values. The setting changes; the abstraction does not.
    /// </para>
    /// <para>
    /// Containment answers "what contains this place?" — never "can a character get there, and how
    /// long does it take?". That is <see cref="TravelNetwork"/> (§28, invariant 50).
    /// </para>
    /// </summary>
    public sealed class LocationNode
    {
        public LocationNode(
            LocationId id,
            LocationId parentLocationId,
            AuthoredId locationKindId,
            string displayName,
            bool isOccupiable = true,
            int capacity = 0)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A location needs an allocated runtime id (§7).", nameof(id));
            }

            Id = id;
            ParentLocationId = parentLocationId;
            LocationKindId = locationKindId;
            DisplayName = displayName;
            IsOccupiable = isOccupiable;
            Capacity = capacity;
        }

        public LocationId Id { get; }

        /// <summary><see cref="LocationId.None"/> for the world root.</summary>
        public LocationId ParentLocationId { get; }

        /// <summary>Content-defined kind, e.g. <c>location_kind.building</c> or <c>location_kind.deck</c>.</summary>
        public AuthoredId LocationKindId { get; }

        public string DisplayName { get; }

        /// <summary>
        /// Whether characters can be <c>Located</c> here. An elevator car may be occupiable; whether
        /// travel connections themselves become occupiable spaces is explicitly deferred (§57).
        /// </summary>
        public bool IsOccupiable { get; }

        /// <summary>Soft occupancy limit; 0 means unlimited. Content decides whether it is enforced.</summary>
        public int Capacity { get; }

        public bool IsRoot => !ParentLocationId.IsSet;

        public override string ToString() => $"{DisplayName} ({Id})";
    }

    /// <summary>Immutable content description of a location kind (§27).</summary>
    public sealed class LocationKindDefinition
    {
        public LocationKindDefinition(AuthoredId id, string displayName, bool occupiableByDefault = true)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            Id = id;
            DisplayName = displayName;
            OccupiableByDefault = occupiableByDefault;
        }

        public AuthoredId Id { get; }

        public string DisplayName { get; }

        public bool OccupiableByDefault { get; }

        public override string ToString() => Id.ToString();
    }
}
