using System.Collections.Generic;
using UnityEngine;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Vivarium.Unity.Authoring;
using Vivarium.Unity.Infrastructure;
using Vivarium.Unity.Presentation;

namespace Vivarium.Unity.Bootstrap
{
    /// <summary>
    /// Unity's composition root and the bridge between frame time and simulation time (§47, §9).
    /// <para>
    /// Unity's job here is to host and present: build the catalog from authoring assets, compose the
    /// simulation through the shared <see cref="SimulationBootstrapper"/>, hand the presenter its
    /// projection feed, and convert elapsed frame time into simulation minutes. It decides nothing about
    /// what is true in the world.
    /// </para>
    /// <para>
    /// The conversion is the important part: <c>Time.deltaTime</c> accumulates into whole simulation
    /// minutes and then advances the world in integral units. No game rule ever sees a delta time (§9).
    /// </para>
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        private static readonly AuthoredId DecisionLeaveWork = new AuthoredId("decision.leave_work_early");
        private static readonly AuthoredId ContextWorkPressure = new AuthoredId("decision_context.work_pressure");
        private static readonly AuthoredId ModifierDislikedColleague = new AuthoredId("activity_modifier.disliked_colleague_present");
        private static readonly AuthoredId InfluenceBadWorkContext = new AuthoredId("Difficult work context");
        [Header("Content")]
        [SerializeField] private ContentPackAsset contentPack;

        [Header("World")]
        [Tooltip("Seed for every random stream in the world (§14).")]
        [SerializeField] private long worldSeed = 827119;

        [Tooltip("Bumped when simulation rules change. Recorded in saves and traces (§38, §53).")]
        [SerializeField] private int simulationRulesVersion = 1;

        [SerializeField] private int startDay;

        [SerializeField] private int startHour = 7;

        [Header("Time")]
        [Tooltip("Simulation minutes that elapse per real second at 1x speed.")]
        [SerializeField] private float simMinutesPerRealSecond = 1f;

        [SerializeField] private float speedMultiplier = 1f;

        [Header("Demo Test")]
        [Tooltip("Seeds followed characters so the Unity presentation pipeline can be smoke-tested.")]
        [SerializeField] private bool seedDemoCharacter = true;

        [Header("Presentation")]
        [SerializeField] private WorldPresenter presenter;

        [SerializeField] private TimeDisplay timeDisplay;

        private SimulationHost _host;
        private DefinitionCatalog _catalog;
        private InMemorySaveGameStore _saveStore;
        private float _accumulatedMinutes;
        private readonly List<LocationId> _demoLocations = new List<LocationId>();
        private readonly List<CharacterId> _demoCharacters = new List<CharacterId>();

        /// <summary>The composed simulation. Null until <see cref="Awake"/> has run.</summary>
        public SimulationHost Host => _host;

        private void Awake()
        {
            if (timeDisplay == null)
            {
                timeDisplay = FindAnyObjectByType<TimeDisplay>();
            }

            _catalog = contentPack != null
                ? contentPack.Build()
                : throw new System.InvalidOperationException("GameBootstrapper needs a content pack.");

            _saveStore = new InMemorySaveGameStore();

            // A real save format is deliberately still open (§57). Until a serializer is chosen, the
            // in-memory store keeps save/load exercised without committing to an encoding.
            _host = SimulationBootstrapper.CreateNewWorld(
                worldSeed,
                SimTime.FromClockTime(startDay, startHour, 0),
                _catalog,
                simulationRulesVersion,
                trace: null,
                saveStore: _saveStore,
                realWorldClock: new UnityRealWorldClock());
            ConfigureDemoRules(_host);

            if (presenter == null)
            {
                presenter = FindAnyObjectByType<WorldPresenter>();
            }

            if (presenter == null)
            {
                var presenterObject = new GameObject("World Presenter (Runtime)");
                presenter = presenterObject.AddComponent<WorldPresenter>();
            }

            presenter.ValidateConfiguration();
            presenter.ConfigureTravel(CreateDemoTravelCommand);
            presenter.ConfigureDecisionContent(_host.Catalog.Interventions);

            presenter.Initialize(_host.Projections, (command, diagnostics) => _host.Session.Enqueue(command, diagnostics));

            if (seedDemoCharacter)
            {
                SeedDemoCharacters();
            }

            _host.Projections.OnQuiescence(_host.World, _host.Simulation);

            timeDisplay?.SetTime(_host.World.Clock.Now);
        }

        private void SeedDemoCharacters()
        {
            _demoLocations.Clear();
            _demoCharacters.Clear();
            LocationId room = SeedDemoLocation("Demo Room");
            LocationId cafe = SeedDemoLocation("Demo Cafe");
            LocationId workshop = SeedDemoLocation("Demo Workshop");

            CharacterId mina = SeedDemoCharacter("Mina Test", room, 5592);
            CharacterId glen = SeedDemoCharacter("Glen Test", room, 2000);
            CharacterId darius = SeedDemoCharacter("Darius Test", workshop, 1000);

            var walking = new AuthoredId("travel_mode.walking");
            _host.World.TravelNetwork.ConnectBidirectional(room, workshop, SimDuration.FromMinutes(30), walking);
            _host.World.TravelNetwork.ConnectBidirectional(room, cafe, SimDuration.FromMinutes(10), walking);
            _host.World.TravelNetwork.ConnectBidirectional(cafe, workshop, SimDuration.FromMinutes(20), walking);

            SeedNegativeRelationship(mina, darius);
            _host.Transitions.BeginActivity(
                _host.Simulation,
                darius,
                ActivityWorking,
                workshop,
                SimDuration.FromHours(2));

            SeedWorkCommitment(mina, workshop);
            SeedWorkCommitment(glen, workshop);
            _host.Session.Advance(SimDuration.Zero);
        }

        private LocationId SeedDemoLocation(string locationName)
        {
            var location = new LocationNode(
                _host.World.RuntimeIds.Locations.Next(),
                LocationId.None,
                new AuthoredId("location_kind.building"),
                locationName);
            _host.World.Locations.Add(location);
            _demoLocations.Add(location.Id);
            return location.Id;
        }

        private CharacterId SeedDemoCharacter(string characterName, LocationId locationId, long initialHunger)
        {
            var character = new Character(
                _host.World.RuntimeIds.Characters.Next(),
                characterName,
                _host.World.Clock.Now);

            _host.World.Characters.Add(character.Id, character);
            _demoCharacters.Add(character.Id);

            NeedDefinition hunger = _host.Catalog.Needs[new AuthoredId("need.hunger")];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(
                    initialHunger,
                    _host.World.Clock.Now,
                    hunger.DefaultRateNumerator,
                    hunger.DefaultRateDenominator,
                    hunger.MinValue,
                    hunger.MaxValue),
                DemoDecisionThresholdFor(hunger.Id, hunger.MaxValue));
            character.SetNeed(hungerState);
            _host.Needs.Rearm(_host.Simulation, character, hungerState);

            _host.Transitions.BeginActivity(
                _host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                locationId,
                SimDuration.FromDays(1));
            _host.WatchSignals.SetFollowed(_host.World, character.Id, true);
            return character.Id;
        }

        private void SeedNegativeRelationship(CharacterId mina, CharacterId darius)
        {
            var relationship = new Relationship(
                _host.World.RuntimeIds.Relationships.Next(),
                mina,
                darius,
                new AuthoredId("relationship.disliked_colleague"),
                AnalyticalProgression.Constant(-5000, _host.World.Clock.Now),
                _host.World.Clock.Now);
            _host.World.Relationships.Add(relationship.Id, relationship);
            _host.World.RelationshipIndex.Register(relationship);
        }

        private void SeedWorkCommitment(CharacterId characterId, LocationId workshop)
        {
            SimTime startsAt = _host.World.Clock.Now.Plus(SimDuration.FromMinutes(32));
            var commitment = new Commitment(
                _host.World.RuntimeIds.Commitments.Next(),
                characterId,
                new AuthoredId("commitment.demo_work_shift"),
                startsAt,
                startsAt.Plus(SimDuration.FromMinutes(5)),
                SimDuration.FromHours(2),
                workshop,
                priority: 100,
                activityDefinitionId: ActivityWorking,
                sourceTemplateId: new AuthoredId("routine.demo_work_shift"));
            _host.World.Commitments.Add(commitment.Id, commitment);
            _host.World.BumpRevision(commitment.ScheduleRevisionKey);
            _host.Planner.TryPlanCommitmentStart(_host.Simulation, commitment);
        }

        private static void ConfigureDemoRules(SimulationHost host)
        {
            host.DecisionReevaluation.Register(new ActivityContextInfluenceReevaluator(
                DecisionLeaveWork,
                ContextWorkPressure,
                ModifierDislikedColleague,
                InfluenceBadWorkContext,
                Die.D10,
                Die.D6));
            var workPressure = new WorkContextPressureService(
                host.Transitions,
                host.DecisionReevaluation,
                ActivityWorking,
                ModifierDislikedColleague,
                ContextWorkPressure,
                affinityThreshold: -1000,
                pressuredRate: -2);
            host.DomainEventHandlers.Register(new WorkContextArrivalHandler(workPressure), 200);
            host.DomainEventHandlers.Register(new WorkContextDepartureHandler(workPressure), 200);
        }

        private long DemoDecisionThresholdFor(AuthoredId needId, long fallback)
        {
            foreach (KeyValuePair<AuthoredId, DecisionDefinition> pair in _host.Catalog.Decisions)
            {
                NeedThresholdDecisionTrigger trigger = pair.Value.Trigger;
                if (trigger != null && trigger.NeedId == needId)
                {
                    return trigger.Threshold;
                }
            }

            return fallback;
        }

        private ICommand CreateDemoTravelCommand(CharacterId characterId)
        {
            if (_demoLocations.Count == 0 ||
                !_host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext spatial) ||
                !spatial.IsLocated)
            {
                return null;
            }

            int currentIndex = _demoLocations.IndexOf(spatial.LocationId);
            int destinationIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % _demoLocations.Count;
            return new TravelCharacterCommand(characterId, _demoLocations[destinationIndex]);
        }

        public void SaveRuntimeSmokeTest() => _host.Session.Save("runtime-smoke-test");

        public bool LoadRuntimeSmokeTest()
        {
            if (!_saveStore.TryLoad("runtime-smoke-test", out SaveGameData saved))
            {
                return false;
            }

            WorldState restoredWorld = _host.SaveMapper.Restore(saved);
            _host = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                _catalog,
                saved.LastCommandSequence,
                saved.SimulationRulesVersion,
                trace: null,
                saveStore: _saveStore,
                realWorldClock: new UnityRealWorldClock());
            ConfigureDemoRules(_host);

            _accumulatedMinutes = 0f;
            presenter.PrepareForWorldReload();
            presenter.Initialize(_host.Projections, (command, diagnostics) => _host.Session.Enqueue(command, diagnostics));
            _host.Projections.OnQuiescence(_host.World, _host.Simulation);
            timeDisplay?.SetTime(_host.World.Clock.Now);
            return true;
        }

        public void LoadRuntimeSmokeTestFromUi() => LoadRuntimeSmokeTest();

        /// <summary>
        /// Drains queued commands and advances the world by whole simulation minutes.
        /// <para>
        /// Note what is <i>not</i> here: any per-character update. A thousand characters cost a thousand
        /// scheduled events, not a thousand <c>Update</c> calls (§50, invariant 1 of the performance
        /// principles).
        /// </para>
        /// </summary>
        private void Update()
        {
            if (_host == null)
            {
                return;
            }

            // External writes first, each settling to quiescence before the next (§2.2.1).
            _host.Session.Pump();

            _accumulatedMinutes += Time.deltaTime * simMinutesPerRealSecond * speedMultiplier;

            int wholeMinutes = Mathf.FloorToInt(_accumulatedMinutes);
            if (wholeMinutes <= 0)
            {
                return;
            }

            _accumulatedMinutes -= wholeMinutes;

            Domain.Simulation.SimulationMode mode = speedMultiplier > 1f ? Domain.Simulation.SimulationMode.PlayerFastForward : Domain.Simulation.SimulationMode.Live;
            _host.Session.Advance(SimDuration.FromMinutes(wholeMinutes), mode);
            timeDisplay?.SetTime(_host.World.Clock.Now);
        }

        /// <summary>Changes game speed. Presentation concern; the rules do not vary with it (§21).</summary>
        public void SetSpeedMultiplier(float multiplier) => speedMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>Saves at a quiescent boundary (§2.2.1).</summary>
        public SaveGameData Save(string slot) => _host.Session.Save(slot);

        /// <summary>
        /// Applies offline catch-up for a save that was taken earlier (§21).
        /// <para>
        /// The duration comes from the persisted anchor and the real-world clock, computed out here —
        /// the Domain is never handed a wall-clock reading (invariant 32).
        /// </para>
        /// </summary>
        public void ApplyOfflineProgression(SaveGameData saved)
        {
            var offline = new OfflineProgressionService(new UnityRealWorldClock());
            SimDuration elapsed = offline.ElapsedSince(saved);

            if (elapsed.TotalMinutes <= 0)
            {
                return;
            }

            // Publish periodically during a long catch-up, but only at safe boundaries (§13.1).
            _host.Session.Advance(elapsed, Domain.Simulation.SimulationMode.OfflineCatchUp, publishEveryInstants: 500);
        }
    }
}
