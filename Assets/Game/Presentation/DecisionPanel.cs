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
            summaryText.text =
                $"Decision: {view.CharacterName}\n" +
                $"Status: {view.StatusLabel} — resolves {view.ResolveAtLabel}" +
                options + resolution;

            holdButton.gameObject.SetActive(view.CanBeHeld && !view.IsHeld);
            releaseButton.gameObject.SetActive(view.IsHeld);
            interveneButton.gameObject.SetActive(view.Resolution == null);
            interveneButton.interactable = _interventionTarget.IsSet;
        }

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
