using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;

namespace Vivarium.Unity.Presentation
{
    /// <summary>Bounded live notification and grouped offline/off-screen recap presentation.</summary>
    public sealed class NotificationRecapPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button openButton;

        private Action<NotificationEntryView> _open;
        private NotificationRecapView _view = new NotificationRecapView(false, new NotificationEntryView[0], 0, 0);
        private int _selectedIndex = -1;

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;
        public int EntryCount => _view.Entries.Count;
        public int SelectedHistoryEntryId => Selected?.HistoryEntryId ?? 0;

        public void Configure(Action<NotificationEntryView> open)
        {
            _open = open;
            previousButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            openButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            openButton.onClick.AddListener(Open);
        }

        public void Apply(NotificationRecapView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            int previousHistoryId = SelectedHistoryEntryId;
            _view = view;
            _selectedIndex = view.Entries.Count == 0 ? -1 : 0;
            for (int i = 0; i < view.Entries.Count; i++)
                if (view.Entries[i].HistoryEntryId == previousHistoryId)
                {
                    _selectedIndex = i;
                    break;
                }
            UpdateButtons();
            Render();
        }

        public bool TrySelectHistoryEntry(int historyEntryId)
        {
            for (int i = 0; i < _view.Entries.Count; i++)
                if (_view.Entries[i].HistoryEntryId == historyEntryId)
                {
                    _selectedIndex = i;
                    UpdateButtons();
                    Render();
                    return true;
                }
            return false;
        }

        public bool InvokeOpenForTest()
        {
            if (Selected == null || _open == null) return false;
            _open(Selected);
            return true;
        }

        private NotificationEntryView Selected =>
            _selectedIndex >= 0 && _selectedIndex < _view.Entries.Count ? _view.Entries[_selectedIndex] : null;

        private void Previous() => SelectOffset(-1);
        private void Next() => SelectOffset(1);
        private void Open() { if (Selected != null) _open?.Invoke(Selected); }

        private void SelectOffset(int offset)
        {
            if (_view.Entries.Count == 0) return;
            _selectedIndex = (_selectedIndex + offset + _view.Entries.Count) % _view.Entries.Count;
            UpdateButtons();
            Render();
        }

        private void UpdateButtons()
        {
            bool hasEntries = _view.Entries.Count > 0;
            previousButton.interactable = _view.Entries.Count > 1;
            nextButton.interactable = _view.Entries.Count > 1;
            openButton.interactable = hasEntries &&
                (Selected.CharacterId > 0 || Selected.DecisionId > 0 || Selected.LocationId > 0);
        }

        private void Render()
        {
            if (summaryText == null) return;
            var text = new StringBuilder();
            text.Append(_view.IsOfflineRecap ? "While you were away" : "World notifications")
                .Append(" — ").Append(_view.IncludedEventCount).Append(" meaningful event(s)");
            if (_view.Entries.Count == 0) text.Append("\nNo known changes to report");
            for (int i = 0; i < _view.Entries.Count; i++)
            {
                NotificationEntryView entry = _view.Entries[i];
                text.Append("\n").Append(i == _selectedIndex ? "> " : "  ")
                    .Append(entry.OccurredAtLabel).Append(" [").Append(entry.Category).Append("] ")
                    .Append(entry.Message);
                if (entry.OccurrenceCount > 1)
                    text.Append(" (×").Append(entry.OccurrenceCount).Append(" grouped)");
            }
            if (_view.OmittedGroupCount > 0)
                text.Append("\n+").Append(_view.OmittedGroupCount).Append(" older group(s) omitted");
            summaryText.text = text.ToString();
        }

        public static NotificationRecapPanel CreateRuntime(Transform parent)
        {
            GameObject root = new GameObject("Notification Recap Panel (Runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(580f, 340f);
            root.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.04f, 0.92f);

            NotificationRecapPanel panel = root.AddComponent<NotificationRecapPanel>();
            panel.summaryText = CreateText(root.transform);
            panel.previousButton = CreateButton(root.transform, "Previous", new Vector2(18f, 12f));
            panel.nextButton = CreateButton(root.transform, "Next", new Vector2(174f, 12f));
            panel.openButton = CreateButton(root.transform, "Open", new Vector2(330f, 12f), 230f);
            return panel;
        }

        private static TextMeshProUGUI CreateText(Transform parent)
        {
            GameObject root = new GameObject("Summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
            text.fontSize = 19f;
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
            root.GetComponent<Image>().color = new Color(0.32f, 0.23f, 0.08f, 1f);
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
