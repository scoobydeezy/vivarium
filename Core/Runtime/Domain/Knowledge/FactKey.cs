using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Identifies one discoverable item of world truth (§22).
    /// <para>
    /// Example: <c>Kind = fact.relationship.resentment</c>, <c>Subject = Relationship#52</c>. The key
    /// names <i>what could be known</i>; it is not a pointer to the current value. Truth may say
    /// "Strong" while the player's knowledge still says "Moderate" — and that staleness is intentional.
    /// </para>
    /// </summary>
    public readonly struct FactKey : IEquatable<FactKey>, IComparable<FactKey>
    {
        public FactKey(AuthoredId kind, EntityRef subject, AuthoredId qualifier = default)
        {
            Kind = kind;
            Subject = subject;
            Qualifier = qualifier;
        }

        /// <summary>Authored fact kind, e.g. <c>fact.character.trait</c> or <c>fact.relationship.resentment</c>.</summary>
        public AuthoredId Kind { get; }

        /// <summary>
        /// The entity the fact is about. A weak historical reference (§7.1): the subject may have been
        /// retired or compacted while the knowledge remains valid.
        /// </summary>
        public EntityRef Subject { get; }

        /// <summary>
        /// Optional discriminator when one subject has many facts of a kind — e.g. which trait, which
        /// need. <see cref="AuthoredId.None"/> when unused.
        /// </summary>
        public AuthoredId Qualifier { get; }

        public bool Equals(FactKey other) =>
            Kind.Equals(other.Kind) && Subject.Equals(other.Subject) && Qualifier.Equals(other.Qualifier);

        public override bool Equals(object obj) => obj is FactKey other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Kind.GetHashCode();
            hash = (hash * 397) ^ Subject.GetHashCode();
            return (hash * 397) ^ Qualifier.GetHashCode();
        }

        public int CompareTo(FactKey other)
        {
            int byKind = Kind.CompareTo(other.Kind);
            if (byKind != 0)
            {
                return byKind;
            }

            int bySubject = Subject.CompareTo(other.Subject);
            return bySubject != 0 ? bySubject : Qualifier.CompareTo(other.Qualifier);
        }

        public override string ToString() =>
            Qualifier.IsSet ? $"{Kind}[{Qualifier}]@{Subject}" : $"{Kind}@{Subject}";
    }
}
