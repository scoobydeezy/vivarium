using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// An aspect-scoped revision address: <c>(entity, aspect)</c> (§11.2.1).
    /// <para>
    /// A monolithic per-entity revision counter is <b>prohibited</b> for normal event invalidation,
    /// because bumping it on every change invalidates pending events that had no logical dependency
    /// on what actually changed. Scope revisions as narrowly as the dependency they protect.
    /// </para>
    /// </summary>
    public readonly struct RevisionKey : IEquatable<RevisionKey>, IComparable<RevisionKey>
    {
        public RevisionKey(EntityRef subject, AuthoredId aspect)
        {
            Subject = subject;
            Aspect = aspect;
        }

        public RevisionKey(EntityKind kind, int runtimeId, AuthoredId aspect)
            : this(new EntityRef(kind, runtimeId), aspect)
        {
        }

        public EntityRef Subject { get; }

        /// <summary>Authored aspect id, e.g. <c>revision.schedule</c> or <c>revision.needs.hunger</c>.</summary>
        public AuthoredId Aspect { get; }

        public bool Equals(RevisionKey other) => Subject.Equals(other.Subject) && Aspect.Equals(other.Aspect);

        public override bool Equals(object obj) => obj is RevisionKey other && Equals(other);

        public override int GetHashCode() => (Subject.GetHashCode() * 397) ^ Aspect.GetHashCode();

        public int CompareTo(RevisionKey other)
        {
            int bySubject = Subject.CompareTo(other.Subject);
            return bySubject != 0 ? bySubject : Aspect.CompareTo(other.Aspect);
        }

        public override string ToString() => $"{Subject}.{Aspect}";
    }

    /// <summary>
    /// Authored aspect ids. Add new aspects here rather than inventing ad-hoc strings at call sites,
    /// so the set of protected dependencies stays reviewable.
    /// </summary>
    public static class RevisionAspects
    {
        /// <summary>A character's planned schedule / commitment plan.</summary>
        public static readonly AuthoredId Schedule = new AuthoredId("revision.schedule");

        /// <summary>A character's current primary activity (§29.1).</summary>
        public static readonly AuthoredId Activity = new AuthoredId("revision.activity");

        /// <summary>Progression parameters of one need. Combine with the need's authored id.</summary>
        public static readonly AuthoredId Need = new AuthoredId("revision.need");

        /// <summary>Employment membership and terms.</summary>
        public static readonly AuthoredId Employment = new AuthoredId("revision.employment");

        /// <summary>Relationship standing.</summary>
        public static readonly AuthoredId Relationship = new AuthoredId("revision.relationship");

        /// <summary>A character's ground-truth latent personality.</summary>
        public static readonly AuthoredId Personality = new AuthoredId("revision.social.personality");

        /// <summary>An observer's appraisal field for one lens. Scope with the lens id.</summary>
        public static readonly AuthoredId AppraisalField = new AuthoredId("revision.social.appraisal_field");

        /// <summary>An observer's belief distribution about one target.</summary>
        public static readonly AuthoredId SocialBelief = new AuthoredId("revision.social.belief");

        /// <summary>A decision's influence set (§17.2).</summary>
        public static readonly AuthoredId DecisionInfluence = new AuthoredId("revision.decision.influence");

        /// <summary>Occupancy / spatial containment of a location.</summary>
        public static readonly AuthoredId Occupancy = new AuthoredId("revision.occupancy");

        /// <summary>Composes a per-instance aspect such as <c>revision.need:need.hunger</c>.</summary>
        public static AuthoredId Scoped(AuthoredId aspect, AuthoredId qualifier) =>
            new AuthoredId(aspect.Value + ":" + qualifier.Value);
    }
}
