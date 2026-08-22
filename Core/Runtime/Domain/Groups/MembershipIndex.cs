using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Groups
{
    /// <summary>
    /// Group membership, queryable in both directions (§31).
    /// <para>
    /// <c>Character → Groups</c> and <c>Group → Members</c> are both O(result), never a population
    /// scan (§50). Shares the generic <see cref="IndexedMembership{TContainer,TMember}"/> bookkeeping
    /// primitive with spatial occupancy while keeping its own semantics (invariant 53).
    /// </para>
    /// <para>
    /// Rebuildable from canonical save state (§40).
    /// </para>
    /// </summary>
    public sealed class MembershipIndex
    {
        private readonly IndexedMembership<GroupId, CharacterId> _membership = new IndexedMembership<GroupId, CharacterId>();

        public bool Join(GroupId group, CharacterId character) => _membership.Add(group, character);

        public bool Leave(GroupId group, CharacterId character) => _membership.Remove(group, character);

        /// <summary>Removes a character from every group — used when they retire from active simulation.</summary>
        public void RemoveCharacter(CharacterId character) => _membership.RemoveMember(character);

        public bool IsMember(GroupId group, CharacterId character) => _membership.Contains(group, character);

        /// <summary>Members of a group, ascending.</summary>
        public IReadOnlyCollection<CharacterId> MembersOf(GroupId group) => _membership.MembersOf(group);

        /// <summary>Groups a character belongs to, ascending. A character may belong to many (§31).</summary>
        public IReadOnlyCollection<GroupId> GroupsOf(CharacterId character) => _membership.ContainersOf(character);

        public int MemberCount(GroupId group) => _membership.CountIn(group);

        /// <summary>Whether two characters share any group — a cheap shared-context check (§32).</summary>
        public bool ShareAnyGroup(CharacterId a, CharacterId b)
        {
            foreach (GroupId group in _membership.ContainersOf(a))
            {
                if (_membership.Contains(group, b))
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear() => _membership.Clear();
    }
}
