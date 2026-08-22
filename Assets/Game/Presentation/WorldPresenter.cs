using System.Collections.Generic;
using UnityEngine;
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
        private readonly CharacterProfileProjector _profiles = new CharacterProfileProjector();

        private System.Func<ICommand, string, CommandEnvelope> _enqueue;

        /// <summary>Wired by the bootstrapper, which owns the session (§47).</summary>
        public void Initialize(ProjectionPublisher publisher, System.Func<ICommand, string, CommandEnvelope> enqueue)
        {
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
            }

            ReleaseViewsThatLeft();
        }

        /// <summary>
        /// A semantic interaction: the player tapped a character. The Domain understands none of the
        /// mouse buttons, screen coordinates, or gestures that led here (§46).
        /// </summary>
        public void OnCharacterTapped(CharacterId characterId) =>
            _enqueue?.Invoke(new InspectCharacterCommand(characterId), "tap");

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
            view.Bind(characterId);
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

        /// <summary>
        /// Placeholder mapping from a location to a screen-space position.
        /// <para>
        /// Whether the game ends up 2D, 2.5D, or stylized 3D is deliberately not frozen (§44, §57), so
        /// this is the seam where that decision will land — not something the simulation ever knows about.
        /// </para>
        /// </summary>
        private static Vector3 PositionFor(CharacterProfileView profile)
        {
            float x = profile.CharacterId % 8;
            float z = profile.CharacterId / 8;
            return new Vector3(x * 2f, 0f, z * 2f);
        }
    }
}
