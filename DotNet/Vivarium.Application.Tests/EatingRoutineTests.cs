using System.Linq;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class EatingRoutineTests
    {
        private static readonly AuthoredId Hunger = new AuthoredId("need.hunger");
        private static readonly AuthoredId Working = new AuthoredId("activity.working");
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");
        private static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");
        private static readonly AuthoredId LeaveWork = new AuthoredId("decision.leave_work");

        [Fact]
        public void OrdinaryHungerStartsEatingOnlyAtAnAffordingLocationAndReturnsToFallback()
        {
            Fixture fixture = Create(startAtAffordingLocation: true, includeWorkDecision: true);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            ActivityInstance eating = Current(fixture.Host, fixture.Character);
            Assert.Equal(WellKnownActivities.Eating, eating.DefinitionId);
            Assert.Equal(fixture.Home, eating.SpatialContext.LocationId);
            Assert.Empty(fixture.Host.World.Decisions.All);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(30));

            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Character).DefinitionId);
            Assert.True(fixture.Host.World.Characters.Get(fixture.Character).TryGetNeed(Hunger, out NeedState hunger));
            Assert.Equal(1030, hunger.ValueAt(fixture.Host.World.Clock.Now));
        }

        [Fact]
        public void TravelMealConsumptionAndAffordancesContinueIdenticallyAcrossReloadAndOfflineCatchUp()
        {
            Fixture fixture = Create(startAtAffordingLocation: false, includeWorkDecision: false);
            fixture.Host.Session.Advance(SimDuration.FromMinutes(15));
            Assert.Equal(WellKnownActivities.Traveling, Current(fixture.Host, fixture.Character).DefinitionId);

            SaveGameData save = fixture.Host.Session.Save("eating-during-travel");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                BuildCatalog(-4500),
                save.LastCommandSequence);

            Assert.True(restored.World.Locations.Get(fixture.Home).Affords(WellKnownActivities.Eating));
            Assert.False(restored.World.Locations.Get(fixture.Bakery).Affords(WellKnownActivities.Eating));

            SimDuration remaining = SimDuration.FromMinutes(37);
            fixture.Host.Session.Advance(remaining, SimulationMode.OfflineCatchUp);
            restored.Session.Advance(remaining, SimulationMode.OfflineCatchUp);

            ActivityInstance expected = Current(fixture.Host, fixture.Character);
            ActivityInstance actual = Current(restored, fixture.Character);
            Assert.Equal(WellKnownActivities.Waiting, expected.DefinitionId);
            Assert.Equal(expected.DefinitionId, actual.DefinitionId);
            Assert.Equal(fixture.Home, actual.SpatialContext.LocationId);
            Assert.Equal(NeedValue(fixture.Host, fixture.Character), NeedValue(restored, fixture.Character));
            Assert.Equal(1042, NeedValue(restored, fixture.Character));
            Assert.Equal(fixture.Host.World.Clock.Now, restored.World.Clock.Now);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        [Fact]
        public void WorkingHungerProducesTheWorkDecisionInsteadOfInterruptingForFood()
        {
            Fixture fixture = Create(startAtAffordingLocation: false, includeWorkDecision: true, beginWorking: true);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(Working, Current(fixture.Host, fixture.Character).DefinitionId);
            Assert.Single(fixture.Host.World.Decisions.All, decision => decision.DefinitionId == LeaveWork);
            Assert.DoesNotContain(fixture.Host.World.Activities.All,
                activity => activity.CharacterId == fixture.Character &&
                    activity.DefinitionId == WellKnownActivities.Eating);
        }

        private static Fixture Create(
            bool startAtAffordingLocation,
            bool includeWorkDecision,
            bool beginWorking = false)
        {
            DefinitionCatalog catalog = BuildCatalog(-5000, includeWorkDecision);
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                4491,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                saveStore: new InMemorySaveGameStore());
            WorldState world = host.World;

            var home = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Home",
                activityAffordances: new[] { WellKnownActivities.Eating });
            world.Locations.Add(home);
            var bakery = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Bakery");
            world.Locations.Add(bakery);
            world.TravelNetwork.ConnectBidirectional(home.Id, bakery.Id, SimDuration.FromMinutes(12), Walking);
            var cafe = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Distant cafe",
                activityAffordances: new[] { WellKnownActivities.Eating });
            world.Locations.Add(cafe);
            world.TravelNetwork.ConnectBidirectional(bakery.Id, cafe.Id, SimDuration.FromMinutes(20), Walking);

            var character = new Character(world.RuntimeIds.Characters.Next(), "Priya", world.Clock.Now);
            world.Characters.Add(character.Id, character);
            NeedDefinition definition = catalog.Needs[Hunger];
            var hunger = new NeedState(
                Hunger,
                AnalyticalProgression.Linear(5990, world.Clock.Now, 1, 1, 0, 10000),
                definition.SatisfactionRoutine.ActivationThreshold);
            character.SetNeed(hunger);
            host.Needs.Rearm(host.Simulation, character, hunger);

            LocationId start = startAtAffordingLocation ? home.Id : bakery.Id;
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                beginWorking ? Working : WellKnownActivities.Waiting,
                start,
                beginWorking ? SimDuration.FromHours(2) : SimDuration.FromHours(1));
            host.Session.Advance(SimDuration.Zero);
            return new Fixture(host, character.Id, home.Id, bakery.Id);
        }

        private static DefinitionCatalog BuildCatalog(long satisfactionOffset, bool includeWorkDecision = false)
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(WellKnownActivities.Eating, "Eating", SimDuration.FromMinutes(30), false));
            builder.Add(new ActivityDefinition(Working, "Working", SimDuration.FromHours(2), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));
            builder.Add(new NeedDefinition(
                Hunger,
                "Hunger",
                0,
                10000,
                1,
                1,
                new long[] { 6000 },
                satisfactionRoutine: new NeedSatisfactionRoutineDefinition(
                    WellKnownActivities.Eating,
                    6000,
                    satisfactionOffset)));
            if (includeWorkDecision)
            {
                builder.Add(new DecisionDefinition(
                    LeaveWork,
                    new[]
                    {
                        new DecisionOption(new AuthoredId("option.leave"), "Leave", 0),
                        new DecisionOption(new AuthoredId("option.stay"), "Stay", 1),
                    },
                    SimDuration.FromMinutes(10),
                    trigger: new NeedThresholdDecisionTrigger(Hunger, 6000, Working)));
            }
            return builder.Build();
        }

        private static ActivityInstance Current(SimulationHost host, CharacterId characterId) =>
            host.World.Activities.Get(host.World.Characters.Get(characterId).CurrentActivityId);

        private static long NeedValue(SimulationHost host, CharacterId characterId)
        {
            Assert.True(host.World.Characters.Get(characterId).TryGetNeed(Hunger, out NeedState need));
            return need.ValueAt(host.World.Clock.Now);
        }

        private sealed class Fixture
        {
            public Fixture(SimulationHost host, CharacterId character, LocationId home, LocationId bakery)
            {
                Host = host;
                Character = character;
                Home = home;
                Bakery = bakery;
            }

            public SimulationHost Host { get; }
            public CharacterId Character { get; }
            public LocationId Home { get; }
            public LocationId Bakery { get; }
        }
    }
}
