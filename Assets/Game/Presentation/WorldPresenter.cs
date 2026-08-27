using System;
using System.Collections.Generic;
using UnityEngine;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Presentation
{
    /// <summary>Projects settled simulation state into authored Unity views and semantic UI actions.</summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        [SerializeField] private CharacterView characterViewPrefab;
        [SerializeField] private Transform viewRoot;
        [SerializeField] private CharacterProfilePanel profilePanel;
        [SerializeField] private CharacterRosterPanel rosterPanel;
        [SerializeField] private DecisionPanel decisionPanel;
        [SerializeField] private WorldLocationPanel locationPanel;
        [SerializeField] private NotificationRecapPanel notificationPanel;

        private readonly Dictionary<int, CharacterView> _activeViews = new Dictionary<int, CharacterView>();
        private readonly Stack<CharacterView> _pool = new Stack<CharacterView>();
        private readonly HashSet<int> _visibleThisRefresh = new HashSet<int>();
        private readonly HashSet<int> _followedCharacters = new HashSet<int>();
        private readonly CharacterProfileProjector _profiles = new CharacterProfileProjector();
        private CharacterRosterProjector _roster;
        private readonly DecisionHistoryProjector _decisionHistory = new DecisionHistoryProjector();
        private readonly NudgeEconomyProjector _nudgeEconomy = new NudgeEconomyProjector();
        private readonly DecisionInterventionResourceProjector _interventionResources =
            new DecisionInterventionResourceProjector();

        private ProjectionPublisher _publisher;
        private Func<ICommand, string, CommandEnvelope> _enqueue;
        private CharacterId _inspectedCharacter;
        private DecisionProjector _decisionProjector;
        private DecisionFeedProjector _decisionFeed;
        private IReadOnlyDictionary<AuthoredId, InterventionDefinition> _interventions;
        private DecisionHoldPolicy _holds;
        private DecisionId _selectedDecisionId;
        private WorldState _lastWorld;
        private readonly LocationProjector _locations = new LocationProjector();
        private IReadOnlyList<LocationId> _managedLocations = new LocationId[0];
        private LocationId _selectedLocationId;
        private NotificationRecapProjector _notifications;
        private SimTime? _offlineRecapSince;

        public int ActiveViewCount => _activeViews.Count;

        public int PooledViewCount => _pool.Count;

        public bool HasActiveView(CharacterId characterId) => _activeViews.ContainsKey(characterId.Value);

        public void ValidateConfiguration()
        {
            if (characterViewPrefab == null || viewRoot == null || profilePanel == null || rosterPanel == null || decisionPanel == null)
            {
                throw new InvalidOperationException(
                    "WorldPresenter requires authored character, profile, roster, and decision presentation references.");
            }
        }

        public void ConfigureDecisionContent(IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions)
        {
            _interventions = interventions ?? throw new ArgumentNullException(nameof(interventions));
            RebuildDecisionProjector();
        }

        public void ConfigureRoster(DecisionImportancePolicyDefinition importance, DecisionHoldPolicy holds)
        {
            _roster = new CharacterRosterProjector(importance, holds);
            _holds = holds ?? throw new ArgumentNullException(nameof(holds));
            _decisionFeed = new DecisionFeedProjector(importance, holds);
            _notifications = new NotificationRecapProjector(importance);
            RebuildDecisionProjector();
        }

        public void ConfigureLocations(IReadOnlyList<LocationId> locations)
        {
            _managedLocations = locations ?? throw new ArgumentNullException(nameof(locations));
            _selectedLocationId = locations.Count == 0 ? LocationId.None : locations[0];
            if (_lastWorld != null) RefreshLocationPanel(_lastWorld);
        }

        private void RebuildDecisionProjector()
        {
            if (_interventions != null)
                _decisionProjector = new DecisionProjector(_interventions, _holds);
        }

        public void Initialize(ProjectionPublisher publisher, Func<ICommand, string, CommandEnvelope> enqueue)
        {
            ValidateConfiguration();

            if (_publisher != null)
            {
                _publisher.Unsubscribe(OnQuiescence);
            }

            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
            profilePanel.Configure(CloseProfile);
            decisionPanel.Configure(HoldDecision, ReleaseDecision, InterveneInDecision, SelectDecision);
            if (locationPanel == null)
                locationPanel = WorldLocationPanel.CreateRuntime(decisionPanel.transform.parent);
            locationPanel.Configure(SelectLocation, ToggleLocationAvailability);
            if (notificationPanel == null)
                notificationPanel = NotificationRecapPanel.CreateRuntime(decisionPanel.transform.parent);
            notificationPanel.Configure(OpenNotification);
            _publisher.Subscribe(OnQuiescence);
        }

        public void PrepareForWorldReload()
        {
            _offlineRecapSince = null;
            _inspectedCharacter = CharacterId.None;
            _selectedDecisionId = DecisionId.None;
            SetSelectedView(CharacterId.None);
            profilePanel.ShowPrompt("World loaded — click a character to inspect");
        }

        private void OnQuiescence(WorldState world, SimulationContext context)
        {
            _lastWorld = world;
            if (_roster == null)
            {
                _roster = new CharacterRosterProjector();
            }
            IReadOnlyList<CharacterRosterEntryView> roster = _roster.Project(world);
            _followedCharacters.Clear();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsFollowed)
                {
                    _followedCharacters.Add(roster[i].CharacterId);
                }
            }

            rosterPanel.Apply(roster, ToggleFollow);
            RefreshDecisionPanel(world);
            decisionPanel.ApplyHistory(_decisionHistory.Project(world));
            RefreshLocationPanel(world);
            RefreshNotifications(world, context);
            _visibleThisRefresh.Clear();

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
                    profilePanel.Apply(profile);
                }
            }

            ReleaseViewsThatLeft();
        }

        private void RefreshLocationPanel(WorldState world)
        {
            if (locationPanel == null || !_selectedLocationId.IsSet ||
                !_locations.TryProject(world, _selectedLocationId, out LocationView view)) return;
            locationPanel.Apply(view, _managedLocations);
        }

        private void SelectLocation(LocationId locationId)
        {
            _selectedLocationId = locationId;
            if (_lastWorld != null) RefreshLocationPanel(_lastWorld);
        }

        private void ToggleLocationAvailability(LocationId locationId)
        {
            if (_lastWorld == null || !_locations.TryProject(_lastWorld, locationId, out LocationView view)) return;
            _enqueue?.Invoke(new SetLocationAvailabilityCommand(locationId, !view.IsOpen), "location-availability");
        }

        public void BeginOfflineRecap(SimTime since) => _offlineRecapSince = since;

        private void RefreshNotifications(WorldState world, SimulationContext context)
        {
            if (notificationPanel == null || _notifications == null) return;
            SimTime? since = context.Mode == Vivarium.Domain.Simulation.SimulationMode.OfflineCatchUp
                ? _offlineRecapSince
                : null;
            notificationPanel.Apply(_notifications.Project(world, context.Mode, since));
            if (context.Mode != Vivarium.Domain.Simulation.SimulationMode.OfflineCatchUp)
                _offlineRecapSince = null;
        }

        private void OpenNotification(NotificationEntryView entry)
        {
            if (entry.DecisionId > 0)
            {
                _selectedDecisionId = new DecisionId(entry.DecisionId);
                if (_lastWorld != null && _lastWorld.Decisions.TryGet(_selectedDecisionId, out Decision _))
                    RefreshDecisionPanel(_lastWorld, preserveExplicitSelection: true);
                return;
            }
            if (entry.CharacterId > 0)
            {
                OnCharacterTapped(new CharacterId(entry.CharacterId));
                return;
            }
            if (entry.LocationId > 0)
                SelectLocation(new LocationId(entry.LocationId));
        }

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

        public void OnCharacterBecameVisible(CharacterId characterId) =>
            _enqueue?.Invoke(new BeginObservingCharacterCommand(characterId), "camera");

        public void OnCharacterLeftView(CharacterId characterId) =>
            _enqueue?.Invoke(new EndObservingCharacterCommand(characterId), "camera");

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
            profilePanel.ShowPrompt("Click a character to inspect");
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

        private void RefreshDecisionPanel(WorldState world, bool preserveExplicitSelection = false)
        {
            if (_decisionProjector == null || _decisionFeed == null)
            {
                return;
            }

            DecisionFeedView feed = _decisionFeed.Project(world);
            bool selectionStillSurfaced = false;
            for (int i = 0; i < feed.Entries.Count; i++)
            {
                if (feed.Entries[i].DecisionId == _selectedDecisionId.Value)
                {
                    selectionStillSurfaced = true;
                    break;
                }
            }

            if (!selectionStillSurfaced && !preserveExplicitSelection)
                _selectedDecisionId = feed.Entries.Count == 0
                    ? DecisionId.None
                    : new DecisionId(feed.Entries[0].DecisionId);

            decisionPanel.ApplyFeed(feed, _selectedDecisionId);
            decisionPanel.ApplyResources(
                _nudgeEconomy.Project(world),
                _interventionResources.Project(world));

            if (_selectedDecisionId.IsSet && world.Decisions.TryGet(_selectedDecisionId, out Decision selected))
            {
                decisionPanel.Apply(_decisionProjector.Project(world, selected));
                return;
            }

            decisionPanel.ShowNoDecision();
        }

        private void HoldDecision(DecisionId decisionId) =>
            _enqueue?.Invoke(new HoldDecisionCommand(decisionId), "hold-button");

        private void ReleaseDecision(DecisionId decisionId) =>
            _enqueue?.Invoke(new ReleaseDecisionCommand(decisionId), "release-button");

        private void InterveneInDecision(
            DecisionId decisionId,
            DecisionInfluenceId influenceId,
            AuthoredId interventionDefinitionId)
        {
            if (interventionDefinitionId.IsSet)
            {
                _enqueue?.Invoke(
                    new ApplyDecisionInterventionCommand(decisionId, interventionDefinitionId, influenceId),
                    "intervene-button");
            }
        }

        private void SelectDecision(DecisionId decisionId)
        {
            _selectedDecisionId = decisionId;
            if (_lastWorld != null) RefreshDecisionPanel(_lastWorld);
        }

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

        private void SetSelectedView(CharacterId selectedCharacter)
        {
            foreach (KeyValuePair<int, CharacterView> pair in _activeViews)
            {
                pair.Value.SetSelected(selectedCharacter.IsSet && pair.Key == selectedCharacter.Value);
            }
        }

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
            if (locationLabel.StartsWith("Mina's flat")) return new Vector3(-3f, 0f, 0f);
            if (locationLabel.StartsWith("Eastmarket Commons")) return new Vector3(0f, 0f, 0f);
            if (locationLabel.StartsWith("East Market Bakery")) return new Vector3(3f, 0f, 0f);

            float x = characterId % 8;
            float z = characterId / 8;
            return new Vector3(x * 2f, 0f, z * 2f);
        }
    }
}
