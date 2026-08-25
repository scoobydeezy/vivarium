using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Activities;

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
        private readonly SortedDictionary<AuthoredId, DecisionParameterValue> _context =
            new SortedDictionary<AuthoredId, DecisionParameterValue>();

        public DecisionOption(
            AuthoredId id,
            AuthoredId labelId,
            int orderIndex,
            IReadOnlyDictionary<AuthoredId, DecisionParameterValue> context = null,
            CommitmentResolutionPlan commitmentResolutionPlan = null)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An option needs a stable authored id (§7).", nameof(id));
            }

            Id = id;
            LabelId = labelId;
            OrderIndex = orderIndex;
            CommitmentResolutionPlan = commitmentResolutionPlan?.Copy();
            if (context != null)
            {
                foreach (KeyValuePair<AuthoredId, DecisionParameterValue> pair in context) _context[pair.Key] = pair.Value;
            }
        }

        /// <summary>Authored option id, e.g. <c>option.accept</c>.</summary>
        public AuthoredId Id { get; }

        /// <summary>Authored display label id. Presentation resolves the actual text.</summary>
        public AuthoredId LabelId { get; }

        /// <summary>Deterministic tie-break order among options.</summary>
        public int OrderIndex { get; }

        public IReadOnlyDictionary<AuthoredId, DecisionParameterValue> Context => _context;

        /// <summary>Optional authoritative plan payload for a commitment-conflict Option.</summary>
        public CommitmentResolutionPlan CommitmentResolutionPlan { get; }

        public void SetContext(AuthoredId parameterId, DecisionParameterValue value) => _context[parameterId] = value;

        public bool TryGetContext(AuthoredId parameterId, out DecisionParameterValue value) =>
            _context.TryGetValue(parameterId, out value);

        public DecisionOption Copy() => new DecisionOption(Id, LabelId, OrderIndex, _context, CommitmentResolutionPlan);

        public override string ToString() => Id.ToString();
    }

    /// <summary>An intervention the player has already spent on a Decision (§19).</summary>
    public readonly struct AppliedIntervention
    {
        public AppliedIntervention(
            AuthoredId interventionDefinitionId,
            DecisionInfluenceId targetInfluenceId,
            long commandSequence,
            InterventionKind kind = InterventionKind.Unknown,
            Die replacementDie = default,
            InterventionResourceKind resourceKind = InterventionResourceKind.None,
            int resourceCost = 0)
        {
            InterventionDefinitionId = interventionDefinitionId;
            TargetInfluenceId = targetInfluenceId;
            CommandSequence = commandSequence;
            Kind = kind;
            ReplacementDie = replacementDie;
            ResourceKind = resourceKind;
            ResourceCost = resourceCost;
        }

        public AuthoredId InterventionDefinitionId { get; }

        /// <summary>
        /// Bound to a stable influence identity, never a collection position — so a world change that
        /// reorders the influence set cannot silently retarget this (§17.2, invariant 37).
        /// </summary>
        public DecisionInfluenceId TargetInfluenceId { get; }

        /// <summary>The external command that applied it, for diagnostics and traces (§53).</summary>
        public long CommandSequence { get; }

        /// <summary>Snapshotted mechanical effect so content reload cannot reinterpret a spent action.</summary>
        public InterventionKind Kind { get; }

        public Die ReplacementDie { get; }

        /// <summary>Snapshotted spend policy so dissolution refunds do not depend on reloaded content.</summary>
        public InterventionResourceKind ResourceKind { get; }

        public int ResourceCost { get; }

        public override string ToString() => $"{InterventionDefinitionId} → {TargetInfluenceId} (cmd {CommandSequence})";
    }
}
