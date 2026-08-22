using System;
using System.Collections.Generic;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Generic bidirectional membership bookkeeping (§30, §31, §54).
    /// <para>
    /// Maintains both directions on write so neither <c>container → members</c> nor
    /// <c>member → containers</c> ever requires a population scan. Enumeration is sorted, so no
    /// authoritative behaviour can accidentally depend on hash iteration order (§15).
    /// </para>
    /// <para>
    /// This is a primitive, not a domain concept: spatial occupancy and social group membership both
    /// build on it while keeping their own distinct semantics.
    /// </para>
    /// </summary>
    public sealed class IndexedMembership<TContainer, TMember>
        where TContainer : IComparable<TContainer>
        where TMember : IComparable<TMember>
    {
        private static readonly IReadOnlyList<TMember> EmptyMembers = new TMember[0];
        private static readonly IReadOnlyList<TContainer> EmptyContainers = new TContainer[0];

        private readonly SortedDictionary<TContainer, SortedSet<TMember>> _membersByContainer =
            new SortedDictionary<TContainer, SortedSet<TMember>>(Comparer<TContainer>.Default);

        private readonly SortedDictionary<TMember, SortedSet<TContainer>> _containersByMember =
            new SortedDictionary<TMember, SortedSet<TContainer>>(Comparer<TMember>.Default);

        public bool Add(TContainer container, TMember member)
        {
            if (!_membersByContainer.TryGetValue(container, out SortedSet<TMember> members))
            {
                members = new SortedSet<TMember>(Comparer<TMember>.Default);
                _membersByContainer.Add(container, members);
            }

            if (!members.Add(member))
            {
                return false;
            }

            if (!_containersByMember.TryGetValue(member, out SortedSet<TContainer> containers))
            {
                containers = new SortedSet<TContainer>(Comparer<TContainer>.Default);
                _containersByMember.Add(member, containers);
            }

            containers.Add(container);
            return true;
        }

        public bool Remove(TContainer container, TMember member)
        {
            if (!_membersByContainer.TryGetValue(container, out SortedSet<TMember> members) || !members.Remove(member))
            {
                return false;
            }

            if (members.Count == 0)
            {
                _membersByContainer.Remove(container);
            }

            if (_containersByMember.TryGetValue(member, out SortedSet<TContainer> containers))
            {
                containers.Remove(container);
                if (containers.Count == 0)
                {
                    _containersByMember.Remove(member);
                }
            }

            return true;
        }

        /// <summary>Removes a member from every container it belongs to.</summary>
        public void RemoveMember(TMember member)
        {
            if (!_containersByMember.TryGetValue(member, out SortedSet<TContainer> containers))
            {
                return;
            }

            foreach (TContainer container in new List<TContainer>(containers))
            {
                Remove(container, member);
            }
        }

        public bool Contains(TContainer container, TMember member) =>
            _membersByContainer.TryGetValue(container, out SortedSet<TMember> members) && members.Contains(member);

        /// <summary>Members of a container, ascending. Empty when the container is unknown.</summary>
        public IReadOnlyCollection<TMember> MembersOf(TContainer container) =>
            _membersByContainer.TryGetValue(container, out SortedSet<TMember> members)
                ? (IReadOnlyCollection<TMember>)members
                : (IReadOnlyCollection<TMember>)EmptyMembers;

        /// <summary>Containers a member belongs to, ascending. Empty when the member is unknown.</summary>
        public IReadOnlyCollection<TContainer> ContainersOf(TMember member) =>
            _containersByMember.TryGetValue(member, out SortedSet<TContainer> containers)
                ? (IReadOnlyCollection<TContainer>)containers
                : (IReadOnlyCollection<TContainer>)EmptyContainers;

        public int CountIn(TContainer container) =>
            _membersByContainer.TryGetValue(container, out SortedSet<TMember> members) ? members.Count : 0;

        /// <summary>Containers holding at least one member, ascending.</summary>
        public IEnumerable<TContainer> Containers => _membersByContainer.Keys;

        public void Clear()
        {
            _membersByContainer.Clear();
            _containersByMember.Clear();
        }
    }
}
