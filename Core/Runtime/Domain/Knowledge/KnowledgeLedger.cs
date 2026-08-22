using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Everything the player has observed or learned (§22, §23).
    /// <para>
    /// This is the <b>player knowledge</b> model — one of the three concepts that must never collapse
    /// into each other: world truth lives in the domain entities, presentation lives in read models,
    /// and this sits between them (§2.3).
    /// </para>
    /// <para>
    /// Authoritative save state (§38): it cannot be rebuilt from truth, because it is precisely a
    /// record of what truth <i>used to look like</i> from the player's side.
    /// </para>
    /// </summary>
    public sealed class KnowledgeLedger
    {
        private readonly SortedDictionary<FactKey, KnowledgeEntry> _entries =
            new SortedDictionary<FactKey, KnowledgeEntry>();

        public int Count => _entries.Count;

        /// <summary>Whether the player knows anything at all about this fact.</summary>
        public bool Knows(FactKey key) => _entries.ContainsKey(key);

        public bool TryGet(FactKey key, out KnowledgeEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>
        /// Records an observation, replacing any earlier entry for the same fact. Overwriting is
        /// correct: the ledger holds what the player currently believes, and history of belief changes
        /// belongs to History (§37) if it is ever needed.
        /// </summary>
        public void Record(KnowledgeEntry entry) => _entries[entry.Key] = entry;

        public bool Forget(FactKey key) => _entries.Remove(key);

        /// <summary>All entries in deterministic fact-key order.</summary>
        public IEnumerable<KnowledgeEntry> All => _entries.Values;

        /// <summary>Entries about one subject — the backbone of knowledge-filtered projections (§35).</summary>
        public IEnumerable<KnowledgeEntry> About(EntityRef subject)
        {
            foreach (KeyValuePair<FactKey, KnowledgeEntry> pair in _entries)
            {
                if (pair.Key.Subject.Equals(subject))
                {
                    yield return pair.Value;
                }
            }
        }
    }
}
