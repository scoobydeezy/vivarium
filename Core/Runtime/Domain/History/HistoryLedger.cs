using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.History
{
    /// <summary>
    /// Retained domain history with explicit retention policy (§37, invariant 63).
    /// <para>
    /// Only <see cref="RetentionTier.Significant"/> and <see cref="RetentionTier.Legacy"/> entries are
    /// save state; ephemeral and recent entries are allowed to disappear. Knowledge entries hold only
    /// weak references here, so pruning can never invalidate what the player knows (§23.1).
    /// </para>
    /// </summary>
    public sealed class HistoryLedger
    {
        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly IIdAllocator<HistoryEntryId> _ids;

        public HistoryLedger(IIdAllocator<HistoryEntryId> ids)
        {
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }

        public int Count => _entries.Count;

        /// <summary>Entries in occurrence order (append order is chronological by construction).</summary>
        public IReadOnlyList<HistoryEntry> Entries => _entries;

        public HistoryEntry Record(
            AuthoredId kind,
            SimTime occurredAt,
            RetentionTier tier,
            string summary,
            IReadOnlyList<EntityRef> subjects = null)
        {
            var entry = new HistoryEntry(_ids.Next(), kind, occurredAt, tier, summary, subjects);
            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// Reinstates a saved entry with its original id, without minting a new one (§7.1, §38).
        /// </summary>
        public void Restore(HistoryEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _entries.Add(entry);
        }

        public bool TryGet(HistoryEntryId id, out HistoryEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Drops entries at or below <paramref name="maxTierToPrune"/> that are older than
        /// <paramref name="olderThan"/>. Called from a bookkeeping-phase scheduled event, not per frame.
        /// </summary>
        public int Prune(SimTime olderThan, RetentionTier maxTierToPrune = RetentionTier.Ephemeral)
        {
            int removed = 0;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                HistoryEntry entry = _entries[i];
                if (entry.OccurredAt < olderThan && (int)entry.Tier <= (int)maxTierToPrune)
                {
                    _entries.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>Entries that must be persisted (§38).</summary>
        public IEnumerable<HistoryEntry> SignificantAndLegacy
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Tier == RetentionTier.Significant || _entries[i].Tier == RetentionTier.Legacy)
                    {
                        yield return _entries[i];
                    }
                }
            }
        }
    }
}
