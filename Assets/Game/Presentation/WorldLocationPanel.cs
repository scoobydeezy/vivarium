using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;

namespace Vivarium.Unity.Presentation
{
    /// <summary>Player-facing location selection, observation, and Commons availability control.</summary>
    public sealed class WorldLocationPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button availabilityButton;

        private Action<LocationId> _select;
        private Action<LocationId> _toggleAvailability;
        private IReadOnlyList<LocationId> _locations = new LocationId[0];
        private LocationView _view;
        private int _selectedIndex = -1;

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;
        public int SelectedLocationId => _view?.LocationId ?? 0;

        public void Configure(Action<LocationId> select, Action<LocationId> toggleAvailability)
        {
            _select = select;
            _toggleAvailability = toggleAvailability;
            previousButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            availabilityButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            availabilityButton.onClick.AddListener(ToggleAvailability);
        }

        public void Apply(LocationView view, IReadOnlyList<LocationId> locations)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _selectedIndex = -1;
            for (int i = 0; i < _locations.Count; i++)
                if (_locations[i].Value == view.LocationId)
                {
                    _selectedIndex = i;
                    break;
                }

            previousButton.interactable = _locations.Count > 1;
            nextButton.interactable = _locations.Count > 1;
            availabilityButton.interactable = view.CanManageAvailability;
            SetButtonLabel(
                availabilityButton,
                (view.IsOpen ? "Close" : "Open") + $" — {view.AvailabilityNudgeCost} Nudge");
            Render();
        }

        public bool TrySelectLocation(LocationId locationId)
        {
            for (int i = 0; i < _locations.Count; i++)
                if (_locations[i] == locationId)
                {
                    _select?.Invoke(locationId);
                    return _select != null;
                }
            return false;
        }

        public bool InvokeAvailabilityForTest()
        {
            if (_view == null || !availabilityButton.interactable || _toggleAvailability == null) return false;
            _toggleAvailability(new LocationId(_view.LocationId));
            return true;
        }

        private void Previous() => SelectOffset(-1);
        private void Next() => SelectOffset(1);

        private void SelectOffset(int offset)
        {
            if (_locations.Count == 0) return;
            int index = (_selectedIndex + offset + _locations.Count) % _locations.Count;
            _select?.Invoke(_locations[index]);
        }

        private void ToggleAvailability()
        {
            if (_view != null) _toggleAvailability?.Invoke(new LocationId(_view.LocationId));
        }

        private void Render()
        {
            if (summaryText == null || _view == null) return;
            var text = new StringBuilder();
            text.Append("World locations — ").Append(_selectedIndex + 1).Append('/').Append(_locations.Count)
                .Append("\n\n").Append(_view.DisplayName).Append(" — ")
                .Append(_view.IsOpen ? "OPEN" : "CLOSED")
                .Append("\nKind: ").Append(_view.LocationKindId);
            if (!string.IsNullOrEmpty(_view.ParentDisplayName))
                text.Append("\nWithin: ").Append(_view.ParentDisplayName);
            text.Append("\nResources: Nudges ").Append(_view.NudgeBalance)
                .Append("; management cost ").Append(_view.AvailabilityNudgeCost);
            if (!_view.CanManageAvailability && !string.IsNullOrEmpty(_view.AvailabilityDisabledReason))
                text.Append("\nManagement unavailable: ").Append(_view.AvailabilityDisabledReason);

            text.Append("\n\nObserved here / approaching:");
            if (_view.ObservedPresence.Count == 0) text.Append("\nNone among watched characters");
            for (int i = 0; i < _view.ObservedPresence.Count; i++)
            {
                LocationPresenceView presence = _view.ObservedPresence[i];
                text.Append("\n• ").Append(presence.CharacterName).Append(" — ").Append(presence.StatusLabel);
            }

            text.Append("\n\nRecent location events:");
            if (_view.RecentHistory.Count == 0) text.Append("\nNone yet");
            for (int i = 0; i < _view.RecentHistory.Count; i++)
            {
                LocationHistoryEntryView entry = _view.RecentHistory[i];
                text.Append("\n").Append(entry.OccurredAtLabel).Append(" — ").Append(entry.Summary);
            }
            summaryText.text = text.ToString();
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        public static WorldLocationPanel CreateRuntime(Transform parent)
        {
            GameObject root = new GameObject("World Location Panel (Runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(620f, 430f);
            root.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.1f, 0.9f);

            WorldLocationPanel panel = root.AddComponent<WorldLocationPanel>();
            panel.summaryText = CreateText(root.transform);
            panel.previousButton = CreateButton(root.transform, "Previous", new Vector2(18f, 12f));
            panel.nextButton = CreateButton(root.transform, "Next", new Vector2(174f, 12f));
            panel.availabilityButton = CreateButton(root.transform, "Availability", new Vector2(330f, 12f), 270f);
            return panel;
        }

        private static TextMeshProUGUI CreateText(Transform parent)
        {
            GameObject root = new GameObject("Summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
            text.fontSize = 21f;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 64f);
            rect.offsetMax = new Vector2(-18f, -18f);
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, float width = 140f)
        {
            GameObject root = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 40f);
            root.GetComponent<Image>().color = new Color(0.12f, 0.3f, 0.28f, 1f);

            TextMeshProUGUI text = CreateText(root.transform);
            text.name = "Label";
            text.text = label;
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return root.GetComponent<Button>();
        }
    }
}
