using System.Collections.Generic;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Stores aspect-scoped revision counters used for cheap stale-event detection (§11.2).
    /// <para>
    /// Revision checks are an <i>optimization</i>. Semantic validation in the handler remains
    /// authoritative — a matching revision never excuses skipping <c>CanExecute</c>.
    /// </para>
    /// </summary>
    public sealed class RevisionRegistry
    {
        // Iteration order of this dictionary is never used for authoritative decisions;
        // Snapshot() sorts explicitly when order matters (persistence, diagnostics).
        private readonly Dictionary<RevisionKey, int> _revisions;

        public RevisionRegistry()
        {
            _revisions = new Dictionary<RevisionKey, int>();
        }

        public RevisionRegistry(IEnumerable<KeyValuePair<RevisionKey, int>> restored)
            : this()
        {
            if (restored == null)
            {
                return;
            }

            foreach (KeyValuePair<RevisionKey, int> entry in restored)
            {
                _revisions[entry.Key] = entry.Value;
            }
        }

        /// <summary>Current revision. Unseen keys are revision 0, so absence needs no special casing.</summary>
        public int Get(RevisionKey key) => _revisions.TryGetValue(key, out int value) ? value : 0;

        /// <summary>
        /// Advances one aspect. Call this from the same code path that changes the protected state —
        /// materialize, bump, reschedule (§10.2).
        /// </summary>
        public int Bump(RevisionKey key)
        {
            int next = Get(key) + 1;
            _revisions[key] = next;
            return next;
        }

        public bool Matches(RevisionKey key, int expected) => Get(key) == expected;

        /// <summary>Deterministically ordered snapshot for persistence and diagnostics.</summary>
        public List<KeyValuePair<RevisionKey, int>> Snapshot()
        {
            var entries = new List<KeyValuePair<RevisionKey, int>>(_revisions);
            entries.Sort((a, b) => a.Key.CompareTo(b.Key));
            return entries;
        }

        public int Count => _revisions.Count;
    }
}
