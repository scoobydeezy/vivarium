using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Unity.Presentation
{
    /// <summary>
    /// Turns quiescent simulation snapshots into visible views, and player input into commands
    /// (§13.1, §43, §46).
    /// <para>
    /// Two responsibilities, both one-directional. Reads: subscribe to the projection publisher, refresh
    /// the views for characters that are actually visible. Writes: translate semantic interactions into
    /// commands — never direct mutation (§2.2).
    /// </para>
    /// <para>
    /// Observation signals are aggregated transitions, not per-frame reports (§25): a character entering
    /// or leaving the visible set emits one command, not one per rendered frame.
    /// </para>
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private CharacterView characterViewPrefab;

        [SerializeField] private Transform viewRoot;

        private readonly Dictionary<int, CharacterView> _activeViews = new Dictionary<int, CharacterView>();
        private readonly Stack<CharacterView> _pool = new Stack<CharacterView>();
        private readonly HashSet<int> _visibleThisRefresh = new HashSet<int>();
        private readonly HashSet<int> _followedCharacters = new HashSet<int>();
        private readonly Dictionary<int, Button> _rosterButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, TextMeshProUGUI> _rosterLabels = new Dictionary<int, TextMeshProUGUI>();
        private readonly CharacterProfileProjector _profiles = new CharacterProfileProjector();
        private readonly CharacterRosterProjector _roster = new CharacterRosterProjector();

        private ProjectionPublisher _publisher;
        private System.Func<ICommand, string, CommandEnvelope> _enqueue;
        private TextMeshProUGUI _runtimeProfileText;
        private Button _runtimeCloseButton;
        private Button _runtimeTravelButton;
        private System.Action _runtimeSave;
        private System.Action _runtimeLoad;
        private CharacterId _inspectedCharacter;
        private System.Func<CharacterId, ICommand> _runtimeTravelCommandFactory;
        private RectTransform _runtimeRosterRoot;

        public int ActiveViewCount => _activeViews.Count;

        public int PooledViewCount => _pool.Count;

        public bool HasActiveView(CharacterId characterId) => _activeViews.ContainsKey(characterId.Value);

        /// <summary>
        /// Supplies a deliberately simple runtime marker when no authored presentation prefab has been
        /// assigned yet. This keeps the first Unity integration test self-contained; assigning a real
        /// prefab and view root in the Inspector automatically replaces the fallback path.
        /// </summary>
        public void EnsureRuntimeFallback()
        {
            if (viewRoot == null)
            {
                var root = new GameObject("Runtime Character Views");
                root.transform.SetParent(transform, false);
                viewRoot = root.transform;
            }

            EnsureRuntimeProfilePanel();

            if (characterViewPrefab != null)
            {
                return;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "Runtime Character Marker (Template)";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(0.75f, 1f, 0.75f);

            if (marker.TryGetComponent(out Renderer markerRenderer))
            {
                markerRenderer.material.color = new Color(0.15f, 0.85f, 1f);
            }

            characterViewPrefab = marker.AddComponent<CharacterView>();
            marker.SetActive(false);
        }

        public void ConfigureRuntimeTravel(System.Func<CharacterId, ICommand> commandFactory) =>
            _runtimeTravelCommandFactory = commandFactory;

        public void ConfigureRuntimePersistence(System.Action save, System.Action load)
        {
            _runtimeSave = save;
            _runtimeLoad = load;
        }

        public void PrepareForWorldReload()
        {
            _inspectedCharacter = CharacterId.None;
            SetSelectedView(CharacterId.None);
            _runtimeProfileText.text = "World loaded — click a cyan character to inspect";
            _runtimeCloseButton.gameObject.SetActive(false);
            _runtimeTravelButton.gameObject.SetActive(false);
        }

        private void EnsureRuntimeProfilePanel()
        {
            if (_runtimeProfileText != null)
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("Runtime UI", typeof(Canvas));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var panelObject = new GameObject("Character Profile (Runtime)", typeof(RectTransform));
            panelObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(620f, 300f);

            _runtimeProfileText = panelObject.AddComponent<TextMeshProUGUI>();
            _runtimeProfileText.fontSize = 28f;
            _runtimeProfileText.color = Color.white;
            _runtimeProfileText.outlineColor = Color.black;
            _runtimeProfileText.outlineWidth = 0.2f;
            _runtimeProfileText.alignment = TextAlignmentOptions.TopLeft;
            _runtimeProfileText.text = "Click the cyan character to inspect";

            var buttonObject = new GameObject("Close Profile", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(24f, -290f);
            buttonRect.sizeDelta = new Vector2(140f, 44f);

            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            _runtimeCloseButton = buttonObject.GetComponent<Button>();
            _runtimeCloseButton.onClick.AddListener(CloseProfile);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Close";
            label.fontSize = 24f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            buttonObject.SetActive(false);

            var travelObject = new GameObject("Travel", typeof(RectTransform), typeof(Image), typeof(Button));
            travelObject.transform.SetParent(canvas.transform, false);
            RectTransform travelRect = travelObject.GetComponent<RectTransform>();
            travelRect.anchorMin = new Vector2(0f, 1f);
            travelRect.anchorMax = new Vector2(0f, 1f);
            travelRect.pivot = new Vector2(0f, 1f);
            travelRect.anchoredPosition = new Vector2(180f, -290f);
            travelRect.sizeDelta = new Vector2(140f, 44f);
            travelObject.GetComponent<Image>().color = new Color(0.08f, 0.35f, 0.5f, 0.95f);
            _runtimeTravelButton = travelObject.GetComponent<Button>();
            _runtimeTravelButton.onClick.AddListener(TravelSelectedCharacter);

            var travelLabelObject = new GameObject("Label", typeof(RectTransform));
            travelLabelObject.transform.SetParent(travelObject.transform, false);
            RectTransform travelLabelRect = travelLabelObject.GetComponent<RectTransform>();
            travelLabelRect.anchorMin = Vector2.zero;
            travelLabelRect.anchorMax = Vector2.one;
            travelLabelRect.offsetMin = Vector2.zero;
            travelLabelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI travelLabel = travelLabelObject.AddComponent<TextMeshProUGUI>();
            travelLabel.text = "Travel";
            travelLabel.fontSize = 24f;
            travelLabel.color = Color.white;
            travelLabel.alignment = TextAlignmentOptions.Center;
            travelLabel.raycastTarget = false;
            travelObject.SetActive(false);

            CreateUtilityButton(canvas.transform, "Save", new Vector2(340f, -290f), () => _runtimeSave?.Invoke());
            CreateUtilityButton(canvas.transform, "Load", new Vector2(496f, -290f), () => _runtimeLoad?.Invoke());

            var rosterObject = new GameObject("Character Roster (Runtime)", typeof(RectTransform));
            rosterObject.transform.SetParent(canvas.transform, false);
            _runtimeRosterRoot = rosterObject.GetComponent<RectTransform>();
            _runtimeRosterRoot.anchorMin = new Vector2(1f, 1f);
            _runtimeRosterRoot.anchorMax = new Vector2(1f, 1f);
            _runtimeRosterRoot.pivot = new Vector2(1f, 1f);
            _runtimeRosterRoot.anchoredPosition = new Vector2(-24f, -24f);
            _runtimeRosterRoot.sizeDelta = new Vector2(300f, 240f);
        }

        private static void CreateUtilityButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(140f, 44f);
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.2f, 0.95f);
            buttonObject.GetComponent<Button>().onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        /// <summary>Wired by the bootstrapper, which owns the session (§47).</summary>
        public void Initialize(ProjectionPublisher publisher, System.Func<ICommand, string, CommandEnvelope> enqueue)
        {
            if (_publisher != null)
            {
                _publisher.Unsubscribe(OnQuiescence);
            }

            _publisher = publisher;
            _enqueue = enqueue;
            publisher.Subscribe(OnQuiescence);
        }

        /// <summary>
        /// Called only at quiescent boundaries, so the world it reads is internally consistent —
        /// an in-progress mutation is not observable from here (§13.1).
        /// </summary>
        private void OnQuiescence(WorldState world, SimulationContext context)
        {
            _visibleThisRefresh.Clear();
            RefreshRoster(_roster.Project(world));

            // Only characters the player is actually attending to get Unity objects (invariant 69).
            foreach (CharacterId characterId in world.Attention.WatchedCharacters)
            {
                if (!_profiles.TryProject(world, characterId, out CharacterProfileView profile))
                {
                    continue;
                }

                _visibleThisRefresh.Add(characterId.Value);
                CharacterView view = Acquire(characterId);
                view.Apply(profile, PositionFor(profile));

                if (_inspectedCharacter == characterId)
                {
                    ShowProfile(profile);
                }
            }

            ReleaseViewsThatLeft();
        }

        private void RefreshRoster(IReadOnlyList<CharacterRosterEntryView> entries)
        {
            _followedCharacters.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterRosterEntryView entry = entries[i];
                if (entry.IsFollowed)
                {
                    _followedCharacters.Add(entry.CharacterId);
                }

                if (!_rosterButtons.TryGetValue(entry.CharacterId, out Button button))
                {
                    button = CreateRosterButton(new CharacterId(entry.CharacterId), i);
                    _rosterButtons.Add(entry.CharacterId, button);
                    _rosterLabels.Add(entry.CharacterId, button.GetComponentInChildren<TextMeshProUGUI>());
                }

                button.GetComponent<Image>().color = entry.IsFollowed
                    ? new Color(0.08f, 0.42f, 0.32f, 0.95f)
                    : new Color(0.22f, 0.22f, 0.22f, 0.95f);
                _rosterLabels[entry.CharacterId].text = $"{(entry.IsFollowed ? "ON" : "OFF")}  {entry.DisplayName}";
            }
        }

        private Button CreateRosterButton(CharacterId characterId, int index)
        {
            var buttonObject = new GameObject($"Roster {characterId.Value}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_runtimeRosterRoot, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 52f);
            rect.sizeDelta = new Vector2(280f, 44f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => ToggleFollow(characterId));

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 22f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return button;
        }

        private void ToggleFollow(CharacterId characterId)
        {
            bool currentlyFollowed = _followedCharacters.Contains(characterId.Value);
            if (currentlyFollowed && _inspectedCharacter == characterId)
            {
                CloseProfile();
            }

            _enqueue?.Invoke(new FollowCharacterCommand(characterId, !currentlyFollowed), "roster-toggle");
        }

        /// <summary>
        /// A semantic interaction: the player tapped a character. The Domain understands none of the
        /// mouse buttons, screen coordinates, or gestures that led here (§46).
        /// </summary>
        public void OnCharacterTapped(CharacterId characterId)
        {
            if (_inspectedCharacter.IsSet && _inspectedCharacter != characterId)
            {
                _enqueue?.Invoke(new InspectCharacterCommand(_inspectedCharacter, false), "switch-profile");
            }

            SetSelectedView(characterId);
            _inspectedCharacter = characterId;
            _enqueue?.Invoke(new InspectCharacterCommand(characterId), "tap");
        }

        private void CloseProfile()
        {
            if (!_inspectedCharacter.IsSet)
            {
                return;
            }

            CharacterId closingCharacter = _inspectedCharacter;
            _inspectedCharacter = CharacterId.None;
            SetSelectedView(CharacterId.None);
            _enqueue?.Invoke(new InspectCharacterCommand(closingCharacter, false), "close-profile");

            _runtimeProfileText.text = "Click the cyan character to inspect";
            _runtimeCloseButton.gameObject.SetActive(false);
            _runtimeTravelButton.gameObject.SetActive(false);
        }

        private void TravelSelectedCharacter()
        {
            if (!_inspectedCharacter.IsSet || _runtimeTravelCommandFactory == null)
            {
                return;
            }

            ICommand command = _runtimeTravelCommandFactory(_inspectedCharacter);
            if (command != null)
            {
                _enqueue?.Invoke(command, "travel-button");
            }
        }

        private void SetSelectedView(CharacterId selectedCharacter)
        {
            foreach (KeyValuePair<int, CharacterView> pair in _activeViews)
            {
                pair.Value.SetSelected(selectedCharacter.IsSet && pair.Key == selectedCharacter.Value);
            }
        }

        /// <summary>Aggregated visibility transition, emitted once rather than per frame (§25).</summary>
        public void OnCharacterBecameVisible(CharacterId characterId) =>
            _enqueue?.Invoke(new BeginObservingCharacterCommand(characterId), "camera");

        public void OnCharacterLeftView(CharacterId characterId) =>
            _enqueue?.Invoke(new EndObservingCharacterCommand(characterId), "camera");

        private CharacterView Acquire(CharacterId characterId)
        {
            if (_activeViews.TryGetValue(characterId.Value, out CharacterView existing))
            {
                return existing;
            }

            CharacterView view = _pool.Count > 0 ? _pool.Pop() : Instantiate(characterViewPrefab, viewRoot);
            view.gameObject.SetActive(true);
            view.Bind(characterId, OnCharacterTapped);
            _activeViews.Add(characterId.Value, view);
            return view;
        }

        private void ShowProfile(CharacterProfileView profile)
        {
            if (_runtimeProfileText == null)
            {
                return;
            }

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

            _runtimeProfileText.text =
                $"{profile.DisplayName}\n" +
                $"Activity: {profile.CurrentActivityLabel}\n" +
                $"Location: {profile.LocationLabel}\n" +
                needs;
            _runtimeCloseButton.gameObject.SetActive(true);
            _runtimeTravelButton.gameObject.SetActive(!profile.IsTraveling);
        }

        private void ReleaseViewsThatLeft()
        {
            var departed = new List<int>();

            foreach (KeyValuePair<int, CharacterView> pair in _activeViews)
            {
                if (!_visibleThisRefresh.Contains(pair.Key))
                {
                    departed.Add(pair.Key);
                }
            }

            for (int i = 0; i < departed.Count; i++)
            {
                CharacterView view = _activeViews[departed[i]];
                _activeViews.Remove(departed[i]);
                view.gameObject.SetActive(false);
                _pool.Push(view);
            }
        }

        /// <summary>
        /// Placeholder mapping from a location to a screen-space position.
        /// <para>
        /// Whether the game ends up 2D, 2.5D, or stylized 3D is deliberately not frozen (§44, §57), so
        /// this is the seam where that decision will land — not something the simulation ever knows about.
        /// </para>
        /// </summary>
        private static Vector3 PositionFor(CharacterProfileView profile)
        {
            Vector3 destination = PositionForLocation(profile.LocationLabel, profile.CharacterId);
            if (!profile.IsTraveling || string.IsNullOrEmpty(profile.TravelOriginLabel))
            {
                return destination;
            }

            Vector3 origin = PositionForLocation(profile.TravelOriginLabel, profile.CharacterId);
            return Vector3.Lerp(origin, destination, Mathf.Clamp01(profile.TravelProgressBasisPoints / 10000f));
        }

        private static Vector3 PositionForLocation(string locationLabel, int characterId)
        {
            if (locationLabel.StartsWith("Demo Room"))
            {
                return new Vector3(-3f, 0f, 0f);
            }

            if (locationLabel.StartsWith("Demo Cafe"))
            {
                return new Vector3(0f, 0f, 0f);
            }

            if (locationLabel.StartsWith("Demo Workshop"))
            {
                return new Vector3(3f, 0f, 0f);
            }

            float x = characterId % 8;
            float z = characterId / 8;
            return new Vector3(x * 2f, 0f, z * 2f);
        }
    }
}
