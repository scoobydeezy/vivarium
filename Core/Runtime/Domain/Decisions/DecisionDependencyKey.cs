using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// A world context an active Decision's influences depend on (§17.2).
    /// <para>
    /// Example: a decision about moving in with Darius depends on <c>housing_market</c> in the relevant
    /// district and on the relationship itself. When a new apartment opens, the index maps that change
    /// back to this Decision — no global scan of every open Decision (invariant 38).
    /// </para>
    /// </summary>
    public readonly struct DecisionDependencyKey : IEquatable<DecisionDependencyKey>, IComparable<DecisionDependencyKey>
    {
        public DecisionDependencyKey(AuthoredId contextKind, EntityRef subject = default)
        {
            ContextKind = contextKind;
            Subject = subject;
        }

        /// <summary>Authored context kind, e.g. <c>decision_context.housing_market</c>.</summary>
        public AuthoredId ContextKind { get; }

        /// <summary>Optional subject narrowing the context to one entity.</summary>
        public EntityRef Subject { get; }

        public bool IsSet => ContextKind.IsSet;

        public bool Equals(DecisionDependencyKey other) =>
            ContextKind.Equals(other.ContextKind) && Subject.Equals(other.Subject);

        public override bool Equals(object obj) => obj is DecisionDependencyKey other && Equals(other);

        public override int GetHashCode() => (ContextKind.GetHashCode() * 397) ^ Subject.GetHashCode();

        public int CompareTo(DecisionDependencyKey other)
        {
            int byKind = ContextKind.CompareTo(other.ContextKind);
            return byKind != 0 ? byKind : Subject.CompareTo(other.Subject);
        }

        public override string ToString() => Subject.IsSet ? $"{ContextKind}@{Subject}" : ContextKind.ToString();
    }

    /// <summary>
    /// A conflict scope declared by a Decision Definition (§17.1).
    /// <para>
    /// Forbidding concurrent decisions globally would be artificial — Mina can weigh a promotion and
    /// Glen's birthday at once. But two mutually exclusive employment decisions must not coexist, so
    /// definitions declare a scope like <c>Employment:Mina</c> and only that scope is exclusive
    /// (invariant 35).
    /// </para>
    /// </summary>
    public readonly struct DecisionConflictScope : IEquatable<DecisionConflictScope>, IComparable<DecisionConflictScope>
    {
        public static readonly DecisionConflictScope None = default;

        public DecisionConflictScope(AuthoredId scopeKind, EntityRef subject = default)
        {
            ScopeKind = scopeKind;
            Subject = subject;
        }

        /// <summary>Authored scope kind, e.g. <c>conflict_scope.employment</c>.</summary>
        public AuthoredId ScopeKind { get; }

        /// <summary>The entity the exclusivity applies to, usually the deciding character.</summary>
        public EntityRef Subject { get; }

        public bool IsSet => ScopeKind.IsSet;

        public bool Equals(DecisionConflictScope other) =>
            ScopeKind.Equals(other.ScopeKind) && Subject.Equals(other.Subject);

        public override bool Equals(object obj) => obj is DecisionConflictScope other && Equals(other);

        public override int GetHashCode() => (ScopeKind.GetHashCode() * 397) ^ Subject.GetHashCode();

        public int CompareTo(DecisionConflictScope other)
        {
            int byKind = ScopeKind.CompareTo(other.ScopeKind);
            return byKind != 0 ? byKind : Subject.CompareTo(other.Subject);
        }

        public override string ToString() => Subject.IsSet ? $"{ScopeKind}:{Subject}" : ScopeKind.ToString();
    }
}
