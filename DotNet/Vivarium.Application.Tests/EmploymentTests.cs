using System.Collections.Generic;
using System.Linq;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class EmploymentTests
    {
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");
        private static readonly AuthoredId Working = new AuthoredId("activity.working");
        private static readonly AuthoredId Definition = new AuthoredId("employment.bakery_worker");
        private static readonly AuthoredId Role = new AuthoredId("employment.role.baker");
        private static readonly AuthoredId Shift = new AuthoredId("employment.pattern.regular_shift");
        private static readonly AuthoredId Closing = new AuthoredId("employment.pattern.closing_duty");

        [Fact]
        public void EmploymentMaterializesOrdinaryAuthorityBackedCommitmentsAndFacts()
        {
            Fixture fixture = Create();

            IReadOnlyList<Commitment> created = fixture.Host.Employments.MaterializeCommitments(
                fixture.Host.Simulation,
                fixture.Employment,
                SimDuration.FromHours(4));

            Assert.Equal(2, created.Count);
            Commitment shift = Assert.Single(created, c => c.SourceTemplateId == Shift);
            Commitment closing = Assert.Single(created, c => c.SourceTemplateId == Closing);
            Assert.Equal(fixture.Employment.Id.ToRef(), shift.Source);
            Assert.Equal(fixture.Bakery, shift.LocationId);
            Assert.Equal(fixture.Bakery, closing.LocationId);
            Assert.Equal(StakeholderRole.Authority, Assert.Single(closing.Stakeholders).Role);
            Assert.Equal(fixture.Supervisor.ToRef(), closing.Stakeholders[0].Entity);
            Assert.True(fixture.Host.World.Memberships.IsMember(fixture.Employer, fixture.Employee));
            Assert.True(fixture.Host.World.Memberships.IsMember(fixture.Employer, fixture.Supervisor));

            Assert.True(fixture.Host.Session.Execute(new InspectCharacterCommand(fixture.Employee)).IsSuccess);
            var facts = fixture.Host.World.Knowledge.About(fixture.Employee.ToRef()).ToArray();
            Assert.Contains(facts, entry =>
                entry.Key.Kind == FactKinds.EmploymentEmployer &&
                entry.ObservedValue.Magnitude == fixture.Employer.Value);
            Assert.Contains(facts, entry =>
                entry.Key.Kind == FactKinds.EmploymentRole &&
                entry.ObservedValue.Band == Role);
            Assert.Contains(facts, entry =>
                entry.Key.Kind == FactKinds.EmploymentSupervisor &&
                entry.ObservedValue.Magnitude == fixture.Supervisor.Value);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(90));
            Assert.Equal(CommitmentStatus.Fulfilled, shift.Status);
            Assert.Contains(fixture.Host.World.CommitmentOutcomes.All, outcome =>
                outcome.CommitmentId == shift.Id && outcome.Outcome == CommitmentOutcomeKind.Fulfilled);
        }

        [Fact]
        public void EmploymentAndFutureAttendanceContinueIdenticallyAfterSaveLoad()
        {
            Fixture fixture = Create();
            fixture.Host.Employments.MaterializeCommitments(
                fixture.Host.Simulation,
                fixture.Employment,
                SimDuration.FromHours(4));
            fixture.Host.Session.Advance(SimDuration.FromMinutes(30));
            SaveGameData save = fixture.Host.Session.Save("employment-before-shift");

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                save.LastCommandSequence);

            Employment copy = restored.World.Employments.Get(fixture.Employment.Id);
            Assert.Equal(fixture.Employment.EmployeeId, copy.EmployeeId);
            Assert.Equal(fixture.Employment.EmployerGroupId, copy.EmployerGroupId);
            Assert.Equal(fixture.Employment.SupervisorId, copy.SupervisorId);
            Assert.Equal(2, copy.ObligationPatterns.Count);
            Assert.Contains(copy.Id, restored.World.EmploymentIndex.OfEmployee(fixture.Employee));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(60));
            restored.Session.Advance(SimDuration.FromMinutes(60));

            Commitment expectedShift = fixture.Host.World.Commitments.All.Single(c => c.SourceTemplateId == Shift);
            Commitment actualShift = restored.World.Commitments.All.Single(c => c.SourceTemplateId == Shift);
            Assert.Equal(CommitmentStatus.Fulfilled, expectedShift.Status);
            Assert.Equal(expectedShift.Status, actualShift.Status);
            Assert.Equal(expectedShift.FulfillingActivityId, actualShift.FulfillingActivityId);
            Assert.Equal(fixture.Host.World.Clock.Now, restored.World.Clock.Now);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        private static Fixture Create()
        {
            DefinitionCatalog catalog = BuildCatalog();
            var store = new InMemorySaveGameStore();
            var clock = new FixedRealWorldClock(1000000000000L);
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                9917,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                saveStore: store,
                realWorldClock: clock);
            WorldState world = host.World;
            var bakery = new LocationNode(world.RuntimeIds.Locations.Next(), LocationId.None, Building, "Bakery");
            world.Locations.Add(bakery);
            var employee = new Character(world.RuntimeIds.Characters.Next(), "Mina", world.Clock.Now);
            var supervisor = new Character(world.RuntimeIds.Characters.Next(), "Darius", world.Clock.Now);
            world.Characters.Add(employee.Id, employee);
            world.Characters.Add(supervisor.Id, supervisor);
            host.Transitions.BeginActivity(host.Simulation, employee.Id, WellKnownActivities.Waiting, bakery.Id, SimDuration.FromDays(1));
            host.Transitions.BeginActivity(host.Simulation, supervisor.Id, WellKnownActivities.Waiting, bakery.Id, SimDuration.FromDays(1));

            var employer = new Group(world.RuntimeIds.Groups.Next(), GroupKinds.Employer, "East Market Bakery", bakery.Id);
            world.Groups.Add(employer.Id, employer);
            Employment employment = host.Employments.Create(
                host.Simulation,
                employee.Id,
                employer.Id,
                Definition,
                supervisor.Id);
            host.Session.Advance(SimDuration.Zero);
            return new Fixture(host, catalog, bakery.Id, employer.Id, employee.Id, supervisor.Id, employment);
        }

        private static DefinitionCatalog BuildCatalog()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(Working, "Working", SimDuration.FromMinutes(30), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));
            builder.Add(new EmploymentDefinition(
                Definition,
                Role,
                new[]
                {
                    new EmploymentObligationPattern(
                        Shift,
                        new AuthoredId("commitment.work_shift"),
                        1,
                        1,
                        8 * 60,
                        SimDuration.FromMinutes(30),
                        100,
                        Working,
                        SimDuration.FromMinutes(5)),
                    new EmploymentObligationPattern(
                        Closing,
                        new AuthoredId("commitment.closing_duty"),
                        1,
                        1,
                        10 * 60,
                        SimDuration.FromMinutes(45),
                        120,
                        Working,
                        SimDuration.FromMinutes(5)),
                }));
            return builder.Build();
        }

        private sealed class Fixture
        {
            public Fixture(
                SimulationHost host,
                DefinitionCatalog catalog,
                LocationId bakery,
                GroupId employer,
                CharacterId employee,
                CharacterId supervisor,
                Employment employment)
            {
                Host = host; Catalog = catalog; Bakery = bakery; Employer = employer;
                Employee = employee; Supervisor = supervisor; Employment = employment;
            }

            public SimulationHost Host { get; }
            public DefinitionCatalog Catalog { get; }
            public LocationId Bakery { get; }
            public GroupId Employer { get; }
            public CharacterId Employee { get; }
            public CharacterId Supervisor { get; }
            public Employment Employment { get; }
        }
    }
}
