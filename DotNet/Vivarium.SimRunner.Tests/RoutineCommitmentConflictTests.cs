using System.Linq;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.SimRunner.Tests
{
    public sealed class RoutineCommitmentConflictTests
    {
        [Fact]
        public void TwoEmploymentRoutinesNaturallyGenerateAndResolveATravelConflictAcrossReload()
        {
            Fixture fixture = Create();

            fixture.Host.Session.Advance(SimDuration.Zero);

            Decision decision = Assert.Single(fixture.Host.World.Decisions.All,
                item => item.IsActive && item.DefinitionId == SampleContent.DecisionCommitmentConflict);
            Assert.Equal(fixture.Character, decision.CharacterId);
            Assert.Equal(2, decision.CommitmentConflictKey.ParticipatingCommitmentIds.Count);
            Assert.DoesNotContain(fixture.Host.World.Scheduler.PendingEvents,
                item => item.EventType == ScheduledEventTypes.CommitmentBecomesKnown);

            Commitment[] participants = decision.CommitmentConflictKey.ParticipatingCommitmentIds
                .Select(fixture.Host.World.Commitments.Get)
                .ToArray();
            Assert.All(participants, commitment =>
            {
                Assert.Equal(EntityKind.Employment, commitment.Source.Kind);
                Assert.Equal(CommitmentStatus.Planned, commitment.Status);
            });
            Assert.Contains(participants, item => item.Source == fixture.BakeryEmployment.ToRef());
            Assert.Contains(participants, item => item.Source == fixture.CafeEmployment.ToRef());

            var feasibility = new CommitmentFeasibilityService();
            Assert.All(participants, commitment => Assert.True(feasibility.Evaluate(
                fixture.Host.World,
                fixture.Character,
                new[] { commitment }).IsJointlyFeasible));
            Assert.False(feasibility.Evaluate(
                fixture.Host.World,
                fixture.Character,
                participants).IsJointlyFeasible);

            SaveGameData save = fixture.Host.Session.Save("routine-employment-conflict");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                save.LastCommandSequence,
                saveStore: fixture.Store);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);

            long untilDeadline = decision.LatestResolutionAt.TotalMinutes - fixture.Host.World.Clock.Now.TotalMinutes;
            SimDuration remaining = SimDuration.FromMinutes(untilDeadline);
            fixture.Host.Session.Advance(remaining, SimulationMode.OfflineCatchUp);
            restored.Session.Advance(remaining, SimulationMode.OfflineCatchUp);

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.Equal(decision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Equal(
                decision.Resolution.Rolls.Select(item => item.Rolled),
                restoredDecision.Resolution.Rolls.Select(item => item.Rolled));
            Assert.Equal(1, participants.Count(item => item.Status == CommitmentStatus.Planned));
            Assert.Equal(1, participants.Count(item => item.Status == CommitmentStatus.Relinquished));
            Commitment relinquished = Assert.Single(participants,
                item => item.Status == CommitmentStatus.Relinquished);
            CommitmentOutcome outcome = Assert.Single(fixture.Host.World.CommitmentOutcomes.All,
                item => item.CommitmentId == relinquished.Id);
            Assert.Equal(CommitmentOutcomeKind.Relinquished, outcome.Outcome);
            Assert.Equal(CommitmentOutcomeCauseKind.ConflictResolution, outcome.Cause.Kind);
            Assert.Equal(decision.Id, outcome.Cause.SourceDecisionId);
            foreach (Commitment expected in participants)
            {
                Commitment actual = restored.World.Commitments.Get(expected.Id);
                Assert.Equal(expected.Status, actual.Status);
            }
            Assert.Equal(fixture.Host.World.Clock.Now, restored.World.Clock.Now);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        private static Fixture Create()
        {
            DefinitionCatalog catalog = SampleContent.Build();
            var store = new InMemorySaveGameStore();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                72191,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                saveStore: store);
            WorldState world = host.World;
            LocationId home = AddLocation(world, "Home");
            LocationId bakery = AddLocation(world, "Bakery");
            LocationId cafe = AddLocation(world, "Cafe");
            world.TravelNetwork.ConnectBidirectional(
                home, bakery, SimDuration.FromMinutes(12), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(
                home, cafe, SimDuration.FromMinutes(5), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(
                bakery, cafe, SimDuration.FromMinutes(9), SampleContent.TravelModeWalking);

            var character = new Character(world.RuntimeIds.Characters.Next(), "Nora", world.Clock.Now);
            world.Characters.Add(character.Id, character);
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                home,
                SimDuration.FromDays(1));

            GroupId bakeryEmployer = AddEmployer(world, "Bakery", bakery);
            GroupId cafeEmployer = AddEmployer(world, "Cafe", cafe);
            Employment bakeryJob = host.Employments.Create(
                host.Simulation,
                character.Id,
                bakeryEmployer,
                SampleContent.EmploymentBakeryWorker,
                assignedPatternIds: new[] { SampleContent.TemplateBakeryShift });
            Employment cafeJob = host.Employments.Create(
                host.Simulation,
                character.Id,
                cafeEmployer,
                SampleContent.EmploymentCafeHost);

            host.Employments.MaterializeCommitments(
                host.Simulation, bakeryJob, SimDuration.FromDays(1));
            host.Employments.MaterializeCommitments(
                host.Simulation, cafeJob, SimDuration.FromDays(1));
            return new Fixture(host, catalog, store, character.Id, bakeryJob.Id, cafeJob.Id);
        }

        private static LocationId AddLocation(WorldState world, string name)
        {
            var location = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                SampleContent.LocationKindBuilding,
                name);
            world.Locations.Add(location);
            return location.Id;
        }

        private static GroupId AddEmployer(WorldState world, string name, LocationId location)
        {
            var group = new Group(world.RuntimeIds.Groups.Next(), GroupKinds.Employer, name, location);
            world.Groups.Add(group.Id, group);
            return group.Id;
        }

        private sealed class Fixture
        {
            public Fixture(
                SimulationHost host,
                DefinitionCatalog catalog,
                InMemorySaveGameStore store,
                CharacterId character,
                EmploymentId bakeryEmployment,
                EmploymentId cafeEmployment)
            {
                Host = host;
                Catalog = catalog;
                Store = store;
                Character = character;
                BakeryEmployment = bakeryEmployment;
                CafeEmployment = cafeEmployment;
            }

            public SimulationHost Host { get; }
            public DefinitionCatalog Catalog { get; }
            public InMemorySaveGameStore Store { get; }
            public CharacterId Character { get; }
            public EmploymentId BakeryEmployment { get; }
            public EmploymentId CafeEmployment { get; }
        }
    }
}
