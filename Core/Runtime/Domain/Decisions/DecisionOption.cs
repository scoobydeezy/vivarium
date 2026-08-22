using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// One branch of a Decision: <c>TAKE JOB</c>, <c>STAY</c> (§17).
    /// <para>
    /// Options are authored content; the influences arguing for them are world truth constructed at
    /// runtime. <see cref="OrderIndex"/> exists so tie-breaking during resolution is explicit rather
    /// than dependent on collection order (§15).
    /// </para>
    /// </summary>
    public sealed class DecisionOption
    {
        public DecisionOption(AuthoredId id, AuthoredId labelId, int orderIndex)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An option needs a stable authored id (§7).", nameof(id));
            }

            Id = id;
            LabelId = labelId;
            OrderIndex = orderIndex;
        }

        /// <summary>Authored option id, e.g. <c>option.accept</c>.</summary>
        public AuthoredId Id { get; }

        /// <summary>Authored display label id. Presentation resolves the actual text.</summary>
        public AuthoredId LabelId { get; }

        /// <summary>Deterministic tie-break order among options.</summary>
        public int OrderIndex { get; }

        public override string ToString() => Id.ToString();
    }

    /// <summary>An intervention the player has already spent on a Decision (§19).</summary>
    public readonly struct AppliedIntervention
    {
        public AppliedIntervention(AuthoredId interventionDefinitionId, DecisionInfluenceId targetInfluenceId, long commandSequence)
        {
            InterventionDefinitionId = interventionDefinitionId;
            TargetInfluenceId = targetInfluenceId;
            CommandSequence = commandSequence;
        }

        public AuthoredId InterventionDefinitionId { get; }

        /// <summary>
        /// Bound to a stable influence identity, never a collection position — so a world change that
        /// reorders the influence set cannot silently retarget this (§17.2, invariant 37).
        /// </summary>
        public DecisionInfluenceId TargetInfluenceId { get; }

        /// <summary>The external command that applied it, for diagnostics and traces (§53).</summary>
        public long CommandSequence { get; }

        public override string ToString() => $"{InterventionDefinitionId} → {TargetInfluenceId} (cmd {CommandSequence})";
    }
}
