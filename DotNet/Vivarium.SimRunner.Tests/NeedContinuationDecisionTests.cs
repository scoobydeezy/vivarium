using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.SimRunner.Tests
{
    public sealed class NeedContinuationDecisionTests
    {
        [Fact]
        public void OrdinaryFatigueStopsForRestWithoutAllocatingADecision()
        {
            // This interest is enough for Continue to outscore Rest, but no single reason clears
            // admission. The non-dramatic fallback must still be Rest.
            Fixture fixture = Create(initialEnergy: 2010, watchedThreshold: 2000, interest: 4500);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(1));

            Assert.Equal(WellKnownActivities.Sleeping, Current(fixture.Host, fixture.Character).DefinitionId);
            Assert.Empty(fixture.Host.World.Decisions.All);
            Assert.Equal(0, fixture.Host.World.RuntimeIds.Decisions.IssuedCount);
        }

        [Fact]
        public void ImportantContinuationPreservesExactActivityRearmsLowerAndMatchesReload()
        {
            Fixture fixture = Create(initialEnergy: 2010, watchedThreshold: 2000, interest: 8000);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(1));

            Decision decision = Assert.Single(fixture.Host.World.Decisions.All);
            Assert.Equal(SampleContent.DecisionRestOrContinue, decision.DefinitionId);
            Assert.Equal(fixture.Activity, Current(fixture.Host, fixture.Character).Id);
            Assert.True(decision.Importance >= fixture.Catalog.DecisionImportancePolicy.AdmissionFloor);
            Assert.True(decision.TryGetContextParameter(
                DecisionReasoningParameters.NextNeedThreshold,
                out DecisionParameterValue next));
            Assert.Equal(1000, next.Integer);

            SaveGameData save = fixture.Host.Session.Save("rest-or-continue");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                save.LastCommandSequence,
                saveStore: fixture.Store);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            restored.Session.Advance(SimDuration.FromMinutes(10));

            Decision expected = fixture.Host.World.Decisions.Get(decision.Id);
            Decision actual = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(expected.Resolution.ChosenOptionId, actual.Resolution.ChosenOptionId);
            Assert.Equal(SampleContent.OptionContinue, expected.Resolution.ChosenOptionId);
            Assert.Equal(fixture.Activity, Current(fixture.Host, fixture.Character).Id);
            Assert.Equal(fixture.Activity, Current(restored, fixture.Character).Id);
            Assert.Equal(1000, Energy(fixture.Host, fixture.Character).BehaviouralThreshold);
            Assert.Equal(1000, Energy(restored, fixture.Character).BehaviouralThreshold);
            Assert.Equal(1, fixture.Host.World.RuntimeIds.Decisions.IssuedCount);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        [Fact]
        public void NeedMutationReevaluatesActiveFatigueReasonAndFinalThresholdCannotThrash()
        {
            Fixture active = Create(initialEnergy: 2010, watchedThreshold: 2000, interest: 8000);
            active.Host.Session.Advance(SimDuration.FromMinutes(1));
            Decision decision = Assert.Single(active.Host.World.Decisions.All);
            DecisionInfluence fatigue = Assert.Single(decision.Influences,
                influence => influence.ReasonBindingId == new AuthoredId("binding.rest_or_continue.fatigue"));
            int beforeRevision = Assert.Single(fatigue.Evaluation.Signals).SourceRevision;
            long beforeScore = fatigue.Evaluation.ExpectedScore;

            Character character = active.Host.World.Characters.Get(active.Character);
            active.Host.Needs.ApplyOffset(
                active.Host.Simulation,
                character,
                WellKnownNeeds.Energy,
                2000);
            active.Host.Session.Advance(SimDuration.Zero);

            DecisionInfluence reevaluated = Assert.Single(decision.Influences,
                influence => influence.ReasonBindingId == new AuthoredId("binding.rest_or_continue.fatigue"));
            Assert.True(Assert.Single(reevaluated.Evaluation.Signals).SourceRevision > beforeRevision);
            Assert.True(reevaluated.Evaluation.ExpectedScore < beforeScore);

            DecisionInfluence activityReason = Assert.Single(decision.Influences,
                influence => influence.ReasonBindingId ==
                    new AuthoredId("binding.rest_or_continue.current_activity"));
            DecisionInfluenceId activityReasonId = activityReason.Id;
            Assert.True(activityReason.Evaluation.ExpectedScore > 0);
            active.Host.Transitions.BeginActivity(
                active.Host.Simulation,
                active.Character,
                WellKnownActivities.Waiting,
                Current(active.Host, active.Character).SpatialContext.LocationId,
                SimDuration.FromHours(1));
            active.Host.Session.Advance(SimDuration.Zero);
            DecisionInfluence changedActivityReason = Assert.Single(decision.Influences,
                influence => influence.ReasonBindingId ==
                    new AuthoredId("binding.rest_or_continue.current_activity"));
            Assert.Equal(activityReasonId, changedActivityReason.Id);
            Assert.True(changedActivityReason.Evaluation.ExpectedScore < 0);

            Fixture exhausted = Create(initialEnergy: 10, watchedThreshold: 0, interest: 10000);
            exhausted.Host.Session.Advance(SimDuration.FromMinutes(1));
            Assert.Equal(WellKnownActivities.Sleeping, Current(exhausted.Host, exhausted.Character).DefinitionId);
            Assert.Empty(exhausted.Host.World.Decisions.All);
            Assert.Equal(0, exhausted.Host.World.RuntimeIds.Decisions.IssuedCount);
        }

        private static Fixture Create(long initialEnergy, long watchedThreshold, long interest)
        {
            DefinitionCatalog catalog = SampleContent.Build();
            var store = new InMemorySaveGameStore();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                1,
                SimTime.FromClockTime(0, 20, 0),
                catalog,
                saveStore: store);
            WorldState world = host.World;
            var home = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                SampleContent.LocationKindBuilding,
                "Home",
                activityAffordances: new[] { SampleContent.ActivityTabletopGames });
            world.Locations.Add(home);

            var character = new Character(world.RuntimeIds.Characters.Next(), "Mara", world.Clock.Now);
            character.Interests.Set(SampleContent.InterestTabletopGames, interest);
            world.Characters.Add(character.Id, character);
            var household = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Household,
                "Mara household",
                home.Id);
            world.Groups.Add(household.Id, household);
            world.Memberships.Join(household.Id, character.Id);

            NeedDefinition energy = catalog.Needs[WellKnownNeeds.Energy];
            var need = new NeedState(
                energy.Id,
                AnalyticalProgression.Linear(
                    initialEnergy,
                    world.Clock.Now,
                    energy.DefaultRateNumerator,
                    energy.DefaultRateDenominator,
                    energy.MinValue,
                    energy.MaxValue),
                watchedThreshold);
            character.SetNeed(need);
            host.Needs.Rearm(host.Simulation, character, need);
            ActivityInstance activity = host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                SampleContent.ActivityTabletopGames,
                home.Id,
                SimDuration.FromDays(1));
            host.Session.Advance(SimDuration.Zero);
            return new Fixture(host, catalog, store, character.Id, activity.Id);
        }

        private static ActivityInstance Current(SimulationHost host, CharacterId character) =>
            host.World.Activities.Get(host.World.Characters.Get(character).CurrentActivityId);

        private static NeedState Energy(SimulationHost host, CharacterId character) =>
            host.World.Characters.Get(character).Needs[WellKnownNeeds.Energy];

        private sealed class Fixture
        {
            public Fixture(
                SimulationHost host,
                DefinitionCatalog catalog,
                InMemorySaveGameStore store,
                CharacterId character,
                ActivityInstanceId activity)
            {
                Host = host;
                Catalog = catalog;
                Store = store;
                Character = character;
                Activity = activity;
            }

            public SimulationHost Host { get; }
            public DefinitionCatalog Catalog { get; }
            public InMemorySaveGameStore Store { get; }
            public CharacterId Character { get; }
            public ActivityInstanceId Activity { get; }
        }
    }
}
