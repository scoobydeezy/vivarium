using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;

namespace Vivarium.Application.Tests
{
    /// <summary>Shared fixture: a two-location world with one character and one decision type.</summary>
    public sealed class TestWorld
    {
        public SimulationHost Host;
        public InMemorySaveGameStore Store;
        public FixedRealWorldClock Clock;
        public DefinitionCatalog Catalog;
        public LocationId Town;
        public LocationId Home;
        public LocationId Bakery;
        public CharacterId Mina;

        public static readonly AuthoredId TraitAmbitious = new AuthoredId("trait.ambitious");
        public static readonly AuthoredId NeedHunger = new AuthoredId("need.hunger");
        public static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        public static readonly AuthoredId DecisionJobOffer = new AuthoredId("decision.job_offer");
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");
        public static readonly AuthoredId InterventionStepUp = new AuthoredId("intervention.encourage");
        public static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");
        public static readonly AuthoredId KindBuilding = new AuthoredId("location_kind.building");

        public static DefinitionCatalog BuildCatalog(int contentVersion = 1)
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };

            builder.Add(new TraitDefinition(
                TraitAmbitious,
                "Ambitious",
                new[]
                {
                    new DiscoveryChannel(DiscoveryChannels.Inspection),
                    new DiscoveryChannel(DiscoveryChannels.DirectObservation),
                }));

            builder.Add(new NeedDefinition(NeedHunger, "Hunger", 0, 10000, 12, 1, new long[] { 8000 }));
            builder.Add(new ActivityDefinition(ActivityWorking, "Working", SimDuration.FromHours(6), true, true));
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new LocationKindDefinition(KindBuilding, "Building"));

            builder.Add(new DecisionDefinition(
                DecisionJobOffer,
                new[]
                {
                    new DecisionOption(OptionAccept, "Take the job", 0),
                    new DecisionOption(OptionStay, "Stay", 1),
                },
                SimDuration.FromHours(8),
                new AuthoredId("conflict_scope.employment"),
                importance: 10));

            builder.Add(new InterventionDefinition(InterventionStepUp, InterventionKind.StepDieUp, 1));

            return builder.Build();
        }

        public static TestWorld Create(long seed = 827119, int contentVersion = 1)
        {
            var fixture = new TestWorld
            {
                Store = new InMemorySaveGameStore(),
                Clock = new FixedRealWorldClock(1000000000000L),
                Catalog = BuildCatalog(contentVersion),
            };

            fixture.Host = SimulationBootstrapper.CreateNewWorld(
                seed,
                SimTime.FromClockTime(0, 8, 0),
                fixture.Catalog,
                simulationRulesVersion: 1,
                trace: null,
                saveStore: fixture.Store,
                realWorldClock: fixture.Clock);

            WorldState world = fixture.Host.World;

            fixture.Town = AddLocation(world, LocationId.None, "Town");
            fixture.Home = AddLocation(world, fixture.Town, "Home");
            fixture.Bakery = AddLocation(world, fixture.Town, "Bakery");
            world.TravelNetwork.ConnectBidirectional(fixture.Home, fixture.Bakery, SimDuration.FromMinutes(12), Walking);

            var mina = new Character(world.RuntimeIds.Characters.Next(), "Mina Cairn", world.Clock.Now);
            mina.AddTrait(TraitAmbitious);
            world.Characters.Add(mina.Id, mina);
            fixture.Mina = mina.Id;

            NeedDefinition hunger = fixture.Catalog.Needs[NeedHunger];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(2000, world.Clock.Now, hunger.DefaultRateNumerator, hunger.DefaultRateDenominator, hunger.MinValue, hunger.MaxValue),
                hunger.BehaviouralThresholds[0]);

            mina.SetNeed(hungerState);
            fixture.Host.Needs.Rearm(fixture.Host.Simulation, mina, hungerState);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, mina.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));

            return fixture;
        }

        /// <summary>Creates a job-offer decision with a mix of visible, generalized, and hidden influences.</summary>
        public Decision CreateDecision()
        {
            WorldState world = Host.World;
            DecisionDefinition definition = Catalog.Decisions[DecisionJobOffer];

            var decision = new Decision(
                world.RuntimeIds.Decisions.Next(),
                Mina,
                definition.Id,
                world.Clock.Now,
                world.Clock.Now.Plus(definition.TimeToResolve),
                definition.Options,
                new DecisionConflictScope(definition.ConflictScopeKind, Mina.ToRef()),
                definition.Importance);

            decision.AddInfluence(OptionAccept, new AuthoredId("cat.personal"), TraitAmbitious, Die.D10, InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude, default, Mina.ToRef());
            decision.AddInfluence(OptionAccept, new AuthoredId("cat.practical"), new AuthoredId("influence.better_pay"), Die.D6, InfluenceVisibility.Full);
            decision.AddInfluence(OptionStay, new AuthoredId("cat.social"), new AuthoredId("influence.family"), Die.D8, InfluenceVisibility.Full);
            decision.AddInfluence(OptionStay, new AuthoredId("cat.practical"), new AuthoredId("influence.commute"), Die.D6, InfluenceVisibility.Hidden);

            world.Decisions.Add(decision.Id, decision);
            world.DecisionDependencies.Register(decision);

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                decision.ResolveAt,
                SchedulePhase.Decision,
                ScheduledEventTypes.DecisionResolve,
                new DecisionResolvePayload(decision.Id, Mina));

            decision.SetPendingResolveEvent(scheduled.Id);
            return decision;
        }

        private static LocationId AddLocation(WorldState world, LocationId parent, string name)
        {
            var node = new LocationNode(world.RuntimeIds.Locations.Next(), parent, KindBuilding, name);
            world.Locations.Add(node);
            return node.Id;
        }
    }
}
