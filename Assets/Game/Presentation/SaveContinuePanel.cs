using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vivarium.Unity.Presentation
{
    /// <summary>Three-slot player save/continue surface. All persistence work is delegated to the composition root.</summary>
    public sealed class SaveContinuePanel : MonoBehaviour
    {
        private static readonly string[] Slots = { "slot_001", "slot_002", "slot_003" };

        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;

        private Func<IReadOnlyList<string>> _listSlots;
        private Func<string, string> _save;
        private Func<string, string> _load;
        private Func<string, string> _delete;
        private int _selectedIndex;
        private string _status = "Ready";
        private readonly HashSet<string> _existing = new HashSet<string>(StringComparer.Ordinal);

        public string DisplayedText => summaryText == null ? string.Empty : summaryText.text;
        public string SelectedSlot => Slots[_selectedIndex];

        public void Configure(
            Func<IReadOnlyList<string>> listSlots,
            Func<string, string> save,
            Func<string, string> load,
            Func<string, string> delete)
        {
            _listSlots = listSlots ?? throw new ArgumentNullException(nameof(listSlots));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _delete = delete ?? throw new ArgumentNullException(nameof(delete));

            previousButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            saveButton.onClick.RemoveAllListeners();
            loadButton.onClick.RemoveAllListeners();
            deleteButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            deleteButton.onClick.AddListener(Delete);
            Refresh();
        }

        public bool SelectSlotForTest(string slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] == slot)
                {
                    _selectedIndex = i;
                    Refresh();
                    return true;
                }
            return false;
        }

        public string InvokeSaveForTest() { Save(); return _status; }
        public string InvokeLoadForTest() { Load(); return _status; }
        public string InvokeDeleteForTest() { Delete(); return _status; }

        public string InvokeSaveForTest(string slot) { _status = _save(slot); Refresh(); return _status; }
        public string InvokeLoadForTest(string slot) { _status = _load(slot); Refresh(); return _status; }
        public string InvokeDeleteForTest(string slot) { _status = _delete(slot); Refresh(); return _status; }

        private void Previous() { _selectedIndex = (_selectedIndex + Slots.Length - 1) % Slots.Length; Refresh(); }
        private void Next() { _selectedIndex = (_selectedIndex + 1) % Slots.Length; Refresh(); }
        private void Save() { _status = _save(SelectedSlot); Refresh(); }
        private void Load() { _status = _load(SelectedSlot); Refresh(); }
        private void Delete() { _status = _delete(SelectedSlot); Refresh(); }

        private void Refresh()
        {
            _existing.Clear();
            if (_listSlots != null)
            {
                IReadOnlyList<string> slots = _listSlots();
                for (int i = 0; i < slots.Count; i++) _existing.Add(slots[i]);
            }
            bool exists = _existing.Contains(SelectedSlot);
            loadButton.interactable = exists;
            deleteButton.interactable = exists;
            if (summaryText == null) return;

            var text = new StringBuilder();
            text.Append("Save / Continue\n")
                .Append("Selected: ").Append(SelectedSlot).Append(exists ? " — saved" : " — empty")
                .Append("\n").Append(_status);
            summaryText.text = text.ToString();
        }

        public static SaveContinuePanel CreateRuntime(Transform parent)
        {
            GameObject root = new GameObject("Save Continue Panel (Runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(620f, 190f);
            root.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.92f);

            SaveContinuePanel panel = root.AddComponent<SaveContinuePanel>();
            panel.summaryText = CreateText(root.transform);
            panel.previousButton = CreateButton(root.transform, "Previous", new Vector2(12f, 12f), 105f);
            panel.nextButton = CreateButton(root.transform, "Next", new Vector2(123f, 12f), 80f);
            panel.saveButton = CreateButton(root.transform, "Save", new Vector2(209f, 12f), 100f);
            panel.loadButton = CreateButton(root.transform, "Load", new Vector2(315f, 12f), 100f);
            panel.deleteButton = CreateButton(root.transform, "Delete", new Vector2(421f, 12f), 185f);
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
            rect.offsetMin = new Vector2(16f, 62f);
            rect.offsetMax = new Vector2(-16f, -14f);
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, float width)
        {
            GameObject root = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 40f);
            root.GetComponent<Image>().color = new Color(0.16f, 0.25f, 0.42f, 1f);
            TextMeshProUGUI text = CreateText(root.transform);
            text.name = "Label";
            text.text = label;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return root.GetComponent<Button>();
        }
    }
}
