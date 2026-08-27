using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;

namespace Vivarium.Unity.Presentation
{
    /// <summary>Selectable Decision inbox and knowledge-filtered detail surface.</summary>
    public sealed class DecisionPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button holdButton;
        [SerializeField] private Button releaseButton;
        [SerializeField] private Button interveneButton;

        private Action<DecisionId> _hold;
        private Action<DecisionId> _release;
        private Action<DecisionId, DecisionInfluenceId, AuthoredId> _intervene;
        private Action<DecisionId> _selectDecision;
        private DecisionFeedView _feed = new DecisionFeedView(new DecisionFeedEntryView[0], 0, 0);
        private DecisionView _detail;
        private NudgeEconomyView _nudges;
        private IReadOnlyList<InterventionResourceView> _resources = new InterventionResourceView[0];
        private readonly List<InterventionFocus> _interventionFocus = new List<InterventionFocus>();
        private DecisionId _decisionId;
        private int _selectedFeedIndex = -1;
        private int _selectedInterventionIndex;
        private string _historySummary = "No recent decision events";
        private Button _previousDecisionButton;
        private Button _nextDecisionButton;
        private Button _previousInterventionButton;
        private Button _nextInterventionButton;

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;
        public int FeedEntryCount => _feed.Entries.Count;
        public int SelectedDecisionId => _decisionId.Value;
        public string SelectedInterventionId => CurrentFocus?.Availability.InterventionDefinitionId;

        public bool TrySelectDecision(DecisionId decisionId)
        {
            for (int i = 0; i < _feed.Entries.Count; i++)
                if (_feed.Entries[i].DecisionId == decisionId.Value)
                {
                    _selectDecision?.Invoke(decisionId);
                    return _selectDecision != null;
                }

            return false;
        }

        public void Configure(
            Action<DecisionId> hold,
            Action<DecisionId> release,
            Action<DecisionId, DecisionInfluenceId, AuthoredId> intervene,
            Action<DecisionId> selectDecision)
        {
            _hold = hold;
            _release = release;
            _intervene = intervene;
            _selectDecision = selectDecision;
            holdButton.onClick.RemoveListener(InvokeHold);
            releaseButton.onClick.RemoveListener(InvokeRelease);
            interveneButton.onClick.RemoveListener(InvokeIntervene);
            holdButton.onClick.AddListener(InvokeHold);
            releaseButton.onClick.AddListener(InvokeRelease);
            interveneButton.onClick.AddListener(InvokeIntervene);
            EnsureNavigationButtons();
        }

        public void ApplyFeed(DecisionFeedView feed, DecisionId selectedDecisionId)
        {
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _selectedFeedIndex = -1;
            for (int i = 0; i < feed.Entries.Count; i++)
                if (feed.Entries[i].DecisionId == selectedDecisionId.Value)
                {
                    _selectedFeedIndex = i;
                    break;
                }
            UpdateNavigationButtons();
            Render();
        }

        public void ApplyResources(NudgeEconomyView nudges, IReadOnlyList<InterventionResourceView> resources)
        {
            _nudges = nudges;
            _resources = resources ?? new InterventionResourceView[0];
            Render();
        }

        public void Apply(DecisionView view)
        {
            _detail = view ?? throw new ArgumentNullException(nameof(view));
            _decisionId = new DecisionId(view.DecisionId);
            string previousIntervention = CurrentFocus?.Availability.InterventionDefinitionId;
            int previousInfluence = CurrentFocus?.Influence.InfluenceId ?? 0;
            _interventionFocus.Clear();

            for (int o = 0; o < view.Options.Count; o++)
                for (int i = 0; i < view.Options[o].Influences.Count; i++)
                {
                    InfluenceView influence = view.Options[o].Influences[i];
                    for (int action = 0; action < influence.Interventions.Count; action++)
                        _interventionFocus.Add(new InterventionFocus(influence, influence.Interventions[action]));
                }

            _selectedInterventionIndex = 0;
            for (int i = 0; i < _interventionFocus.Count; i++)
                if (_interventionFocus[i].Influence.InfluenceId == previousInfluence &&
                    _interventionFocus[i].Availability.InterventionDefinitionId == previousIntervention)
                {
                    _selectedInterventionIndex = i;
                    break;
                }

            holdButton.gameObject.SetActive(view.CanBeHeld);
            releaseButton.gameObject.SetActive(view.IsHeld);
            UpdateInterventionButtons();
            Render();
        }

        public void ApplyHistory(DecisionHistoryView view)
        {
            _historySummary = "Recent events";
            for (int i = 0; i < view.Entries.Count; i++)
            {
                DecisionHistoryEntryView entry = view.Entries[i];
                _historySummary += $"\n{entry.OccurredAtLabel} — {entry.Message}";
            }
            if (view.Entries.Count == 0) _historySummary += "\nNone yet";
            Render();
        }

        public void ShowNoDecision()
        {
            _decisionId = DecisionId.None;
            _detail = null;
            _interventionFocus.Clear();
            holdButton.gameObject.SetActive(false);
            releaseButton.gameObject.SetActive(false);
            interveneButton.gameObject.SetActive(false);
            UpdateInterventionButtons();
            Render();
        }

        private void Render()
        {
            if (summaryText == null) return;
            var text = new System.Text.StringBuilder();
            text.Append("Decision inbox — Holds ").Append(_feed.HeldCount).Append('/').Append(_feed.HeldCapacity);
            for (int i = 0; i < _feed.Entries.Count; i++)
            {
                DecisionFeedEntryView entry = _feed.Entries[i];
                text.Append('\n').Append(i == _selectedFeedIndex ? "▶ " : "  ")
                    .Append(entry.CharacterName).Append(" — ").Append(entry.DefinitionId)
                    .Append(" — importance ").Append(entry.Importance);
                if (entry.IsHeld) text.Append(" [Held]");
                if (entry.HasHardDeadline) text.Append(" [ceiling]");
                if (entry.IsRecentResolution) text.Append(" [Recent result]");
                else text.Append(" — ").Append(entry.TimeRemainingLabel).Append(" remaining");
            }
            if (_feed.Entries.Count == 0) text.Append("\nNo surfaced decisions");

            text.Append("\n\n");
            if (_detail == null) text.Append("No selected decision");
            else AppendDetail(text, _detail);

            InterventionFocus focus = CurrentFocus;
            if (focus != null)
                text.Append("\nSelected action: ").Append(focus.Availability.InterventionDefinitionId)
                    .Append(" on Influence ").Append(focus.Influence.InfluenceId)
                    .Append(" — ").Append(focus.Availability.IsAvailable
                        ? "available"
                        : "unavailable: " + focus.Availability.UnavailableReason);

            text.Append("\n\nResources:");
            if (_nudges != null) text.Append(" Nudges ").Append(_nudges.Balance).Append('/').Append(_nudges.Cap);
            for (int i = 0; i < _resources.Count; i++)
                text.Append("; ").Append(_resources[i].ResourceKind).Append(' ')
                    .Append(_resources[i].Balance).Append('/').Append(_resources[i].Cap);
            text.Append("\n\n").Append(_historySummary);
            summaryText.text = text.ToString();
        }

        private static void AppendDetail(System.Text.StringBuilder text, DecisionView view)
        {
            text.Append("Decision: ").Append(view.CharacterName).Append(" — ").Append(view.DefinitionId).Append('\n')
                .Append("Status: ").Append(view.StatusLabel).Append(" — importance ").Append(view.Importance)
                .Append(" — ").Append(view.HasHardDeadline ? "hard deadline " : "resolves ")
                .Append(view.ResolveAtLabel).Append('\n')
                .Append("Hold capacity: global ").Append(view.GlobalHoldRemaining)
                .Append(", character ").Append(view.CharacterHoldRemaining);
            if (!view.CanBeHeld && !view.IsHeld && view.HoldUnavailableReason != null)
                text.Append(" — unavailable: ").Append(view.HoldUnavailableReason);

            for (int o = 0; o < view.Options.Count; o++)
            {
                DecisionOptionView option = view.Options[o];
                text.Append('\n').Append(option.Label).Append(':');
                for (int i = 0; i < option.Influences.Count; i++)
                {
                    InfluenceView influence = option.Influences[i];
                    string label = influence.Label ?? influence.Category ?? "Unknown influence";
                    text.Append("\n  • ").Append(label);
                    if (influence.DieSides.HasValue) text.Append(" d").Append(influence.DieSides.Value);
                    if (influence.Explanation != null && influence.Explanation != label)
                        text.Append(" — ").Append(influence.Explanation);
                    for (int action = 0; action < influence.Interventions.Count; action++)
                    {
                        InterventionAvailabilityView availability = influence.Interventions[action];
                        text.Append("\n      ").Append(availability.InterventionDefinitionId)
                            .Append(" — ").Append(availability.ResourceKind).Append(' ')
                            .Append(availability.Cost).Append(" — ")
                            .Append(availability.IsAvailable ? "available" : "unavailable: " + availability.UnavailableReason);
                    }
                }
            }

            if (view.PendingResolution != null)
            {
                text.Append("\nPending rolls — expires ").Append(view.PendingResolution.ExpiresAt);
                for (int i = 0; i < view.PendingResolution.AcceptedRolls.Count; i++)
                {
                    PendingInfluenceRollView roll = view.PendingResolution.AcceptedRolls[i];
                    text.Append("\n  Influence ").Append(roll.InfluenceId).Append(": d")
                        .Append(roll.DieSides).Append(" → ").Append(roll.Rolled)
                        .Append(" (#").Append(roll.RollIndex).Append(')');
                }
            }

            if (view.AppliedInterventions.Count > 0)
            {
                text.Append("\nApplied interventions:");
                for (int i = 0; i < view.AppliedInterventions.Count; i++)
                {
                    AppliedInterventionView applied = view.AppliedInterventions[i];
                    text.Append("\n  ").Append(applied.InterventionDefinitionId)
                        .Append(" → Influence ").Append(applied.TargetInfluenceId)
                        .Append(" — ").Append(applied.ResourceKind).Append(' ').Append(applied.ResourceCost);
                }
            }

            if (view.Resolution != null)
            {
                text.Append("\nResolved: ").Append(view.Resolution.ChosenOptionId)
                    .Append(" (").Append(view.Resolution.DegreeLabel).Append(") — ")
                    .Append(view.Resolution.OutcomeSourceLabel);
                for (int i = 0; i < view.Resolution.Reasons.Count; i++)
                {
                    DecisionReasonExplanationView reason = view.Resolution.Reasons[i];
                    text.Append("\n  ").Append(reason.Label ?? reason.Category ?? "Known reason")
                        .Append(" d").Append(reason.DieSides).Append(" → ").Append(reason.Rolled);
                }
            }
        }

        private void EnsureNavigationButtons()
        {
            if (_previousDecisionButton != null) return;
            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null && panelRect.sizeDelta.y < 600f)
                panelRect.sizeDelta = new Vector2(Mathf.Max(panelRect.sizeDelta.x, 660f), 600f);
            if (summaryText != null) summaryText.rectTransform.offsetMin = new Vector2(18f, 150f);
            _previousDecisionButton = CloneButton("Previous Decision", new Vector2(18f, 56f), PreviousDecision);
            _nextDecisionButton = CloneButton("Next Decision", new Vector2(174f, 56f), NextDecision);
            _previousInterventionButton = CloneButton("Previous Action", new Vector2(330f, 56f), PreviousIntervention);
            _nextInterventionButton = CloneButton("Next Action", new Vector2(486f, 56f), NextIntervention);
            SetButtonLabel(interveneButton, "Apply Action");
        }

        private Button CloneButton(string label, Vector2 position, UnityEngine.Events.UnityAction listener)
        {
            Button button = Instantiate(holdButton, holdButton.transform.parent);
            button.name = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(listener);
            button.GetComponent<RectTransform>().anchoredPosition = position;
            SetButtonLabel(button, label);
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        private void UpdateNavigationButtons()
        {
            bool several = _feed.Entries.Count > 1;
            if (_previousDecisionButton != null) _previousDecisionButton.gameObject.SetActive(several);
            if (_nextDecisionButton != null) _nextDecisionButton.gameObject.SetActive(several);
        }

        private void UpdateInterventionButtons()
        {
            bool hasActions = _interventionFocus.Count > 0 && _detail != null && _detail.Resolution == null;
            if (_previousInterventionButton != null) _previousInterventionButton.gameObject.SetActive(_interventionFocus.Count > 1);
            if (_nextInterventionButton != null) _nextInterventionButton.gameObject.SetActive(_interventionFocus.Count > 1);
            interveneButton.gameObject.SetActive(hasActions);
            InterventionFocus focus = CurrentFocus;
            interveneButton.interactable = focus != null && focus.Availability.IsAvailable;
            if (focus != null) SetButtonLabel(interveneButton, ShortLabel(focus.Availability.InterventionDefinitionId));
        }

        private static string ShortLabel(string authoredId)
        {
            int separator = authoredId.LastIndexOf('.');
            string value = separator < 0 ? authoredId : authoredId.Substring(separator + 1);
            return value.Replace('_', ' ');
        }

        private InterventionFocus CurrentFocus =>
            _interventionFocus.Count == 0 ? null : _interventionFocus[_selectedInterventionIndex];

        private void PreviousDecision() => MoveDecision(-1);
        private void NextDecision() => MoveDecision(1);

        private void MoveDecision(int delta)
        {
            if (_feed.Entries.Count < 2) return;
            _selectedFeedIndex = (_selectedFeedIndex + delta + _feed.Entries.Count) % _feed.Entries.Count;
            TrySelectDecision(new DecisionId(_feed.Entries[_selectedFeedIndex].DecisionId));
        }

        private void PreviousIntervention() => MoveIntervention(-1);
        private void NextIntervention() => MoveIntervention(1);

        private void MoveIntervention(int delta)
        {
            if (_interventionFocus.Count < 2) return;
            _selectedInterventionIndex =
                (_selectedInterventionIndex + delta + _interventionFocus.Count) % _interventionFocus.Count;
            UpdateInterventionButtons();
            Render();
        }

        private void InvokeHold() => _hold?.Invoke(_decisionId);
        private void InvokeRelease() => _release?.Invoke(_decisionId);

        private void InvokeIntervene()
        {
            InterventionFocus focus = CurrentFocus;
            if (focus != null && focus.Availability.IsAvailable)
                _intervene?.Invoke(
                    _decisionId,
                    new DecisionInfluenceId(focus.Influence.InfluenceId),
                    new AuthoredId(focus.Availability.InterventionDefinitionId));
        }

        private sealed class InterventionFocus
        {
            public InterventionFocus(InfluenceView influence, InterventionAvailabilityView availability)
            {
                Influence = influence;
                Availability = availability;
            }

            public InfluenceView Influence { get; }
            public InterventionAvailabilityView Availability { get; }
        }
    }
}
