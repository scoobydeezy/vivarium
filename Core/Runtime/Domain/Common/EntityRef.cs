using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// A durable historical reference to a runtime entity (§7.1).
    /// <para>
    /// A runtime id is never reassigned, so an <see cref="EntityRef"/> stays meaningful after the
    /// entity leaves its active repository — a dead character, an ended employment, a demolished
    /// location. Resolving a ref may therefore fail while the reference itself remains valid.
    /// </para>
    /// </summary>
    public readonly struct EntityRef : IEquatable<EntityRef>, IComparable<EntityRef>
    {
        public static readonly EntityRef None = default;

        public EntityRef(EntityKind kind, int runtimeId)
        {
            Kind = kind;
            RuntimeId = runtimeId;
        }

        public EntityKind Kind { get; }

        public int RuntimeId { get; }

        public bool IsSet => Kind != EntityKind.None && RuntimeId > 0;

        public bool Equals(EntityRef other) => Kind == other.Kind && RuntimeId == other.RuntimeId;

        public override bool Equals(object obj) => obj is EntityRef other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ RuntimeId;

        public int CompareTo(EntityRef other)
        {
            int byKind = ((int)Kind).CompareTo((int)other.Kind);
            return byKind != 0 ? byKind : RuntimeId.CompareTo(other.RuntimeId);
        }

        public override string ToString() => IsSet ? $"{Kind}#{RuntimeId}" : "<none>";

        public static bool operator ==(EntityRef left, EntityRef right) => left.Equals(right);

        public static bool operator !=(EntityRef left, EntityRef right) => !left.Equals(right);
    }
}
