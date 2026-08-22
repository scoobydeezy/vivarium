using UnityEngine;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Session;
using Vivarium.Domain.Content;
using Vivarium.Domain.Simulation;
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

        [Header("Presentation")]
        [SerializeField] private WorldPresenter presenter;

        private SimulationHost _host;
        private float _accumulatedMinutes;

        /// <summary>The composed simulation. Null until <see cref="Awake"/> has run.</summary>
        public SimulationHost Host => _host;

        private void Awake()
        {
            DefinitionCatalog catalog = contentPack != null
                ? contentPack.Build()
                : throw new System.InvalidOperationException("GameBootstrapper needs a content pack.");

            var storage = new UnityPersistentDataStorage();
            var saveStore = new InMemorySaveGameStore();

            // A real save format is deliberately still open (§57). Until a serializer is chosen, the
            // in-memory store keeps save/load exercised without committing to an encoding.
            _host = SimulationBootstrapper.CreateNewWorld(
                worldSeed,
                SimTime.FromClockTime(startDay, startHour, 0),
                catalog,
                simulationRulesVersion,
                trace: null,
                saveStore: saveStore,
                realWorldClock: new UnityRealWorldClock());

            if (presenter != null)
            {
                presenter.Initialize(_host.Projections, (command, diagnostics) => _host.Session.Enqueue(command, diagnostics));
            }
        }

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

            SimulationMode mode = speedMultiplier > 1f ? SimulationMode.PlayerFastForward : SimulationMode.Live;
            _host.Session.Advance(SimDuration.FromMinutes(wholeMinutes), mode);
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
            _host.Session.Advance(elapsed, SimulationMode.OfflineCatchUp, publishEveryInstants: 500);
        }
    }
}
