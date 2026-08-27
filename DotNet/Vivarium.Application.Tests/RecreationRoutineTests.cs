using Vivarium.Application.Persistence;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class RecreationRoutineTests
    {
        private static readonly AuthoredId Tabletop = new AuthoredId("activity.tabletop_games");
        private static readonly AuthoredId Reading = new AuthoredId("activity.reading");
        private static readonly AuthoredId TabletopInterest = new AuthoredId("interest.tabletop_games");
        private static readonly AuthoredId ReadingInterest = new AuthoredId("interest.reading");
        private static readonly AuthoredId RecreationDecision = new AuthoredId("decision.choose_recreation");
        private static readonly AuthoredId TabletopOption = new AuthoredId("option.recreation.tabletop_games");
        private static readonly AuthoredId ReadingOption = new AuthoredId("option.recreation.reading");
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");
        private static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");

        [Fact]
        public void PreflightDoesNotAllocateIdentityScheduleEventsOrPublishDomainEvents()
        {
            Fixture fixture = Create(tabletopInterest: 8000, readingInterest: 3000);
            WorldState world = fixture.Host.World;
            DecisionDefinition definition = fixture.Host.Catalog.Decisions[RecreationDecision];
            var context = new DecisionReasoningContext(
                fixture.Character,
                definition.Options,
                definition.ReasoningProgram);
            RuntimeIdCounters beforeIds = world.RuntimeIds.Snapshot();
            int beforeScheduled = world.Scheduler.PendingCount;
            int beforeDomainEvents = world.DomainEvents.PendingCount;
            int beforeDecisions = world.Decisions.Count;

            DecisionReasoningPreflightResult result = new CompiledDecisionReasoningPreflightService().Evaluate(
                world,
                context,
                DecisionSignalProviderRegistry.WithBuiltIns());

            Assert.Equal(TabletopOption, result.SelectedOptionId);
            Assert.Equal(7059, result.Importance);
            AssertRuntimeIdsEqual(beforeIds, world.RuntimeIds.Snapshot());
            Assert.Equal(beforeScheduled, world.Scheduler.PendingCount);
            Assert.Equal(beforeDomainEvents, world.DomainEvents.PendingCount);
            Assert.Equal(beforeDecisions, world.Decisions.Count);
        }

        [Fact]
        public void OrdinaryPreferenceStartsAvailableRecreationWithoutSpendingDecisionIdentity()
        {
            Fixture fixture = Create(tabletopInterest: 4500, readingInterest: 2500);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            ActivityInstance current = Current(fixture.Host, fixture.Character);
            Assert.Equal(WellKnownActivities.Traveling, current.DefinitionId);
            Assert.Equal(fixture.Commons, current.SpatialContext.Transit.DestinationLocationId);
            Assert.Empty(fixture.Host.World.Decisions.All);
            Assert.Equal(0, fixture.Host.World.RuntimeIds.Decisions.IssuedCount);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(38));

            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Character).DefinitionId);
            Assert.True(NeedValue(fixture.Host, fixture.Character) < 6000);
        }

        [Fact]
        public void UnavailableStrongPreferenceFallsBackToTheRemainingRealAffordance()
        {
            Fixture fixture = Create(
                tabletopInterest: 9000,
                readingInterest: 3000,
                commonsAffordsTabletop: false);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            ActivityInstance current = Current(fixture.Host, fixture.Character);
            Assert.Equal(Reading, current.DefinitionId);
            Assert.Equal(fixture.Home, current.SpatialContext.LocationId);
            Assert.Empty(fixture.Host.World.Decisions.All);
        }

        [Fact]
        public void ClosingCommonsBeforePlanningSpendsOneNudgeAndFallsBackToHomeAffordance()
        {
            Fixture fixture = Create(tabletopInterest: 9000, readingInterest: 3000);

            Result result = fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: false));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, fixture.Host.World.Nudges.Balance);
            Assert.False(fixture.Host.World.Locations.Get(fixture.Commons).IsOpen);
            Assert.True(new LocationProjector().TryProject(
                fixture.Host.World, fixture.Commons, out LocationView locationView));
            Assert.False(locationView.IsOpen);
            Assert.True(locationView.CanManageAvailability);
            Assert.Equal(LocationAvailabilityRules.NudgeCost, locationView.AvailabilityNudgeCost);
            Assert.Equal(2, locationView.NudgeBalance);
            Assert.Contains(locationView.RecentHistory,
                entry => entry.Summary.Contains("was closed"));

            NotificationRecapView notification = new NotificationRecapProjector(
                fixture.Host.Catalog.DecisionImportancePolicy).Project(
                    fixture.Host.World, SimulationMode.Live, maximumGroups: 1);
            NotificationEntryView worldChange = Assert.Single(notification.Entries);
            Assert.Equal("World", worldChange.Category);
            Assert.Contains("was closed", worldChange.Message);

            for (int i = 0; i < 12; i++)
                fixture.Host.World.HistoryLedger.Record(
                    LocationAvailabilityHistoryHandler.HistoryKind,
                    fixture.Host.World.Clock.Now,
                    RetentionTier.Recent,
                    "Commons availability changed again.",
                    new[] { fixture.Commons.ToRef() });
            NotificationRecapView grouped = new NotificationRecapProjector(
                fixture.Host.Catalog.DecisionImportancePolicy).Project(
                    fixture.Host.World, SimulationMode.OfflineCatchUp, maximumGroups: 1);
            Assert.Equal(13, Assert.Single(grouped.Entries).OccurrenceCount);
            Assert.Equal(13, grouped.IncludedEventCount);
            Assert.Equal(0, grouped.OmittedGroupCount);

            fixture.Host.World.HistoryLedger.Record(
                new AuthoredId("history.interaction"),
                fixture.Host.World.Clock.Now,
                RetentionTier.Recent,
                "A private interaction.",
                new[] { fixture.Character.ToRef() });
            NotificationRecapProjector recap = new NotificationRecapProjector(
                fixture.Host.Catalog.DecisionImportancePolicy);
            Assert.DoesNotContain(
                recap.Project(fixture.Host.World, SimulationMode.OfflineCatchUp, maximumGroups: 20).Entries,
                entry => entry.Category == "Social");

            fixture.Host.World.Knowledge.Record(new KnowledgeEntry(
                new FactKey(new AuthoredId("fact.test.observed"), fixture.Character.ToRef()),
                ObservedValue.Of(new AuthoredId("value.known")),
                fixture.Host.World.Clock.Now,
                KnowledgeConfidence.Known,
                DiscoverySource.Channel(DiscoveryChannels.DirectObservation)));
            Assert.Contains(
                recap.Project(fixture.Host.World, SimulationMode.OfflineCatchUp, maximumGroups: 20).Entries,
                entry => entry.Category == "Social");

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            ActivityInstance current = Current(fixture.Host, fixture.Character);
            Assert.Equal(Reading, current.DefinitionId);
            Assert.Equal(fixture.Home, current.SpatialContext.LocationId);
        }

        [Fact]
        public void ClosingCommonsTargetsAnInFlightRoutineWithoutInterruptingAnActivityAlreadyThere()
        {
            Fixture fixture = Create(tabletopInterest: 4500, readingInterest: 2500);
            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            Assert.Equal(fixture.Commons, Current(fixture.Host, fixture.Character).SpatialContext.Transit.DestinationLocationId);
            Assert.Empty(new LocationProjector().TryProject(
                fixture.Host.World, fixture.Commons, out LocationView unwatchedView)
                ? unwatchedView.ObservedPresence
                : throw new Xunit.Sdk.XunitException("Commons did not project."));
            Assert.True(fixture.Host.Session.Execute(
                new FollowCharacterCommand(fixture.Character, true)).IsSuccess);
            Assert.True(new LocationProjector().TryProject(
                fixture.Host.World, fixture.Commons, out LocationView watchedView));
            Assert.Contains(watchedView.ObservedPresence,
                presence => presence.CharacterId == fixture.Character.Value &&
                    presence.StatusLabel.Contains("traveling"));

            Result result = fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: false));

            Assert.True(result.IsSuccess);
            ActivityInstance redirected = Current(fixture.Host, fixture.Character);
            Assert.False(redirected.SpatialContext.IsTraveling &&
                redirected.SpatialContext.Transit.DestinationLocationId == fixture.Commons);

            Fixture underwayFixture = Create(tabletopInterest: 4500, readingInterest: 2500);
            underwayFixture.Host.Transitions.BeginActivity(
                underwayFixture.Host.Simulation,
                underwayFixture.Character,
                Tabletop,
                underwayFixture.Commons,
                SimDuration.FromMinutes(30));
            ActivityInstance underway = Current(underwayFixture.Host, underwayFixture.Character);

            Assert.True(underwayFixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(underwayFixture.Commons, open: false)).IsSuccess);

            Assert.Equal(Tabletop, underway.DefinitionId);
            Assert.Same(underway, Current(underwayFixture.Host, underwayFixture.Character));
            Assert.Equal(underwayFixture.Commons, underway.SpatialContext.LocationId);
            Assert.Equal(ActivityStatus.Active, underway.Status);
        }

        [Fact]
        public void ClosingCommonsInvalidatesOnlyTheDependentLivingRecreationDecision()
        {
            Fixture fixture = Create(tabletopInterest: 8000, readingInterest: 3000);
            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            Decision original = Assert.Single(fixture.Host.World.Decisions.All);
            Assert.True(original.IsActive);

            Result result = fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: false));

            Assert.True(result.IsSuccess);
            Assert.Equal(DecisionStatus.Dissolved, original.Status);
            ActivityInstance current = Current(fixture.Host, fixture.Character);
            Assert.Equal(Reading, current.DefinitionId);
            Assert.Equal(fixture.Home, current.SpatialContext.LocationId);
            Assert.Contains(fixture.Host.World.HistoryLedger.Entries,
                entry => entry.Kind == LocationAvailabilityHistoryHandler.HistoryKind);
        }

        [Fact]
        public void NoOpAvailabilityCommandDoesNotSpendAndReopeningRestoresAffordancesAcrossSaveLoad()
        {
            Fixture fixture = Create(tabletopInterest: 4500, readingInterest: 2500);

            Result noOp = fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: true));
            Assert.True(noOp.IsFailure);
            Assert.Equal(LocationAvailabilityRules.ReasonAlreadySet, noOp.Reason);
            Assert.Equal(NudgePolicy.InitialBalance, fixture.Host.World.Nudges.Balance);

            Assert.True(fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: false)).IsSuccess);
            Assert.True(fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: true)).IsSuccess);

            SaveGameData save = fixture.Host.Session.Save("commons-reopened");
            WorldState restored = fixture.Host.SaveMapper.Restore(save);

            LocationNode commons = restored.Locations.Get(fixture.Commons);
            Assert.True(commons.IsOpen);
            Assert.True(commons.SupportsPlayerManagedAvailability);
            Assert.Equal(1, restored.Nudges.Balance);
        }

        [Fact]
        public void InsufficientNudgesLeavesCommonsAndItsRevisionUntouched()
        {
            Fixture fixture = Create(tabletopInterest: 4500, readingInterest: 2500);
            Assert.True(fixture.Host.World.Nudges.TrySpend(NudgePolicy.InitialBalance));
            int beforeRevision = fixture.Host.World.Revisions.Get(
                new RevisionKey(fixture.Commons.ToRef(), RevisionAspects.LocationAvailability));

            Result result = fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Commons, open: false));

            Assert.True(result.IsFailure);
            Assert.Equal(LocationAvailabilityRules.ReasonInsufficientNudges, result.Reason);
            Assert.True(fixture.Host.World.Locations.Get(fixture.Commons).IsOpen);
            Assert.Equal(beforeRevision, fixture.Host.World.Revisions.Get(
                new RevisionKey(fixture.Commons.ToRef(), RevisionAspects.LocationAvailability)));
        }

        [Fact]
        public void ImportantRecreationAdoptsExactPreflightReasonsAndContinuesAcrossReloadOffline()
        {
            Fixture fixture = Create(tabletopInterest: 8000, readingInterest: 3000);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Decision decision = Assert.Single(fixture.Host.World.Decisions.All);
            Assert.Equal(RecreationDecision, decision.DefinitionId);
            Assert.Equal(7059, decision.Importance);
            Assert.Equal(2, decision.Influences.Count);
            Assert.All(decision.Influences, influence =>
                Assert.Equal(2, Assert.Single(influence.Evaluation.Signals).SourceRevision));
            Assert.Equal(1, fixture.Host.World.RuntimeIds.Decisions.IssuedCount);

            SaveGameData save = fixture.Host.Session.Save("important-recreation");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                BuildCatalog(),
                save.LastCommandSequence);

            SimDuration remaining = SimDuration.FromMinutes(50);
            fixture.Host.Session.Advance(remaining, SimulationMode.OfflineCatchUp);
            restored.Session.Advance(remaining, SimulationMode.OfflineCatchUp);

            Decision expectedDecision = fixture.Host.World.Decisions.Get(decision.Id);
            Decision actualDecision = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(expectedDecision.Resolution.ChosenOptionId, actualDecision.Resolution.ChosenOptionId);
            Assert.Equal(expectedDecision.Importance, actualDecision.Importance);
            Assert.Equal(Current(fixture.Host, fixture.Character).DefinitionId, Current(restored, fixture.Character).DefinitionId);
            Assert.Equal(WellKnownActivities.Waiting, Current(fixture.Host, fixture.Character).DefinitionId);
            Assert.Equal(NeedValue(fixture.Host, fixture.Character), NeedValue(restored, fixture.Character));
            Assert.True(NeedValue(fixture.Host, fixture.Character) < 6000);
            Assert.Equal(fixture.Host.World.Clock.Now, restored.World.Clock.Now);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.World.Scheduler.PendingCount);
        }

        private static Fixture Create(
            long tabletopInterest,
            long readingInterest,
            bool commonsAffordsTabletop = true)
        {
            DefinitionCatalog catalog = BuildCatalog();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                8819,
                SimTime.FromClockTime(0, 18, 0),
                catalog,
                saveStore: new InMemorySaveGameStore());
            WorldState world = host.World;
            var home = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Home",
                activityAffordances: new[] { Reading });
            world.Locations.Add(home);
            var commons = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Commons",
                activityAffordances: commonsAffordsTabletop ? new[] { Tabletop, Reading } : new[] { Reading },
                supportsPlayerManagedAvailability: true);
            world.Locations.Add(commons);
            world.TravelNetwork.ConnectBidirectional(home.Id, commons.Id, SimDuration.FromMinutes(8), Walking);

            var character = new Character(world.RuntimeIds.Characters.Next(), "Owen", world.Clock.Now);
            character.Interests.Set(TabletopInterest, tabletopInterest);
            character.Interests.Set(ReadingInterest, readingInterest);
            world.Characters.Add(character.Id, character);
            NeedDefinition recreation = catalog.Needs[WellKnownNeeds.Recreation];
            var need = new NeedState(
                recreation.Id,
                AnalyticalProgression.Linear(5990, world.Clock.Now, 1, 1, 0, 10000),
                recreation.RecreationRoutine.ActivationThreshold);
            character.SetNeed(need);
            host.Needs.Rearm(host.Simulation, character, need);
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                home.Id,
                SimDuration.FromHours(1));
            host.Session.Advance(SimDuration.Zero);
            return new Fixture(host, character.Id, home.Id, commons.Id);
        }

        private static DefinitionCatalog BuildCatalog()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.SetDecisionImportancePolicy(new DecisionImportancePolicyDefinition(6500, 6500, 7000, 7500));
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(Tabletop, "Tabletop Games", SimDuration.FromMinutes(30), false));
            builder.Add(new ActivityDefinition(Reading, "Reading", SimDuration.FromMinutes(30), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));

            var tabletopOption = new DecisionOption(TabletopOption, "Play Tabletop Games", 0);
            tabletopOption.SetContext(
                DecisionReasoningParameters.InterestId,
                DecisionParameterValue.FromAuthoredId(TabletopInterest));
            var readingOption = new DecisionOption(ReadingOption, "Read", 1);
            readingOption.SetContext(
                DecisionReasoningParameters.InterestId,
                DecisionParameterValue.FromAuthoredId(ReadingInterest));
            builder.Add(new DecisionDefinition(
                RecreationDecision,
                new[] { tabletopOption, readingOption },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.recreation"),
                reasoningProgram: RecreationReasoningProgram()));
            builder.Add(new NeedDefinition(
                WellKnownNeeds.Recreation,
                "Recreation",
                0,
                10000,
                1,
                1,
                new long[] { 6000 },
                recreationRoutine: new RecreationRoutineDefinition(
                    RecreationDecision,
                    6000,
                    -5000,
                    new[]
                    {
                        new RecreationCandidateDefinition(TabletopOption, Tabletop, TabletopInterest),
                        new RecreationCandidateDefinition(ReadingOption, Reading, ReadingInterest),
                    })));
            return builder.Build();
        }

        private static DecisionReasoningProgram RecreationReasoningProgram()
        {
            AuthoredId signal = new AuthoredId("decision.signal.recreation.interest");
            return new DecisionReasoningProgram(new[]
            {
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.recreation.interest"),
                    new AuthoredId("consideration.recreation.interest"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.InterestId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(
                            DecisionReasoningParameters.InterestId,
                            ParameterBindingSource.OptionContext,
                            DecisionReasoningParameters.InterestId),
                    },
                    new[] { new DecisionSignalRequest(signal, DecisionSignalProviderIds.ActorInterest) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.recreation.interest"),
                        0,
                        new[] { new SignalLinearTerm(signal, 30000, new AuthoredId("reason.recreation.interest")) },
                        null,
                        null,
                        null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.recreation.interest")),
                    ReasonScaleProfile.Standard(),
                    new AuthoredId("cat.personal"),
                    new AuthoredId("influence.recreation.interest"),
                    new AuthoredId("influence.recreation.disinterest"),
                    InfluenceVisibility.Full),
            });
        }

        private static ActivityInstance Current(SimulationHost host, CharacterId character) =>
            host.World.Activities.Get(host.World.Characters.Get(character).CurrentActivityId);

        private static long NeedValue(SimulationHost host, CharacterId character)
        {
            Assert.True(host.World.Characters.Get(character).TryGetNeed(WellKnownNeeds.Recreation, out NeedState need));
            return need.ValueAt(host.World.Clock.Now);
        }

        private static void AssertRuntimeIdsEqual(RuntimeIdCounters expected, RuntimeIdCounters actual)
        {
            Assert.Equal(expected.Characters, actual.Characters);
            Assert.Equal(expected.Activities, actual.Activities);
            Assert.Equal(expected.Commitments, actual.Commitments);
            Assert.Equal(expected.CommitmentOutcomes, actual.CommitmentOutcomes);
            Assert.Equal(expected.Relationships, actual.Relationships);
            Assert.Equal(expected.Decisions, actual.Decisions);
            Assert.Equal(expected.Locations, actual.Locations);
            Assert.Equal(expected.Groups, actual.Groups);
            Assert.Equal(expected.Employments, actual.Employments);
            Assert.Equal(expected.ScheduledEvents, actual.ScheduledEvents);
            Assert.Equal(expected.HistoryEntries, actual.HistoryEntries);
            Assert.Equal(expected.EventSequence, actual.EventSequence);
        }

        private sealed class Fixture
        {
            public Fixture(SimulationHost host, CharacterId character, LocationId home, LocationId commons)
            {
                Host = host;
                Character = character;
                Home = home;
                Commons = commons;
            }

            public SimulationHost Host { get; }
            public CharacterId Character { get; }
            public LocationId Home { get; }
            public LocationId Commons { get; }
        }
    }
}
