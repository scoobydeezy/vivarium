using System;
using System.Collections.Generic;
using Vivarium.Application.Commands;
using Vivarium.Application.Commands.Handlers;
using Vivarium.Domain.Content;
using Vivarium.Application.Observation;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Application.Queries;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Infrastructure.Bootstrap
{
    /// <summary>Everything a composed simulation hands back to its host (§47).</summary>
    public sealed class SimulationHost
    {
        public SimulationHost(
            GameSession session,
            ProjectionPublisher projections,
            ActivityTransitionService transitions,
            SchedulePlanner planner,
            NeedProgressionService needs,
            ActivityResolutionRegistry activityResolution,
            DecisionResolutionService decisionResolution,
            DecisionReevaluationService decisionReevaluation,
            KnowledgeDiscoveryService knowledgeDiscovery,
            WatchSignalService watchSignals,
            InteractionCandidateSelector interactionCandidates,
            DecisionHoldPolicy holdPolicy,
            SaveGameMapper saveMapper,
            OrderedDomainEventHandlerRegistry domainEventHandlers,
            ScheduledEventHandlerRegistry scheduledEventHandlers,
            CommandDispatcher dispatcher,
            DefinitionCatalog catalog)
        {
            Session = session;
            Projections = projections;
            Transitions = transitions;
            Planner = planner;
            Needs = needs;
            ActivityResolution = activityResolution;
            DecisionResolution = decisionResolution;
            DecisionReevaluation = decisionReevaluation;
            KnowledgeDiscovery = knowledgeDiscovery;
            WatchSignals = watchSignals;
            InteractionCandidates = interactionCandidates;
            HoldPolicy = holdPolicy;
            SaveMapper = saveMapper;
            DomainEventHandlers = domainEventHandlers;
            ScheduledEventHandlers = scheduledEventHandlers;
            Dispatcher = dispatcher;
            Catalog = catalog;
        }

        public GameSession Session { get; }

        public WorldState World => Session.World;

        public SimulationContext Simulation => Session.Simulation;

        public ProjectionPublisher Projections { get; }

        public ActivityTransitionService Transitions { get; }

        public SchedulePlanner Planner { get; }

        public NeedProgressionService Needs { get; }

        public ActivityResolutionRegistry ActivityResolution { get; }

        public DecisionResolutionService DecisionResolution { get; }

        public DecisionReevaluationService DecisionReevaluation { get; }

        public KnowledgeDiscoveryService KnowledgeDiscovery { get; }

        public WatchSignalService WatchSignals { get; }

        public InteractionCandidateSelector InteractionCandidates { get; }

        public DecisionHoldPolicy HoldPolicy { get; }

        public SaveGameMapper SaveMapper { get; }

        public OrderedDomainEventHandlerRegistry DomainEventHandlers { get; }

        public ScheduledEventHandlerRegistry ScheduledEventHandlers { get; }

        public CommandDispatcher Dispatcher { get; }

        public DefinitionCatalog Catalog { get; }
    }

    /// <summary>
    /// The composition root (§47).
    /// <para>
    /// One obvious place where runtime dependencies are constructed, in the order the architecture
    /// describes: catalog → seed and random oracle → domain services → scheduler and event registries →
    /// application services → infrastructure adapters. Manual constructor injection, deliberately — a DI
    /// framework should wait for demonstrated composition pain.
    /// </para>
    /// <para>
    /// Shared by the headless runner, the test suite, and Unity's bootstrapper, so all three compose the
    /// same world. That is also what keeps the runner honest as a check that Unity has not leaked into
    /// the core (§52).
    /// </para>
    /// </summary>
    public static class SimulationBootstrapper
    {
        /// <summary>Builds a fresh world.</summary>
        public static SimulationHost CreateNewWorld(
            long worldSeed,
            SimTime startTime,
            DefinitionCatalog catalog,
            int simulationRulesVersion = 1,
            ISimulationTrace trace = null,
            ISaveGameStore saveStore = null,
            IRealWorldClock realWorldClock = null) =>
            Compose(
                new WorldState(worldSeed, startTime),
                catalog,
                simulationRulesVersion,
                trace,
                saveStore,
                realWorldClock,
                0);

        /// <summary>
        /// Rebuilds a host around a restored world (§40).
        /// <para>
        /// The world arrives already reconstructed and index-rebuilt from
        /// <see cref="SaveGameMapper.Restore"/>; this only re-composes the services around it, so the
        /// resumed session is wired identically to the one that saved.
        /// </para>
        /// </summary>
        public static SimulationHost CreateFromRestoredWorld(
            WorldState restoredWorld,
            DefinitionCatalog catalog,
            long restoredCommandSequence,
            int simulationRulesVersion = 1,
            ISimulationTrace trace = null,
            ISaveGameStore saveStore = null,
            IRealWorldClock realWorldClock = null) =>
            Compose(
                restoredWorld,
                catalog,
                simulationRulesVersion,
                trace,
                saveStore,
                realWorldClock,
                restoredCommandSequence);

        private static SimulationHost Compose(
            WorldState world,
            DefinitionCatalog catalog,
            int simulationRulesVersion,
            ISimulationTrace trace,
            ISaveGameStore saveStore,
            IRealWorldClock realWorldClock,
            long restoredCommandSequence)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyList<string> contentErrors = ContentValidator.Validate(catalog);
            if (contentErrors.Count > 0)
            {
                // Content problems must surface before gameplay, not as a null reference three
                // simulated years in (§42).
                throw new InvalidOperationException("Content validation failed: " + string.Join("; ", ToArray(contentErrors)));
            }

            // Randomness: one oracle, derived from the world seed (§14).
            IRandomOracle random = new DeterministicRandomOracle(world.WorldSeed);

            // Domain services.
            var transitions = new ActivityTransitionService();
            var planner = new SchedulePlanner();
            var needs = new NeedProgressionService();
            var activityResolution = new ActivityResolutionRegistry();
            var decisionResolution = new DecisionResolutionService();
            var decisionReevaluation = new DecisionReevaluationService();
            var holdPolicy = new DecisionHoldPolicy(maxGlobalHeld: 12, maxHeldPerCharacter: 3);
            var interactionCandidates = new InteractionCandidateSelector(random);

            var knowledgeDiscovery = new KnowledgeDiscoveryService();
            knowledgeDiscovery.RegisterProvider(new CharacterFactProvider(catalog.Traits));

            // Scheduler handler registry. Registration is explicit and ordered by hand (§11.3, §12.1).
            var scheduledHandlers = new ScheduledEventHandlerRegistry();
            scheduledHandlers.Register(new ActivityStartHandler(transitions));
            scheduledHandlers.Register(new TravelArrivalHandler(transitions));
            scheduledHandlers.Register(new ActivityCompletionHandler(activityResolution, transitions));
            scheduledHandlers.Register(new NeedThresholdHandler());
            scheduledHandlers.Register(new DecisionResolveHandler(decisionResolution, holdPolicy));

            // Domain Event chains start empty: content registers reactions with explicit orders.
            var domainHandlers = new OrderedDomainEventHandlerRegistry();

            var settlement = new SettlementLoop(scheduledHandlers, domainHandlers);
            var projections = new ProjectionPublisher();
            var runner = new SimulationRunner(settlement, projections);

            // Application services.
            var watchSignals = new WatchSignalService(knowledgeDiscovery);
            var saveMapper = new SaveGameMapper(ScheduledEventPayloadCodecRegistry.WithBuiltIns());

            var dispatcher = new CommandDispatcher();
            dispatcher.Register(new AdvanceSimulationHandler(runner));
            dispatcher.Register(new FollowCharacterHandler(watchSignals));
            dispatcher.Register(new BeginObservingCharacterHandler(watchSignals));
            dispatcher.Register(new EndObservingCharacterHandler(watchSignals));
            dispatcher.Register(new InspectCharacterHandler(watchSignals));
            dispatcher.Register(new TravelCharacterHandler(transitions));
            dispatcher.Register(new HoldDecisionHandler(holdPolicy));
            dispatcher.Register(new ReleaseDecisionHandler());
            dispatcher.Register(new ApplyDecisionInterventionHandler(catalog.Interventions));
            dispatcher.Register(new SubmitActivityPerformanceHandler(activityResolution));
            dispatcher.Register(new BuildLocationHandler());
            dispatcher.Register(new SetAttentionPolicyHandler());

            var session = new GameSession(
                world,
                random,
                dispatcher,
                runner,
                catalog.ContentVersion,
                simulationRulesVersion,
                saveMapper,
                saveStore,
                realWorldClock,
                trace,
                restoredCommandSequence);

            return new SimulationHost(
                session,
                projections,
                transitions,
                planner,
                needs,
                activityResolution,
                decisionResolution,
                decisionReevaluation,
                knowledgeDiscovery,
                watchSignals,
                interactionCandidates,
                holdPolicy,
                saveMapper,
                domainHandlers,
                scheduledHandlers,
                dispatcher,
                catalog);
        }

        private static string[] ToArray(IReadOnlyList<string> values)
        {
            var array = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                array[i] = values[i];
            }

            return array;
        }
    }
}
