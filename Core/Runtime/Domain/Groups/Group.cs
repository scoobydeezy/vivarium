using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Groups
{
    /// <summary>
    /// A non-spatial social or organizational grouping (§31): household, employer, club, friend group,
    /// team, school, faction.
    /// <para>
    /// Deliberately outside the location hierarchy. Spatial containment is a tree and a character
    /// occupies one place; group membership is many-to-many and must not be forced into that tree
    /// (invariant 52).
    /// </para>
    /// </summary>
    public sealed class Group
    {
        public Group(GroupId id, AuthoredId kind, string displayName, LocationId primaryLocationId = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A group needs an allocated runtime id (§7).", nameof(id));
            }

            Id = id;
            Kind = kind;
            DisplayName = displayName;
            PrimaryLocationId = primaryLocationId;
        }

        public GroupId Id { get; }

        /// <summary>Authored kind, e.g. <c>group.household</c> or <c>group.employer</c>.</summary>
        public AuthoredId Kind { get; }

        public string DisplayName { get; }

        /// <summary>
        /// Where the group is based, when that is meaningful — a workplace, a home. Optional: a friend
        /// group has no location.
        /// </summary>
        public LocationId PrimaryLocationId { get; }

        public override string ToString() => $"{DisplayName} ({Kind} {Id})";
    }

    /// <summary>Authored group kinds.</summary>
    public static class GroupKinds
    {
        public static readonly AuthoredId Household = new AuthoredId("group.household");
        public static readonly AuthoredId Employer = new AuthoredId("group.employer");
        public static readonly AuthoredId Club = new AuthoredId("group.club");
        public static readonly AuthoredId FriendGroup = new AuthoredId("group.friend_group");
        public static readonly AuthoredId School = new AuthoredId("group.school");
        public static readonly AuthoredId Faction = new AuthoredId("group.faction");
    }
}
