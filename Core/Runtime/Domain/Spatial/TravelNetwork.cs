using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Spatial
{
    /// <summary>One traversable connection between two locations.</summary>
    public readonly struct TravelConnection
    {
        public TravelConnection(LocationId from, LocationId to, SimDuration cost, AuthoredId travelModeId)
        {
            From = from;
            To = to;
            Cost = cost;
            TravelModeId = travelModeId;
        }

        public LocationId From { get; }

        public LocationId To { get; }

        /// <summary>Traversal time. Integral simulation minutes (§16).</summary>
        public SimDuration Cost { get; }

        /// <summary>Authored mode, e.g. <c>travel_mode.walking</c>, <c>travel_mode.elevator</c>, <c>travel_mode.train</c>.</summary>
        public AuthoredId TravelModeId { get; }

        public override string ToString() => $"{From}→{To} {Cost} by {TravelModeId}";
    }

    /// <summary>
    /// Navigability, separate from containment (§28, invariant 50).
    /// <para>
    /// Answers "can Mina walk from Room A to Room B, and how long does that take?". A ship has
    /// corridors and lifts, a town has roads and paths, a country has trains and airports — the
    /// abstraction is the same.
    /// </para>
    /// <para>
    /// Route planning here is a deterministic Dijkstra with explicit tie-breaks. The real pathfinding
    /// solution is deferred (§57); what matters architecturally is that travel timing is a
    /// <i>committed parameter</i> handed to a Traveling Activity, not a per-frame position update.
    /// </para>
    /// </summary>
    public sealed class TravelNetwork
    {
        private static readonly TravelConnection[] NoConnections = new TravelConnection[0];

        private readonly SortedDictionary<LocationId, List<TravelConnection>> _outgoing =
            new SortedDictionary<LocationId, List<TravelConnection>>();

        /// <summary>Adds a one-way connection.</summary>
        public void Connect(LocationId from, LocationId to, SimDuration cost, AuthoredId travelModeId)
        {
            if (cost.IsNegative)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), "Travel cost cannot be negative.");
            }

            if (!_outgoing.TryGetValue(from, out List<TravelConnection> connections))
            {
                connections = new List<TravelConnection>();
                _outgoing.Add(from, connections);
            }

            connections.Add(new TravelConnection(from, to, cost, travelModeId));

            // Deterministic adjacency order regardless of authoring order (§15).
            connections.Sort((a, b) =>
            {
                int byTarget = a.To.CompareTo(b.To);
                return byTarget != 0 ? byTarget : a.TravelModeId.CompareTo(b.TravelModeId);
            });
        }

        /// <summary>Adds connections in both directions with the same cost and mode.</summary>
        public void ConnectBidirectional(LocationId a, LocationId b, SimDuration cost, AuthoredId travelModeId)
        {
            Connect(a, b, cost, travelModeId);
            Connect(b, a, cost, travelModeId);
        }

        public IReadOnlyList<TravelConnection> ConnectionsFrom(LocationId from) =>
            _outgoing.TryGetValue(from, out List<TravelConnection> connections)
                ? (IReadOnlyList<TravelConnection>)connections
                : NoConnections;

        /// <summary>
        /// Plans a route. Deterministic: equal-cost frontier entries break ties by location id, so the
        /// same graph always yields the same plan (§15).
        /// </summary>
        public bool TryPlanRoute(LocationId origin, LocationId destination, out TravelPlan plan)
        {
            plan = null;

            if (origin == destination)
            {
                plan = TravelPlan.Trivial(origin);
                return true;
            }

            var best = new SortedDictionary<LocationId, SimDuration>();
            var cameFrom = new Dictionary<LocationId, TravelConnection>();
            var frontier = new SortedSet<FrontierEntry>();

            best[origin] = SimDuration.Zero;
            frontier.Add(new FrontierEntry(SimDuration.Zero, origin));

            while (frontier.Count > 0)
            {
                FrontierEntry current = frontier.Min;
                frontier.Remove(current);

                if (current.Location == destination)
                {
                    plan = BuildPlan(origin, destination, cameFrom);
                    return true;
                }

                if (best.TryGetValue(current.Location, out SimDuration known) && known < current.Cost)
                {
                    continue;
                }

                IReadOnlyList<TravelConnection> edges = ConnectionsFrom(current.Location);
                for (int i = 0; i < edges.Count; i++)
                {
                    TravelConnection edge = edges[i];
                    SimDuration candidate = current.Cost.Plus(edge.Cost);
                    if (best.TryGetValue(edge.To, out SimDuration existing) && existing <= candidate)
                    {
                        continue;
                    }

                    best[edge.To] = candidate;
                    cameFrom[edge.To] = edge;
                    frontier.Add(new FrontierEntry(candidate, edge.To));
                }
            }

            return false;
        }

        private static TravelPlan BuildPlan(LocationId origin, LocationId destination, Dictionary<LocationId, TravelConnection> cameFrom)
        {
            var legs = new List<TravelConnection>();
            LocationId cursor = destination;
            while (cursor != origin)
            {
                TravelConnection leg = cameFrom[cursor];
                legs.Add(leg);
                cursor = leg.From;
            }

            legs.Reverse();
            return new TravelPlan(origin, destination, legs);
        }

        private readonly struct FrontierEntry : IComparable<FrontierEntry>
        {
            public FrontierEntry(SimDuration cost, LocationId location)
            {
                Cost = cost;
                Location = location;
            }

            public SimDuration Cost { get; }

            public LocationId Location { get; }

            public int CompareTo(FrontierEntry other)
            {
                int byCost = Cost.CompareTo(other.Cost);
                return byCost != 0 ? byCost : Location.CompareTo(other.Location);
            }
        }
    }
}
