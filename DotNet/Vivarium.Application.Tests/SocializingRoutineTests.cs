using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class SocializingRoutineTests
    {
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");

        [Fact]
        public void ActiveSocialPressureWaitsForSharedContextThenSocializesWithoutDisplacingCounterpart()
        {
            Fixture fixture = Create(locationAffordsSocializing: true);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Actor).DefinitionId);

            AddCounterpart(fixture);
            ActivityInstanceId counterpartActivity = fixture.CounterpartActivity;
            fixture.Host.Session.Advance(SimDuration.Zero);

            ActivityInstance socializing = Current(fixture.Host, fixture.Actor);
            Assert.Equal(WellKnownActivities.Socializing, socializing.DefinitionId);
            Assert.Equal(fixture.Counterpart, new CharacterId((int)socializing.CommittedParameterOr(
                SocializingRoutineService.TargetCharacterParameter,
                0)));
            Assert.Equal(counterpartActivity, Current(fixture.Host, fixture.Counterpart).Id);
            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(
                fixture.Actor,
                fixture.Counterpart,
                out RelationshipId relationshipId));
            Assert.Equal(250, fixture.Host.World.Relationships.Get(relationshipId)
                .From(fixture.Actor).FamiliarityAt(fixture.Host.World.Clock.Now));
        }

        [Fact]
        public void SocializingCompletionAndNeedSatisfactionMatchOfflineAfterReload()
        {
            Fixture fixture = Create(locationAffordsSocializing: true);
            AddCounterpart(fixture);
            fixture.Host.Session.Advance(SimDuration.Zero);

            SaveGameData save = fixture.Host.Session.Save("socializing-in-progress");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                BuildCatalog(),
                save.LastCommandSequence);
            ActivityInstance restoredSocializing = Current(restored, fixture.Actor);
            Assert.Equal(WellKnownActivities.Socializing, restoredSocializing.DefinitionId);
            Assert.Equal(fixture.Counterpart.Value, restoredSocializing.CommittedParameterOr(
                SocializingRoutineService.TargetCharacterParameter,
                0));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(30), SimulationMode.OfflineCatchUp);
            restored.Session.Advance(SimDuration.FromMinutes(30), SimulationMode.OfflineCatchUp);

            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Actor).DefinitionId);
            Assert.Equal(Current(fixture.Host, fixture.Actor).DefinitionId, Current(restored, fixture.Actor).DefinitionId);
            Assert.Equal(NeedValue(fixture.Host, fixture.Actor), NeedValue(restored, fixture.Actor));
            Assert.True(NeedValue(fixture.Host, fixture.Actor) < 7000);
            Assert.Equal(fixture.Host.World.Clock.Now, restored.World.Clock.Now);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        [Fact]
        public void SharedOccupancyWithoutSocializingAffordanceDoesNotStartTheRoutine()
        {
            Fixture fixture = Create(locationAffordsSocializing: false);
            AddCounterpart(fixture);

            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Actor).DefinitionId);
            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Counterpart).DefinitionId);
        }

        private static Fixture Create(bool locationAffordsSocializing)
        {
            DefinitionCatalog catalog = BuildCatalog();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                1471,
                SimTime.FromClockTime(0, 18, 0),
                catalog,
                saveStore: new InMemorySaveGameStore());
            WorldState world = host.World;
            var commons = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Commons",
                activityAffordances: locationAffordsSocializing
                    ? new[] { WellKnownActivities.Socializing }
                    : new AuthoredId[0]);
            world.Locations.Add(commons);

            var actor = new Character(world.RuntimeIds.Characters.Next(), "Lena", world.Clock.Now);
            world.Characters.Add(actor.Id, actor);
            NeedDefinition social = catalog.Needs[WellKnownNeeds.Social];
            var need = new NeedState(
                social.Id,
                AnalyticalProgression.Linear(7000, world.Clock.Now, 4, 1, 0, 10000),
                social.SocializingRoutine.ActivationThreshold);
            actor.SetNeed(need);
            host.Needs.Rearm(host.Simulation, actor, need);
            host.Transitions.BeginActivity(
                host.Simulation,
                actor.Id,
                WellKnownActivities.Waiting,
                commons.Id,
                SimDuration.FromHours(1));
            return new Fixture(host, actor.Id, commons.Id);
        }

        private static CharacterId AddCounterpart(Fixture fixture)
        {
            WorldState world = fixture.Host.World;
            var counterpart = new Character(world.RuntimeIds.Characters.Next(), "Glen", world.Clock.Now);
            world.Characters.Add(counterpart.Id, counterpart);
            ActivityInstance activity = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                counterpart.Id,
                WellKnownActivities.Waiting,
                fixture.Location,
                SimDuration.FromHours(1));
            fixture.Counterpart = counterpart.Id;
            fixture.CounterpartActivity = activity.Id;
            return counterpart.Id;
        }

        private static DefinitionCatalog BuildCatalog()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Socializing, "Socializing", SimDuration.FromMinutes(30), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));
            builder.Add(new NeedDefinition(
                WellKnownNeeds.Social,
                "Social",
                0,
                10000,
                4,
                1,
                new long[] { 7000 },
                socializingRoutine: new SocializingRoutineDefinition(
                    WellKnownActivities.Socializing,
                    7000,
                    -5000,
                    4)));
            return builder.Build();
        }

        private static ActivityInstance Current(SimulationHost host, CharacterId character) =>
            host.World.Activities.Get(host.World.Characters.Get(character).CurrentActivityId);

        private static long NeedValue(SimulationHost host, CharacterId character)
        {
            Assert.True(host.World.Characters.Get(character).TryGetNeed(WellKnownNeeds.Social, out NeedState need));
            return need.ValueAt(host.World.Clock.Now);
        }

        private sealed class Fixture
        {
            public Fixture(SimulationHost host, CharacterId actor, LocationId location)
            {
                Host = host;
                Actor = actor;
                Location = location;
            }

            public SimulationHost Host { get; }
            public CharacterId Actor { get; }
            public CharacterId Counterpart { get; set; }
            public ActivityInstanceId CounterpartActivity { get; set; }
            public LocationId Location { get; }
        }
    }
}
