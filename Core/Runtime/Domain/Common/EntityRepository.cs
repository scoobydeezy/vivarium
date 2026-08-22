using System;
using System.Collections.Generic;

namespace Vivarium.Domain.Common
{
    /// <summary>Read side of an entity repository. Application/query code should depend on this.</summary>
    public interface IReadOnlyEntityRepository<TId, TEntity>
        where TId : IComparable<TId>
    {
        int Count { get; }

        bool Contains(TId id);

        bool TryGet(TId id, out TEntity entity);

        TEntity Get(TId id);

        /// <summary>All entities in ascending id order — the canonical deterministic traversal (§15).</summary>
        IEnumerable<TEntity> All { get; }

        IEnumerable<TId> Ids { get; }
    }

    /// <summary>
    /// Id-keyed storage for one authoritative entity family.
    /// <para>
    /// Backed by a sorted map on purpose: every enumeration is ordered by runtime id, so simulation
    /// systems cannot accidentally inherit non-deterministic hash traversal order (§15). Removing an
    /// entity retires it from active simulation but never releases its identity (§7.1).
    /// </para>
    /// </summary>
    public sealed class EntityRepository<TId, TEntity> : IReadOnlyEntityRepository<TId, TEntity>
        where TId : IComparable<TId>
    {
        private readonly SortedDictionary<TId, TEntity> _entities =
            new SortedDictionary<TId, TEntity>(Comparer<TId>.Default);

        private readonly string _entityName;

        public EntityRepository(string entityName = null)
        {
            _entityName = entityName ?? typeof(TEntity).Name;
        }

        public int Count => _entities.Count;

        public bool Contains(TId id) => _entities.ContainsKey(id);

        public bool TryGet(TId id, out TEntity entity) => _entities.TryGetValue(id, out entity);

        public TEntity Get(TId id) => _entities.TryGetValue(id, out TEntity entity)
            ? entity
            : throw new KeyNotFoundException($"No active {_entityName} with id {id}. Note that an absent id may still be a valid historical reference (§7.1).");

        public void Add(TId id, TEntity entity)
        {
            if (_entities.ContainsKey(id))
            {
                throw new InvalidOperationException($"{_entityName} {id} already exists; runtime ids are never reused (§7.1).");
            }

            _entities.Add(id, entity);
        }

        public void Set(TId id, TEntity entity) => _entities[id] = entity;

        /// <summary>Retires an entity from active simulation. Its identity remains spent forever.</summary>
        public bool Remove(TId id) => _entities.Remove(id);

        public IEnumerable<TEntity> All => _entities.Values;

        public IEnumerable<TId> Ids => _entities.Keys;

        public void Clear() => _entities.Clear();
    }
}
