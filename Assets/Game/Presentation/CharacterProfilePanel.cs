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
        private CharacterProfileView _profile;
        private Button _timelineButton;
        private Button _knowledgeButton;
        private bool _showTimeline;
        private bool _showKnowledge;

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;

        public bool IsTravelControlVisible => travelButton != null && travelButton.gameObject.activeSelf;

        public bool IsTimelineVisible => _showTimeline && _profile != null;

        public bool IsKnowledgeVisible => _showKnowledge && _profile != null;

        public void Configure(System.Action close)
        {
            _close = close;
            closeButton.onClick.RemoveListener(InvokeClose);
            closeButton.onClick.AddListener(InvokeClose);
            if (travelButton != null) travelButton.gameObject.SetActive(false);
            EnsureModeButtons();
            ShowPrompt("Click a character to inspect");
        }

        public void Apply(CharacterProfileView profile)
        {
            _profile = profile;
            if (_showTimeline)
            {
                RenderTimeline();
                return;
            }
            if (_showKnowledge)
            {
                RenderKnowledge();
                return;
            }

            RenderProfile();
        }

        public void ShowTimeline()
        {
            if (_profile == null) return;
            _showTimeline = true;
            _showKnowledge = false;
            RenderTimeline();
        }

        public void ShowKnowledge()
        {
            if (_profile == null) return;
            _showKnowledge = true;
            _showTimeline = false;
            RenderKnowledge();
        }

        private void RenderProfile()
        {
            CharacterProfileView profile = _profile;
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
            if (_timelineButton != null)
            {
                _timelineButton.gameObject.SetActive(true);
                SetButtonLabel(_timelineButton, "Timeline");
            }
            if (_knowledgeButton != null)
            {
                _knowledgeButton.gameObject.SetActive(true);
                SetButtonLabel(_knowledgeButton, "Knowledge");
            }
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        private void RenderTimeline()
        {
            ScheduleView schedule = _profile.Schedule;
            var text = new System.Text.StringBuilder();
            text.Append(schedule.CharacterName ?? _profile.DisplayName).Append(" — materialized timeline")
                .Append("\nNow: ").Append(schedule.NowLabel ?? "unknown")
                .Append("\nConflicting commitments: ").Append(schedule.ConflictCount);

            if (schedule.Entries.Count == 0)
            {
                text.Append("\n\nNo commitments are materialized in the current planning horizon.");
            }

            for (int i = 0; i < schedule.Entries.Count; i++)
            {
                ScheduleEntryView entry = schedule.Entries[i];
                text.Append("\n\n").Append(entry.Conflicts ? "⚠ " : "• ")
                    .Append(entry.Kind).Append(" — ").Append(entry.TimingLabel ?? entry.StatusLabel)
                    .Append(" [").Append(entry.StatusLabel).Append(']')
                    .Append("\n  ").Append(entry.StartLabel).Append(" → ").Append(entry.ExpectedEndLabel)
                    .Append("; start deadline ").Append(entry.LatestStartLabel)
                    .Append("\n  ").Append(entry.LocationLabel).Append(" — ").Append(entry.DurationLabel)
                    .Append(" — ").Append(entry.SourceLabel);
                if (entry.ParticipantNames.Count > 0)
                    text.Append("\n  With: ").Append(string.Join(", ", entry.ParticipantNames));
                if (entry.ConflictingCommitmentIds.Count > 0)
                    text.Append("\n  CONFLICT with commitment #")
                        .Append(string.Join(", #", entry.ConflictingCommitmentIds));
            }

            summaryText.text = text.ToString();
            closeButton.gameObject.SetActive(true);
            if (_timelineButton != null)
            {
                _timelineButton.gameObject.SetActive(true);
                SetButtonLabel(_timelineButton, "Overview");
            }
            if (_knowledgeButton != null)
            {
                _knowledgeButton.gameObject.SetActive(true);
                SetButtonLabel(_knowledgeButton, "Knowledge");
            }
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        private void RenderKnowledge()
        {
            var text = new System.Text.StringBuilder();
            text.Append(_profile.DisplayName)
                .Append(" — Knowledge view")
                .Append("\nPlayer observations only — unknown and character-held beliefs stay hidden.")
                .Append("\n\nPersonal observations:");
            if (_profile.KnownTraits.Count == 0 && _profile.KnownNeeds.Count == 0)
                text.Append(" none known");
            for (int i = 0; i < _profile.KnownTraits.Count; i++)
                AppendKnownFact(text, _profile.KnownTraits[i]);
            for (int i = 0; i < _profile.KnownNeeds.Count; i++)
                AppendKnownFact(text, _profile.KnownNeeds[i]);

            text.Append("\n\nKnown relationships:");
            if (_profile.KnownRelationships.Count == 0)
                text.Append(" none — relationship truth has not been observed");
            for (int i = 0; i < _profile.KnownRelationships.Count; i++)
            {
                KnownRelationshipView relationship = _profile.KnownRelationships[i];
                text.Append("\n\n  ").Append(relationship.PerspectiveLabel ?? relationship.OtherCharacterName)
                    .Append("\n  ").Append(relationship.DirectionLabel ?? "Direction unknown");
                if (relationship.MostRecentObservationLabel != null)
                    text.Append("\n  Last evidence: ").Append(relationship.MostRecentObservationLabel);
                if (relationship.HasStaleFacts) text.Append(" [possibly stale]");
                for (int fact = 0; fact < relationship.KnownFacts.Count; fact++)
                    AppendKnownFact(text, relationship.KnownFacts[fact], "    ");
            }

            text.Append("\n\nKnown social reports:");
            if (_profile.KnownSocialReports.Count == 0) text.Append(" none");
            for (int i = 0; i < _profile.KnownSocialReports.Count; i++)
                AppendKnownFact(text, _profile.KnownSocialReports[i]);

            summaryText.text = text.ToString();
            closeButton.gameObject.SetActive(true);
            if (_timelineButton != null)
            {
                _timelineButton.gameObject.SetActive(true);
                SetButtonLabel(_timelineButton, "Timeline");
            }
            if (_knowledgeButton != null)
            {
                _knowledgeButton.gameObject.SetActive(true);
                SetButtonLabel(_knowledgeButton, "Overview");
            }
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        private static void AppendKnownFact(
            System.Text.StringBuilder text,
            KnownFactView fact,
            string indent = "  ")
        {
            text.Append('\n').Append(indent).Append(fact.Label).Append(": ").Append(fact.ValueLabel)
                .Append(" — ").Append(fact.ConfidenceLabel)
                .Append(", observed ").Append(fact.AgeLabel ?? fact.ObservedAtLabel);
            if (fact.SourceLabel != null) text.Append(" via ").Append(fact.SourceLabel);
            if (fact.MayBeStale) text.Append(" [possibly stale]");
        }

        public void ShowPrompt(string message)
        {
            _profile = null;
            _showTimeline = false;
            _showKnowledge = false;
            summaryText.text = message;
            closeButton.gameObject.SetActive(false);
            if (_timelineButton != null) _timelineButton.gameObject.SetActive(false);
            if (_knowledgeButton != null) _knowledgeButton.gameObject.SetActive(false);
            if (travelButton != null) travelButton.gameObject.SetActive(false);
        }

        private void InvokeClose() => _close?.Invoke();

        private void ToggleTimeline()
        {
            if (_profile == null) return;
            _showTimeline = !_showTimeline;
            _showKnowledge = false;
            if (_showTimeline) RenderTimeline();
            else RenderProfile();
        }

        private void ToggleKnowledge()
        {
            if (_profile == null) return;
            _showKnowledge = !_showKnowledge;
            _showTimeline = false;
            if (_showKnowledge) RenderKnowledge();
            else RenderProfile();
        }

        private void EnsureModeButtons()
        {
            if (_timelineButton != null && _knowledgeButton != null) return;
            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null && panelRect.sizeDelta.y < 520f)
                panelRect.sizeDelta = new Vector2(Mathf.Max(panelRect.sizeDelta.x, 620f), 520f);
            if (_timelineButton == null)
            {
                _timelineButton = Instantiate(closeButton, closeButton.transform.parent);
                _timelineButton.name = "Timeline";
                _timelineButton.onClick.RemoveAllListeners();
                _timelineButton.onClick.AddListener(ToggleTimeline);
                _timelineButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(174f, 12f);
                SetButtonLabel(_timelineButton, "Timeline");
            }
            if (_knowledgeButton == null)
            {
                _knowledgeButton = Instantiate(closeButton, closeButton.transform.parent);
                _knowledgeButton.name = "Knowledge";
                _knowledgeButton.onClick.RemoveAllListeners();
                _knowledgeButton.onClick.AddListener(ToggleKnowledge);
                _knowledgeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(330f, 12f);
                SetButtonLabel(_knowledgeButton, "Knowledge");
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

    }
}
