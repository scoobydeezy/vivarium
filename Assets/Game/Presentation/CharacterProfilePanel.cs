using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;

namespace Vivarium.Unity.Presentation
{
    public sealed class CharacterProfilePanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button closeButton;
        [Tooltip("Legacy smoke-UI control; Travel is not an MVP player verb and is always hidden.")]
        [SerializeField] private Button travelButton;

        private System.Action _close;

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;

        public bool IsTravelControlVisible => travelButton != null && travelButton.gameObject.activeSelf;

        public void Configure(System.Action close)
        {
            _close = close;
            closeButton.onClick.RemoveListener(InvokeClose);
            closeButton.onClick.AddListener(InvokeClose);
            if (travelButton != null) travelButton.gameObject.SetActive(false);
            ShowPrompt("Click a character to inspect");
        }

        public void Apply(CharacterProfileView profile)
        {
            string needs = "Needs: not yet observed";
            if (profile.KnownNeeds.Count > 0)
            {
                needs = "Needs:";
                for (int i = 0; i < profile.KnownNeeds.Count; i++)
                {
                    KnownFactView need = profile.KnownNeeds[i];
                    string stale = need.MayBeStale ? " (possibly stale)" : string.Empty;
                    needs += $"\n  {need.Label}: {need.ValueLabel} observed {need.ObservedAtLabel}{stale}";
                }
            }

            var text = new System.Text.StringBuilder();
            text.Append(profile.DisplayName).Append('\n')
                .Append("Activity: ").Append(profile.CurrentActivityLabel).Append('\n')
                .Append("Location: ").Append(profile.LocationLabel).Append('\n')
                .Append(needs).Append("\n\nSchedule:");
            if (profile.Schedule.Entries.Count == 0) text.Append(" none materialized");
            for (int i = 0; i < profile.Schedule.Entries.Count; i++)
            {
                ScheduleEntryView entry = profile.Schedule.Entries[i];
                text.Append("\n  ").Append(entry.StartLabel).Append(" — ").Append(entry.Kind)
                    .Append(" at ").Append(entry.LocationLabel);
                if (entry.Conflicts) text.Append(" [conflict]");
            }

            text.Append("\n\nSocial / Knowledge:");
            if (profile.KnownRelationships.Count == 0) text.Append(" no known relationships");
            for (int i = 0; i < profile.KnownRelationships.Count; i++)
            {
                KnownRelationshipView relationship = profile.KnownRelationships[i];
                text.Append("\n  ").Append(relationship.OtherCharacterName);
                for (int j = 0; j < relationship.KnownFacts.Count; j++)
                    text.Append(" — ").Append(relationship.KnownFacts[j].ValueLabel);
            }

            text.Append("\n\nDecisions:");
            if (profile.Decisions.Count == 0) text.Append(" none");
            for (int i = 0; i < profile.Decisions.Count; i++)
            {
                CharacterDecisionSummaryView decision = profile.Decisions[i];
                text.Append("\n  ").Append(decision.DefinitionId).Append(" — ")
                    .Append(decision.StatusLabel).Append(", ").Append(decision.TimeLabel);
            }

            text.Append("\n\nHistory:");
            if (profile.RecentHistory.Count == 0) text.Append(" none retained");
            for (int i = 0; i < profile.RecentHistory.Count; i++)
                text.Append("\n  ").Append(profile.RecentHistory[i].OccurredAtLabel).Append(" — ")
                    .Append(profile.RecentHistory[i].Summary);

            summaryText.text = text.ToString();
            closeButton.gameObject.SetActive(true);
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        public void ShowPrompt(string message)
        {
            summaryText.text = message;
            closeButton.gameObject.SetActive(false);
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        private void InvokeClose() => _close?.Invoke();

    }
}
