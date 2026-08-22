using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Spatial
{
    /// <summary>
    /// The containment tree (§27) with efficient ancestor traversal (§30).
    /// <para>
    /// Ancestor chains are cached because "who is in this building?" and "how many residents are in
    /// this settlement?" must not walk the population or rebuild parent chains per query (§50).
    /// The cache is rebuildable from canonical state after load (§40).
    /// </para>
    /// </summary>
    public sealed class LocationHierarchy
    {
        private static readonly LocationId[] NoAncestors = new LocationId[0];

        private readonly EntityRepository<LocationId, LocationNode> _nodes = new EntityRepository<LocationId, LocationNode>("Location");
        private readonly IndexedMembership<LocationId, LocationId> _children = new IndexedMembership<LocationId, LocationId>();
        private readonly Dictionary<LocationId, LocationId[]> _ancestorCache = new Dictionary<LocationId, LocationId[]>();

        public IReadOnlyEntityRepository<LocationId, LocationNode> Nodes => _nodes;

        public int Count => _nodes.Count;

        public void Add(LocationNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.ParentLocationId.IsSet && !_nodes.Contains(node.ParentLocationId))
            {
                throw new InvalidOperationException($"Parent {node.ParentLocationId} must exist before adding {node.Id}.");
            }

            _nodes.Add(node.Id, node);
            if (node.ParentLocationId.IsSet)
            {
                _children.Add(node.ParentLocationId, node.Id);
            }

            _ancestorCache.Clear();
        }

        public bool TryGet(LocationId id, out LocationNode node) => _nodes.TryGet(id, out node);

        public LocationNode Get(LocationId id) => _nodes.Get(id);

        /// <summary>Direct children, ascending.</summary>
        public IReadOnlyCollection<LocationId> ChildrenOf(LocationId id) => _children.MembersOf(id);

        /// <summary>
        /// Ancestors from immediate parent up to the root, cached per location. Empty for the root.
        /// </summary>
        public IReadOnlyList<LocationId> AncestorsOf(LocationId id)
        {
            if (_ancestorCache.TryGetValue(id, out LocationId[] cached))
            {
                return cached;
            }

            if (!_nodes.TryGet(id, out LocationNode node))
            {
                return NoAncestors;
            }

            var chain = new List<LocationId>();
            LocationId parent = node.ParentLocationId;
            while (parent.IsSet && _nodes.TryGet(parent, out LocationNode parentNode))
            {
                chain.Add(parent);
                parent = parentNode.ParentLocationId;
            }

            LocationId[] result = chain.ToArray();
            _ancestorCache[id] = result;
            return result;
        }

        /// <summary>Whether <paramref name="descendant"/> sits anywhere beneath <paramref name="ancestor"/>.</summary>
        public bool IsDescendantOf(LocationId descendant, LocationId ancestor)
        {
            IReadOnlyList<LocationId> chain = AncestorsOf(descendant);
            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i] == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Rebuilds derived caches after a load (§40).</summary>
        public void RebuildCaches() => _ancestorCache.Clear();
    }
}
