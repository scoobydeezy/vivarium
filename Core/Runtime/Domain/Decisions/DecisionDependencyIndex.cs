using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    public readonly struct DecisionReasoningRoute : IEquatable<DecisionReasoningRoute>, IComparable<DecisionReasoningRoute>
    {
        public DecisionReasoningRoute(DecisionId decisionId, AuthoredId bindingId, AuthoredId optionId)
        {
            DecisionId = decisionId;
            BindingId = bindingId;
            OptionId = optionId;
        }

        public DecisionId DecisionId { get; }
        public AuthoredId BindingId { get; }
        public AuthoredId OptionId { get; }
        public bool Equals(DecisionReasoningRoute other) => DecisionId == other.DecisionId && BindingId == other.BindingId && OptionId == other.OptionId;
        public override bool Equals(object obj) => obj is DecisionReasoningRoute other && Equals(other);
        public override int GetHashCode() => ((DecisionId.GetHashCode() * 397) ^ BindingId.GetHashCode()) * 397 ^ OptionId.GetHashCode();
        public int CompareTo(DecisionReasoningRoute other)
        {
            int decision = DecisionId.CompareTo(other.DecisionId);
            if (decision != 0) return decision;
            int binding = BindingId.CompareTo(other.BindingId);
            return binding != 0 ? binding : OptionId.CompareTo(other.OptionId);
        }
    }

    public readonly struct DecisionReasoningDependencyRoute
    {
        public DecisionReasoningDependencyRoute(DecisionDependencyKey dependency, DecisionReasoningRoute route)
        {
            Dependency = dependency;
            Route = route;
        }

        public DecisionDependencyKey Dependency { get; }
        public DecisionReasoningRoute Route { get; }
    }

    /// <summary>
    /// Maps world contexts back to the active Decisions they can affect (§17.2).
    /// <para>
    /// This is what makes living decisions affordable. Without it, every unrelated world change would
    /// have to scan every open Decision — the exact pattern §50 forbids. With it, "a new apartment
    /// opened in this district" reaches only the decisions that registered an interest (invariant 38).
    /// </para>
    /// <para>
    /// Rebuildable from the active decisions after load (§40).
    /// </para>
    /// </summary>
    public sealed class DecisionDependencyIndex
    {
        private readonly IndexedMembership<DecisionDependencyKey, DecisionId> _index =
            new IndexedMembership<DecisionDependencyKey, DecisionId>();
        private readonly IndexedMembership<DecisionDependencyKey, DecisionReasoningRoute> _reasoningRoutes =
            new IndexedMembership<DecisionDependencyKey, DecisionReasoningRoute>();
        private readonly SortedDictionary<DecisionId, SortedSet<DecisionReasoningDependencyRoute>> _routesByDecision =
            new SortedDictionary<DecisionId, SortedSet<DecisionReasoningDependencyRoute>>();

        /// <summary>Registers every dependency the decision currently declares.</summary>
        public void Register(Decision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            foreach (DecisionDependencyKey key in decision.DependencyKeys)
            {
                _index.Add(key, decision.Id);
            }
        }

        public void RegisterDependency(DecisionDependencyKey key, DecisionId decision)
        {
            if (key.IsSet)
            {
                _index.Add(key, decision);
            }
        }

        /// <summary>Drops a decision from the index — call on resolution, expiry, or supersession.</summary>
        public void Unregister(DecisionId decision)
        {
            _index.RemoveMember(decision);
            ClearReasoningRoutes(decision);
        }

        /// <summary>
        /// Active decisions that may need reevaluation because <paramref name="key"/> changed.
        /// Ascending by DecisionId, so reevaluation order is deterministic (§15).
        /// </summary>
        public IReadOnlyCollection<DecisionId> DecisionsDependingOn(DecisionDependencyKey key) => _index.MembersOf(key);

        public IReadOnlyCollection<DecisionReasoningRoute> ReasoningRoutesDependingOn(DecisionDependencyKey key) =>
            _reasoningRoutes.MembersOf(key);

        public void ReplaceReasoningRoutes(
            Decision decision,
            IReadOnlyList<DecisionReasoningDependencyRoute> routes)
        {
            ClearReasoningRoutes(decision.Id);
            AddReasoningRoutes(decision, routes);
        }

        public void ReplaceReasoningRoutes(
            Decision decision,
            IReadOnlyList<DecisionReasoningDependencyRoute> routes,
            IReadOnlyCollection<DecisionReasoningRoute> scopes)
        {
            if (_routesByDecision.TryGetValue(decision.Id, out SortedSet<DecisionReasoningDependencyRoute> owned))
            {
                var selected = new SortedSet<DecisionReasoningRoute>(scopes);
                var removing = new List<DecisionReasoningDependencyRoute>();
                foreach (DecisionReasoningDependencyRoute route in owned)
                {
                    if (selected.Contains(route.Route)) removing.Add(route);
                }
                for (int i = 0; i < removing.Count; i++)
                {
                    owned.Remove(removing[i]);
                    _reasoningRoutes.Remove(removing[i].Dependency, removing[i].Route);
                }
                if (owned.Count == 0) _routesByDecision.Remove(decision.Id);
            }
            AddReasoningRoutes(decision, routes);
        }

        private void AddReasoningRoutes(Decision decision, IReadOnlyList<DecisionReasoningDependencyRoute> routes)
        {
            if (!_routesByDecision.TryGetValue(decision.Id, out SortedSet<DecisionReasoningDependencyRoute> owned))
            {
                owned = new SortedSet<DecisionReasoningDependencyRoute>(
                    Comparer<DecisionReasoningDependencyRoute>.Create((a, b) =>
                    {
                        int dependency = a.Dependency.CompareTo(b.Dependency);
                        return dependency != 0 ? dependency : a.Route.CompareTo(b.Route);
                    }));
            }
            for (int i = 0; i < routes.Count; i++)
            {
                DecisionReasoningDependencyRoute route = routes[i];
                if (!route.Dependency.IsSet) continue;
                owned.Add(route);
                _reasoningRoutes.Add(route.Dependency, route.Route);
                _index.Add(route.Dependency, decision.Id);
                decision.RegisterDependency(route.Dependency);
            }
            if (owned.Count > 0) _routesByDecision[decision.Id] = owned;
        }

        private void ClearReasoningRoutes(DecisionId decision)
        {
            if (!_routesByDecision.TryGetValue(decision, out SortedSet<DecisionReasoningDependencyRoute> owned)) return;
            foreach (DecisionReasoningDependencyRoute route in owned)
            {
                _reasoningRoutes.Remove(route.Dependency, route.Route);
            }
            _routesByDecision.Remove(decision);
        }

        public void Clear()
        {
            _index.Clear();
            _reasoningRoutes.Clear();
            _routesByDecision.Clear();
        }
    }
}
