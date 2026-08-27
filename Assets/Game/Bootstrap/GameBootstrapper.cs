using System.Collections.Generic;
using UnityEngine;
using Vivarium.Application.Commands;
using Vivarium.Application.Content;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Application.Queries;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Social;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Scheduling;
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
        private static readonly ContentDefinitionKey[] BaseGameRequirements =
        {
            new ContentDefinitionKey(ContentDefinitionFamily.Need, WellKnownNeeds.Energy),
            new ContentDefinitionKey(ContentDefinitionFamily.Activity, WellKnownActivities.Sleeping),
        };

        [Header("Content")]
        [SerializeField] private ContentPackIndexAsset contentPackIndex;

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

        [Header("Minimum Playable Scenario")]
        [Tooltip("Populates the shared ten-character production-shaped world.")]
        [SerializeField] private bool seedDemoCharacter = true;

        [Header("Presentation")]
        [SerializeField] private WorldPresenter presenter;

        [SerializeField] private TimeDisplay timeDisplay;

        [SerializeField] private SaveContinuePanel saveContinuePanel;

        private SimulationHost _host;
        private DefinitionCatalog _catalog;
        private ResolvedContent _resolvedContent;
        private ISaveGameStore _saveStore;
        private float _accumulatedMinutes;
        private readonly SimulationStatusProjector _statusProjector = new SimulationStatusProjector();
        private long _offlineReturnMinutes = -1;

        /// <summary>The composed simulation. Null until <see cref="Awake"/> has run.</summary>
        public SimulationHost Host => _host;

        public MinimumPlayableWorldLayout WorldLayout { get; private set; }

        private void Awake()
        {
            if (timeDisplay == null)
            {
                timeDisplay = FindAnyObjectByType<TimeDisplay>();
            }

            if (contentPackIndex == null)
                throw new System.InvalidOperationException("GameBootstrapper needs a content pack index.");
            if (contentPackIndex.Manifest == null)
                throw new System.InvalidOperationException("GameBootstrapper content pack index needs a manifest.");

            ContentPackManifestAsset manifest = contentPackIndex.Manifest;
            ContentOverrideEntry[] authoredOverrides = manifest.Overrides;
            var overrides = new ContentOverrideDeclaration[authoredOverrides.Length];
            for (int i = 0; i < overrides.Length; i++)
            {
                overrides[i] = new ContentOverrideDeclaration(
                    authoredOverrides[i].family,
                    new AuthoredId(authoredOverrides[i].authoredId),
                    authoredOverrides[i].expectedSourcePackId);
            }
            _resolvedContent = ContentPackResolver.Resolve(new[]
            {
                new ContentPackContribution(
                    manifest.PackId,
                    manifest.DisplayName,
                    manifest.PackVersion,
                    contentPackIndex.BuildDefinitionSet(),
                    overrides),
            });
            _catalog = _resolvedContent.Catalog;

            IReadOnlyList<string> baseGameErrors =
                ContentValidator.ValidateRequiredDefinitions(_catalog, BaseGameRequirements);
            if (baseGameErrors.Count > 0)
                throw new System.InvalidOperationException(
                    "BaseGame content requirements failed: " + string.Join("; ", baseGameErrors));

            // Phase 6: persistent saves via JSON+gzip under Unity's platform-specific data path (§48, §57).
            var serializer = new JsonGzipSaveGameSerializer();
            var storage = new UnityPersistentDataPathStorage();
            _saveStore = new PlatformSaveGameStore(storage, serializer);
            _host = SimulationBootstrapper.CreateNewWorld(
                worldSeed,
                SimTime.FromClockTime(startDay, startHour, 0),
                _catalog,
                simulationRulesVersion,
                trace: null,
                saveStore: _saveStore,
                realWorldClock: new UnityRealWorldClock());
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
            presenter.ConfigureDecisionContent(_host.Catalog.Interventions);
            presenter.ConfigureRoster(_host.Catalog.DecisionImportancePolicy, _host.HoldPolicy);

            presenter.Initialize(_host.Projections, (command, diagnostics) => _host.Session.Enqueue(command, diagnostics));

            if (seedDemoCharacter)
            {
                WorldLayout = MinimumPlayableWorld.Populate(_host);
                presenter.ConfigureLocations(new[] { WorldLayout.Home, WorldLayout.Bakery, WorldLayout.Commons });
            }

            ConfigurePersistencePresentation();

            _host.Projections.OnQuiescence(_host.World, _host.Simulation);

            RefreshStatus(Domain.Simulation.SimulationMode.Live);
        }

        public void SaveRuntimeSmokeTest() => SaveToSlot("runtime-smoke-test");

        public bool LoadRuntimeSmokeTest()
        {
            return TryLoadSlot("runtime-smoke-test", out _);
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
            RefreshStatus(mode);
        }

        /// <summary>Changes game speed. Presentation concern; the rules do not vary with it (§21).</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
            _offlineReturnMinutes = -1;
            if (_host != null)
            {
                RefreshStatus(speedMultiplier > 1f
                    ? Domain.Simulation.SimulationMode.PlayerFastForward
                    : Domain.Simulation.SimulationMode.Live);
            }
        }

        /// <summary>Saves at a quiescent boundary (§2.2.1).</summary>
        public SaveGameData Save(string slot) => _host.Session.Save(slot);

        public IReadOnlyList<string> ListSaveSlots() => _saveStore.ListSlots();

        public string SaveToSlot(string slot)
        {
            try
            {
                SaveGameData saved = Save(slot);
                return $"Saved {slot} at {FormatSimTime(saved.ClockMinutes)}.";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save '{slot}': {ex.Message}");
                return $"Save failed: {ex.Message}";
            }
        }

        public string DeleteSaveSlot(string slot)
        {
            try
            {
                return _saveStore.Delete(slot) ? $"Deleted {slot}." : $"No save exists in {slot}.";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to delete '{slot}': {ex.Message}");
                return $"Delete failed: {ex.Message}";
            }
        }

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

            _offlineReturnMinutes = System.Math.Max(0, elapsed.TotalMinutes);
            presenter.BeginOfflineRecap(_host.World.Clock.Now);

            if (elapsed.TotalMinutes <= 0)
            {
                RefreshStatus(Domain.Simulation.SimulationMode.OfflineCatchUp);
                return;
            }

            // Publish periodically during a long catch-up, but only at safe boundaries (§13.1).
            _host.Session.Advance(elapsed, Domain.Simulation.SimulationMode.OfflineCatchUp, publishEveryInstants: 500);
            RefreshStatus(Domain.Simulation.SimulationMode.OfflineCatchUp);
        }

        /// <summary>
        /// Loads a saved game from disk with full migration and diagnostics (Phase 6 §4.2).
        /// <para>
        /// Deserializes → migrates schema → restores world → applies offline catch-up.
        /// All errors are handled and logged; callback receives success/failure.
        /// </para>
        /// </summary>
        public string LoadFromSlot(string slot)
        {
            TryLoadSlot(slot, out string message);
            return message;
        }

        public void LoadSave(string slot, System.Action<bool> onComplete) =>
            onComplete?.Invoke(TryLoadSlot(slot, out _));

        private bool TryLoadSlot(string slot, out string message)
        {
            SaveGameData saved;
            try
            {
                if (_saveStore == null || !_saveStore.TryLoad(slot, out saved))
                {
                    message = $"No save exists in {slot}.";
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to read save '{slot}': {ex.Message}");
                message = $"Load failed — corrupted or unreadable save: {ex.Message}";
                return false;
            }

            var bootstrapper = new SaveLoadBootstrapper(
                new JsonGzipSaveGameSerializer(),
                _host.SaveMapper,
                _catalog.ContentVersion,
                simulationRulesVersion);
            SaveLoadResult result = bootstrapper.TryRestore(saved);
            if (!result.IsSuccess)
            {
                Debug.LogError($"Failed to load save '{slot}' ({result.ErrorKind}): {result.ErrorMessage}");
                message = $"Load failed [{result.ErrorKind}]: {result.ErrorMessage}";
                return false;
            }

            try
            {
                _host = SimulationBootstrapper.CreateFromRestoredWorld(
                    result.World,
                    _catalog,
                    result.SavedData.LastCommandSequence,
                    simulationRulesVersion,
                    trace: null,
                    saveStore: _saveStore,
                    realWorldClock: new UnityRealWorldClock());
                MinimumPlayableWorld.ConfigureScenarioServices(_host);

                _accumulatedMinutes = 0f;
                presenter.PrepareForWorldReload();
                presenter.Initialize(_host.Projections, (command, diagnostics) => _host.Session.Enqueue(command, diagnostics));
                _host.Projections.OnQuiescence(_host.World, _host.Simulation);
                ApplyOfflineProgression(result.SavedData);
                RefreshStatus(Domain.Simulation.SimulationMode.Live);
                message = $"Loaded {slot} — {FormatCompatibility(result.CompatibilityReport)}; " +
                    $"offline catch-up {SimDuration.FromMinutes(_offlineReturnMinutes)}.";
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to restore save '{slot}': {ex.Message}");
                message = $"Load failed [RestorationFailed]: {ex.Message}";
                return false;
            }
        }

        private void ConfigurePersistencePresentation()
        {
            if (saveContinuePanel == null) saveContinuePanel = FindAnyObjectByType<SaveContinuePanel>();
            if (saveContinuePanel == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas == null)
                    throw new System.InvalidOperationException("Save/continue presentation requires a Canvas.");
                saveContinuePanel = SaveContinuePanel.CreateRuntime(canvas.transform);
            }
            saveContinuePanel.Configure(ListSaveSlots, SaveToSlot, LoadFromSlot, DeleteSaveSlot);
        }

        private static string FormatCompatibility(SaveCompatibilityReport report)
        {
            string migrations = report.MigrationSteps.Count == 0
                ? "schema current"
                : "migrated " + string.Join(", ", report.MigrationSteps);
            if (!report.ReproductionIsVersionScoped) return migrations + ", versions match";
            return migrations + $", version drift content={report.ContentVersionDiffers}, " +
                $"rules={report.RulesVersionDiffers}, rng={report.RandomAlgorithmVersionDiffers}";
        }

        private static string FormatSimTime(long totalMinutes) =>
            $"Day {totalMinutes / 1440} {totalMinutes % 1440 / 60:00}:{totalMinutes % 60:00}";

        private void RefreshStatus(Domain.Simulation.SimulationMode mode) =>
            timeDisplay?.Apply(_statusProjector.Project(
                _host.World,
                mode,
                Mathf.RoundToInt(speedMultiplier * 100f),
                _offlineReturnMinutes));
    }
}
