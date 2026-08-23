using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.History
{
    /// <summary>
    /// Retention tier of a historical record (§37).
    /// <para>
    /// "Legacy" replaces the earlier notion of "Permanent" because <b>durability and storage
    /// granularity are separate concerns</b>: long-lived significance means the information survives
    /// while its representation compacts, not that raw history accumulates forever (invariant 64).
    /// </para>
    /// </summary>
    public enum RetentionTier
    {
        /// <summary>Discarded soon — "skipped bowling Tuesday".</summary>
        Ephemeral = 0,

        /// <summary>Kept while recent, then summarized.</summary>
        Recent = 1,

        /// <summary>Kept in detail because it mattered — "married Darius".</summary>
        Significant = 2,

        /// <summary>Compacted to a durable summary — "Mina married Darius in Year 14".</summary>
        Legacy = 3,
    }

    /// <summary>
    /// A retained record of something that happened (§37).
    /// <para>
    /// The lifecycle is Active → Resolved → Recent History → Summary → Pruned, and this entry moves
    /// down the tiers rather than living forever at full detail. That is what makes generational-scale
    /// simulation bounded (§1, §37).
    /// </para>
    /// </summary>
    public sealed class HistoryEntry
    {
        private static readonly EntityRef[] NoSubjects = new EntityRef[0];

        public HistoryEntry(
            HistoryEntryId id,
            AuthoredId kind,
            SimTime occurredAt,
            RetentionTier tier,
            string summary,
            IReadOnlyList<EntityRef> subjects = null,
            CommitmentOutcomeId sourceOutcomeId = default)
        {
            Id = id;
            Kind = kind;
            OccurredAt = occurredAt;
            Tier = tier;
            Summary = summary;
            Subjects = subjects ?? NoSubjects;
            SourceOutcomeId = sourceOutcomeId;
        }

        public HistoryEntryId Id { get; }

        /// <summary>Authored kind, e.g. <c>history.decision_resolved</c>.</summary>
        public AuthoredId Kind { get; }

        public SimTime OccurredAt { get; }

        public RetentionTier Tier { get; private set; }

        /// <summary>Human-readable summary. Survives compaction; detail may not.</summary>
        public string Summary { get; private set; }

        /// <summary>Who it involved. Weak references — subjects may since have been retired (§7.1).</summary>
        public IReadOnlyList<EntityRef> Subjects { get; }

        /// <summary>Weak provenance link; the denormalized summary remains meaningful if it expires.</summary>
        public CommitmentOutcomeId SourceOutcomeId { get; }

        /// <summary>Compacts the entry into a smaller representation at a lower fidelity (§37).</summary>
        public void CompactTo(RetentionTier tier, string compactedSummary)
        {
            Tier = tier;
            Summary = compactedSummary;
        }

        public override string ToString() => $"[{Tier}] {OccurredAt} {Kind}: {Summary}";
    }
}
