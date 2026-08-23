using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;

namespace Vivarium.Unity.Presentation
{
    public sealed class DecisionPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button holdButton;
        [SerializeField] private Button releaseButton;
        [SerializeField] private Button interveneButton;

        private System.Action<DecisionId> _hold;
        private System.Action<DecisionId> _release;
        private System.Action<DecisionId, DecisionInfluenceId> _intervene;
        private DecisionId _decisionId;
        private DecisionInfluenceId _interventionTarget;
        private string _decisionSummary = "No active decision";
        private string _historySummary = "No recent decision events";

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;

        public void Configure(
            System.Action<DecisionId> hold,
            System.Action<DecisionId> release,
            System.Action<DecisionId, DecisionInfluenceId> intervene)
        {
            _hold = hold;
            _release = release;
            _intervene = intervene;
            holdButton.onClick.RemoveListener(InvokeHold);
            releaseButton.onClick.RemoveListener(InvokeRelease);
            interveneButton.onClick.RemoveListener(InvokeIntervene);
            holdButton.onClick.AddListener(InvokeHold);
            releaseButton.onClick.AddListener(InvokeRelease);
            interveneButton.onClick.AddListener(InvokeIntervene);
        }

        public void Apply(DecisionView view)
        {
            _decisionId = new DecisionId(view.DecisionId);
            _interventionTarget = DecisionInfluenceId.None;
            string options = string.Empty;

            for (int o = 0; o < view.Options.Count; o++)
            {
                DecisionOptionView option = view.Options[o];
                options += $"\n{option.Label}:";
                for (int i = 0; i < option.Influences.Count; i++)
                {
                    InfluenceView influence = option.Influences[i];
                    string label = influence.Label ?? influence.Category ?? "Unknown influence";
                    string die = influence.DieSides.HasValue ? $" d{influence.DieSides.Value}" : string.Empty;
                    options += $"\n  • {label}{die}";
                    if (!_interventionTarget.IsSet && influence.CanBeIntervenedOn)
                    {
                        _interventionTarget = new DecisionInfluenceId(influence.InfluenceId);
                    }
                }
            }

            string resolution = view.Resolution == null
                ? string.Empty
                : $"\nResolved: {view.Resolution.ChosenOptionId} ({view.Resolution.DegreeLabel})";
            _decisionSummary =
                $"Decision: {view.CharacterName}\n" +
                $"Status: {view.StatusLabel} — resolves {view.ResolveAtLabel}" +
                options + resolution;
            Render();

            holdButton.gameObject.SetActive(view.CanBeHeld && !view.IsHeld);
            releaseButton.gameObject.SetActive(view.IsHeld);
            interveneButton.gameObject.SetActive(view.Resolution == null);
            interveneButton.interactable = _interventionTarget.IsSet;
        }

        public void ApplyHistory(DecisionHistoryView view)
        {
            _historySummary = "Recent events";
            for (int i = 0; i < view.Entries.Count; i++)
            {
                DecisionHistoryEntryView entry = view.Entries[i];
                _historySummary += $"\n{entry.OccurredAtLabel} — {entry.Message}";
            }

            if (view.Entries.Count == 0)
            {
                _historySummary += "\nNone yet";
            }

            Render();
        }

        public void ShowNoDecision()
        {
            _decisionId = DecisionId.None;
            _interventionTarget = DecisionInfluenceId.None;
            _decisionSummary = "No active decision";
            holdButton.gameObject.SetActive(false);
            releaseButton.gameObject.SetActive(false);
            interveneButton.gameObject.SetActive(false);
            Render();
        }

        private void Render() => summaryText.text = _decisionSummary + "\n\n" + _historySummary;

        private void InvokeHold() => _hold?.Invoke(_decisionId);

        private void InvokeRelease() => _release?.Invoke(_decisionId);

        private void InvokeIntervene()
        {
            if (_interventionTarget.IsSet)
            {
                _intervene?.Invoke(_decisionId, _interventionTarget);
            }
        }
    }
}
