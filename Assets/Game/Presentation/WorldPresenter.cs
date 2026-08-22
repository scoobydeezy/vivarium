using System;
using System.Collections.Generic;
using UnityEngine;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;

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

        private readonly Dictionary<int, CharacterView> _activeViews = new Dictionary<int, CharacterView>();
        private readonly Stack<CharacterView> _pool = new Stack<CharacterView>();
        private readonly HashSet<int> _visibleThisRefresh = new HashSet<int>();
        private readonly HashSet<int> _followedCharacters = new HashSet<int>();
        private readonly CharacterProfileProjector _profiles = new CharacterProfileProjector();
        private readonly CharacterRosterProjector _roster = new CharacterRosterProjector();

        private ProjectionPublisher _publisher;
        private Func<ICommand, string, CommandEnvelope> _enqueue;
        private Func<CharacterId, ICommand> _travelCommandFactory;
        private CharacterId _inspectedCharacter;
        private DecisionProjector _decisionProjector;
        private AuthoredId _interventionDefinitionId;

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

        public void ConfigureTravel(Func<CharacterId, ICommand> commandFactory) =>
            _travelCommandFactory = commandFactory;

        public void ConfigureDecisionContent(IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions)
        {
            _decisionProjector = new DecisionProjector(interventions);
            _interventionDefinitionId = AuthoredId.None;
            foreach (KeyValuePair<AuthoredId, InterventionDefinition> pair in interventions)
            {
                _interventionDefinitionId = pair.Key;
                break;
            }
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
            profilePanel.Configure(CloseProfile, TravelSelectedCharacter);
            decisionPanel.Configure(HoldDecision, ReleaseDecision, InterveneInDecision);
            _publisher.Subscribe(OnQuiescence);
        }

        public void PrepareForWorldReload()
        {
            _inspectedCharacter = CharacterId.None;
            SetSelectedView(CharacterId.None);
            profilePanel.ShowPrompt("World loaded — click a character to inspect");
        }

        private void OnQuiescence(WorldState world, SimulationContext context)
        {
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

        private void TravelSelectedCharacter()
        {
            if (!_inspectedCharacter.IsSet || _travelCommandFactory == null)
            {
                return;
            }

            ICommand command = _travelCommandFactory(_inspectedCharacter);
            if (command != null)
            {
                _enqueue?.Invoke(command, "travel-button");
            }
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

        private void RefreshDecisionPanel(WorldState world)
        {
            if (_decisionProjector == null)
            {
                return;
            }

            foreach (Decision decision in world.Decisions.All)
            {
                decisionPanel.Apply(_decisionProjector.Project(world, decision));
                return;
            }
        }

        private void HoldDecision(DecisionId decisionId) =>
            _enqueue?.Invoke(new HoldDecisionCommand(decisionId), "hold-button");

        private void ReleaseDecision(DecisionId decisionId) =>
            _enqueue?.Invoke(new ReleaseDecisionCommand(decisionId), "release-button");

        private void InterveneInDecision(DecisionId decisionId, DecisionInfluenceId influenceId)
        {
            if (_interventionDefinitionId.IsSet)
            {
                _enqueue?.Invoke(
                    new ApplyDecisionInterventionCommand(decisionId, _interventionDefinitionId, influenceId),
                    "intervene-button");
            }
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
            if (locationLabel.StartsWith("Demo Room")) return new Vector3(-3f, 0f, 0f);
            if (locationLabel.StartsWith("Demo Cafe")) return new Vector3(0f, 0f, 0f);
            if (locationLabel.StartsWith("Demo Workshop")) return new Vector3(3f, 0f, 0f);

            float x = characterId % 8;
            float z = characterId / 8;
            return new Vector3(x * 2f, 0f, z * 2f);
        }
    }
}
