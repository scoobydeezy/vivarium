using System.Collections.Generic;
using UnityEngine;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;

namespace Vivarium.Unity.Presentation
{
    public sealed class CharacterRosterPanel : MonoBehaviour
    {
        [SerializeField] private CharacterRosterEntry entryPrefab;
        [SerializeField] private Transform entryRoot;

        private readonly Dictionary<int, CharacterRosterEntry> _entries = new Dictionary<int, CharacterRosterEntry>();

        public int EntryCount => _entries.Count;

        public void Apply(IReadOnlyList<CharacterRosterEntryView> roster, System.Action<CharacterId> toggle)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterRosterEntryView view = roster[i];
                if (!_entries.TryGetValue(view.CharacterId, out CharacterRosterEntry entry))
                {
                    entry = Instantiate(entryPrefab, entryRoot);
                    entry.gameObject.SetActive(true);
                    _entries.Add(view.CharacterId, entry);
                }

                entry.Bind(view, toggle);
            }
        }
    }
}
