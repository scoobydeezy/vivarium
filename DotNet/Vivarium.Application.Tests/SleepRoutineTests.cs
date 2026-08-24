using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class SleepRoutineTests
    {
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");
        private static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");

        [Fact]
        public void LowEnergyTravelsHomeSleepsRecoversWakesAndResumesFallbackPlanning()
        {
            Fixture fixture = Create();

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            Assert.Equal(WellKnownActivities.Traveling, Current(fixture.Host, fixture.Character).DefinitionId);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(12));
            ActivityInstance sleeping = Current(fixture.Host, fixture.Character);
            Assert.Equal(WellKnownActivities.Sleeping, sleeping.DefinitionId);
            Assert.Equal(fixture.Home, sleeping.SpatialContext.LocationId);

            NeedState recovering = fixture.Host.World.Characters.Get(fixture.Character).Needs[WellKnownNeeds.Energy];
            Assert.Equal(8000, recovering.BehaviouralThreshold);
            Assert.True(recovering.Progression.IsIncreasing);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(306));

            ActivityInstance awake = Current(fixture.Host, fixture.Character);
            NeedState energy = fixture.Host.World.Characters.Get(fixture.Character).Needs[WellKnownNeeds.Energy];
            Assert.Equal(WellKnownActivities.Waiting, awake.DefinitionId);
            Assert.Equal(fixture.Home, awake.SpatialContext.LocationId);
            Assert.Equal(8000, energy.ValueAt(fixture.Host.World.Clock.Now));
            Assert.Equal(2000, energy.BehaviouralThreshold);
            Assert.False(energy.Progression.IsIncreasing);
        }

        [Fact]
        public void TravelContinuationAndSleepingRoundTripToTheSameWakeState()
        {
            AssertContinuationMatchesAfterSave(saveAfterMinutes: 15, advanceAfterSaveMinutes: 313);
            AssertContinuationMatchesAfterSave(saveAfterMinutes: 60, advanceAfterSaveMinutes: 268);
            AssertContinuationMatchesAfterSave(saveAfterMinutes: 328, advanceAfterSaveMinutes: 1);
        }

        [Fact]
        public void OfflineCatchUpUsesTheSameSleepAndWakeRulesAsLiveSimulation()
        {
            Fixture live = Create();
            Fixture offline = Create();

            live.Host.Session.Advance(SimDuration.FromMinutes(328), SimulationMode.Live);
            offline.Host.Session.Advance(SimDuration.FromMinutes(328), SimulationMode.OfflineCatchUp);

            AssertEquivalent(live.Host, offline.Host, live.Character);
        }

        private static void AssertContinuationMatchesAfterSave(long saveAfterMinutes, long advanceAfterSaveMinutes)
        {
            Fixture fixture = Create();
            fixture.Host.Session.Advance(SimDuration.FromMinutes(saveAfterMinutes));
            SaveGameData save = fixture.Host.Session.Save("sleep-checkpoint");

            fixture.Host.Session.Advance(SimDuration.FromMinutes(advanceAfterSaveMinutes));

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                save.LastCommandSequence,
                saveStore: fixture.Store,
                realWorldClock: fixture.Clock);
            restored.Session.Advance(SimDuration.FromMinutes(advanceAfterSaveMinutes));

            AssertEquivalent(fixture.Host, restored, fixture.Character);
        }

        private static void AssertEquivalent(SimulationHost expected, SimulationHost actual, CharacterId characterId)
        {
            Assert.Equal(expected.World.Clock.Now, actual.World.Clock.Now);

            ActivityInstance expectedActivity = Current(expected, characterId);
            ActivityInstance actualActivity = Current(actual, characterId);
            Assert.Equal(expectedActivity.DefinitionId, actualActivity.DefinitionId);
            Assert.Equal(expectedActivity.SpatialContext.ToString(), actualActivity.SpatialContext.ToString());
            Assert.Equal(expectedActivity.StartedAt, actualActivity.StartedAt);

            NeedState expectedEnergy = expected.World.Characters.Get(characterId).Needs[WellKnownNeeds.Energy];
            NeedState actualEnergy = actual.World.Characters.Get(characterId).Needs[WellKnownNeeds.Energy];
            Assert.Equal(expectedEnergy.ValueAt(expected.World.Clock.Now), actualEnergy.ValueAt(actual.World.Clock.Now));
            Assert.Equal(expectedEnergy.BehaviouralThreshold, actualEnergy.BehaviouralThreshold);
            Assert.Equal(expectedEnergy.Progression.RatePerMinuteNumerator, actualEnergy.Progression.RatePerMinuteNumerator);
            Assert.Equal(expected.World.Scheduler.PendingCount, actual.World.Scheduler.PendingCount);
        }

        private static ActivityInstance Current(SimulationHost host, CharacterId characterId) =>
            host.World.Activities.Get(host.World.Characters.Get(characterId).CurrentActivityId);

        private static Fixture Create()
        {
            DefinitionCatalog catalog = BuildCatalog();
            var store = new InMemorySaveGameStore();
            var clock = new FixedRealWorldClock(1000000000000L);
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                1441,
                SimTime.FromClockTime(0, 20, 0),
                catalog,
                saveStore: store,
                realWorldClock: clock);

            WorldState world = host.World;
            LocationId town = AddLocation(world, LocationId.None, "Town");
            LocationId home = AddLocation(world, town, "Home");
            LocationId bakery = AddLocation(world, town, "Bakery");
            world.TravelNetwork.ConnectBidirectional(home, bakery, SimDuration.FromMinutes(12), Walking);

            var character = new Character(world.RuntimeIds.Characters.Next(), "Tess", world.Clock.Now);
            world.Characters.Add(character.Id, character);

            var household = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Household,
                "Tess household",
                home);
            world.Groups.Add(household.Id, household);
            world.Memberships.Join(household.Id, character.Id);

            NeedDefinition energy = catalog.Needs[WellKnownNeeds.Energy];
            var state = new NeedState(
                energy.Id,
                AnalyticalProgression.Linear(
                    2100,
                    world.Clock.Now,
                    energy.DefaultRateNumerator,
                    energy.DefaultRateDenominator,
                    energy.MinValue,
                    energy.MaxValue),
                energy.RestRoutine.ActivationThreshold);
            character.SetNeed(state);
            host.Needs.Rearm(host.Simulation, character, state);
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                bakery,
                SimDuration.FromDays(1));
            host.Session.Advance(SimDuration.Zero);

            return new Fixture(host, catalog, store, clock, home, character.Id);
        }

        private static DefinitionCatalog BuildCatalog()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(WellKnownActivities.Sleeping, "Sleeping", SimDuration.FromHours(8), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));
            builder.Add(new NeedDefinition(
                WellKnownNeeds.Energy,
                "Energy",
                0,
                10000,
                -10,
                1,
                new long[] { 2000, 8000 },
                new NeedRestRoutineDefinition(
                    WellKnownActivities.Sleeping,
                    GroupKinds.Household,
                    2000,
                    8000,
                    20)));
            return builder.Build();
        }

        private static LocationId AddLocation(WorldState world, LocationId parent, string name)
        {
            var location = new LocationNode(world.RuntimeIds.Locations.Next(), parent, Building, name);
            world.Locations.Add(location);
            return location.Id;
        }

        private sealed class Fixture
        {
            public Fixture(
                SimulationHost host,
                DefinitionCatalog catalog,
                InMemorySaveGameStore store,
                FixedRealWorldClock clock,
                LocationId home,
                CharacterId character)
            {
                Host = host;
                Catalog = catalog;
                Store = store;
                Clock = clock;
                Home = home;
                Character = character;
            }

            public SimulationHost Host { get; }
            public DefinitionCatalog Catalog { get; }
            public InMemorySaveGameStore Store { get; }
            public FixedRealWorldClock Clock { get; }
            public LocationId Home { get; }
            public CharacterId Character { get; }
        }
    }
}
