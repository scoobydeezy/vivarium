using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Immutable content description of a decision type (§6, §41).
    /// <para>
    /// Everything here that can affect an outcome is snapshotted onto the runtime
    /// <see cref="Decision"/> at construction, so hot-reloading this definition changes future
    /// decisions only (§42.1, invariant 73).
    /// </para>
    /// </summary>
    public sealed class DecisionDefinition
    {
        private static readonly DecisionDependencyKey[] NoDependencies = new DecisionDependencyKey[0];

        public DecisionDefinition(
            AuthoredId id,
            IReadOnlyList<DecisionOption> options,
            SimDuration timeToResolve,
            AuthoredId conflictScopeKind = default,
            int importance = 0,
            bool holdEligible = true,
            IReadOnlyList<DecisionDependencyKey> dependencyTemplates = null,
            bool hotReloadSafe = true)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            if (options == null || options.Count < 2)
            {
                throw new ArgumentException("A decision definition needs at least two options.", nameof(options));
            }

            Id = id;
            Options = options;
            TimeToResolve = timeToResolve;
            ConflictScopeKind = conflictScopeKind;
            Importance = importance;
            HoldEligible = holdEligible;
            DependencyTemplates = dependencyTemplates ?? NoDependencies;
            HotReloadSafe = hotReloadSafe;
        }

        public AuthoredId Id { get; }

        public IReadOnlyList<DecisionOption> Options { get; }

        /// <summary>How long the character takes to decide if nothing intervenes.</summary>
        public SimDuration TimeToResolve { get; }

        /// <summary>
        /// The exclusivity scope kind this decision claims, e.g. <c>conflict_scope.employment</c>.
        /// Unset means it never blocks another decision (§17.1).
        /// </summary>
        public AuthoredId ConflictScopeKind { get; }

        public int Importance { get; }

        /// <summary>Whether the player may hold this decision rather than let it auto-resolve (§20).</summary>
        public bool HoldEligible { get; }

        /// <summary>
        /// The dependency contexts instances of this decision register, so world changes can find them
        /// (§17.2). Subjects are filled in per instance.
        /// </summary>
        public IReadOnlyList<DecisionDependencyKey> DependencyTemplates { get; }

        public bool HotReloadSafe { get; }

        public override string ToString() => Id.ToString();
    }
}
