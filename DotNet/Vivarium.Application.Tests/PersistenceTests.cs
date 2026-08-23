using System.Collections.Generic;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    /// <summary>
    /// Persistence tests (§51): round-trip, scheduler reconstruction, active travel state, the offline
    /// anchor, version metadata, and index rebuilding.
    /// </summary>
    public sealed class PersistenceTests
    {
        [Fact]
        public void RoundTripPreservesAuthoritativeState()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(2));
            Decision decision = fixture.CreateDecision();
            fixture.Host.Session.Advance(SimDuration.Zero);

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.Equal(fixture.Host.World.Clock.Now, restored.Clock.Now);
            Assert.Equal(fixture.Host.World.WorldSeed, restored.WorldSeed);
            Assert.Equal(fixture.Host.World.Characters.Count, restored.Characters.Count);
            Assert.Equal(fixture.Host.World.Activities.Count, restored.Activities.Count);
            Assert.Equal(fixture.Host.World.Decisions.Count, restored.Decisions.Count);
            Assert.Equal(fixture.Host.World.Locations.Count, restored.Locations.Count);
            Assert.Equal(fixture.Host.World.Scheduler.PendingCount, restored.Scheduler.PendingCount);

            // Allocator counters must continue rather than restart, so ids are never reused (§7.1).
            Assert.Equal(
                fixture.Host.World.RuntimeIds.Snapshot().Decisions,
                restored.RuntimeIds.Snapshot().Decisions);

            Decision restoredDecision = restored.Decisions.Get(decision.Id);
            Assert.Equal(decision.Influences.Count, restoredDecision.Influences.Count);
            Assert.Equal(decision.InfluenceRevision, restoredDecision.InfluenceRevision);
        }

        [Fact]
        public void SchedulerRoundTripsWithOrderingAndDependenciesIntact()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(1));

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            var original = new List<string>();
            foreach (Domain.Scheduling.ScheduledEvent scheduled in fixture.Host.World.Scheduler.PendingEvents)
            {
                original.Add($"{scheduled.Id.Value}:{scheduled.DueAt.TotalMinutes}:{scheduled.Phase}:{scheduled.EventSequence}:{scheduled.EventType}:{scheduled.Dependencies.Count}");
            }

            var reloaded = new List<string>();
            foreach (Domain.Scheduling.ScheduledEvent scheduled in restored.Scheduler.PendingEvents)
            {
                reloaded.Add($"{scheduled.Id.Value}:{scheduled.DueAt.TotalMinutes}:{scheduled.Phase}:{scheduled.EventSequence}:{scheduled.EventType}:{scheduled.Dependencies.Count}");
            }

            Assert.Equal(original, reloaded);
            Assert.NotEmpty(reloaded);
        }

        [Fact]
        public void RevisionsAreRestoredSoQueuedEventsAreNotDiscardedAsStale()
        {
            // Without persisted revisions, every saved event would look stale on load and the world's
            // entire queued future would vanish silently (§11.2).
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(3));

            var activityKey = new RevisionKey(fixture.Mina.ToRef(), RevisionAspects.Activity);
            int expected = fixture.Host.World.Revisions.Get(activityKey);
            Assert.True(expected > 0);

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.Equal(expected, restored.Revisions.Get(activityKey));
        }

        [Fact]
        public void ActiveTravelRoundTripsExactly()
        {
            TestWorld fixture = TestWorld.Create();

            Assert.True(fixture.Host.Transitions.TryBeginTravel(
                fixture.Host.Simulation, fixture.Mina, fixture.Bakery, out ActivityInstance travel));

            TransitDetails original = travel.SpatialContext.Transit;

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            ActivityInstance restoredTravel = restored.Activities.Get(travel.Id);
            TransitDetails reloaded = restoredTravel.SpatialContext.Transit;

            Assert.True(restoredTravel.SpatialContext.IsTraveling);
            Assert.Equal(original.OriginLocationId, reloaded.OriginLocationId);
            Assert.Equal(original.DestinationLocationId, reloaded.DestinationLocationId);
            Assert.Equal(original.DepartedAt, reloaded.DepartedAt);
            Assert.Equal(original.ArrivesAt, reloaded.ArrivesAt);
            Assert.Equal(original.TravelModeId, reloaded.TravelModeId);

            // And the rebuilt indexes agree: still travelling, still not occupying either endpoint (§30).
            Assert.Contains(fixture.Mina, restored.Spatial.Travelers);
            Assert.Empty(restored.Spatial.DirectOccupantsOf(fixture.Home));
            Assert.Empty(restored.Spatial.DirectOccupantsOf(fixture.Bakery));
        }

        [Fact]
        public void DerivedIndexesAreRebuiltRatherThanPersisted()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(1));

            SaveGameData saved = fixture.Host.Session.Save("slot1");

            // Nothing in the save describes occupancy; it is derived from Activity spatial contexts (§40).
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.Contains(fixture.Mina, restored.Spatial.DirectOccupantsOf(fixture.Home));
            Assert.Equal(1, restored.Spatial.CountWithin(fixture.Town));
        }

        [Fact]
        public void InterventionsSurviveReloadStillBoundToTheirInfluence()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            DecisionInfluenceId ambition = decision.Influences[0].Id;

            Result applied = fixture.Host.Session.Execute(
                new ApplyDecisionInterventionCommand(decision.Id, TestWorld.InterventionStepUp, ambition));
            Assert.True(applied.IsSuccess);

            Die upgraded = decision.Influences[0].CurrentDie;

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);
            Decision reloaded = restored.Decisions.Get(decision.Id);

            Assert.Single(reloaded.Interventions);
            Assert.Equal(ambition, reloaded.Interventions[0].TargetInfluenceId);
            Assert.True(reloaded.TryGetInfluence(ambition, out DecisionInfluence influence));
            Assert.Equal(upgraded, influence.CurrentDie);
            Assert.Equal(Die.D10, influence.BaseDie);
        }

        [Fact]
        public void KnowledgeSurvivesReloadIncludingItsObservationTime()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Execute(new InspectCharacterCommand(fixture.Mina));

            Assert.True(fixture.Host.World.Knowledge.Count > 0);

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.Equal(fixture.Host.World.Knowledge.Count, restored.Knowledge.Count);

            var key = new Domain.Knowledge.FactKey(
                Domain.Knowledge.FactKinds.CharacterTrait, fixture.Mina.ToRef(), TestWorld.TraitAmbitious);

            Assert.True(restored.Knowledge.TryGet(key, out Domain.Knowledge.KnowledgeEntry entry));
            Assert.Equal(fixture.Host.World.Clock.Now, entry.ObservedAt);
        }

        [Fact]
        public void ObservationOrdinalsPersistSoRollsDoNotRepeatAfterReload()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Execute(new InspectCharacterCommand(fixture.Mina));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Mina));

            int ordinal = fixture.Host.World.Attention.ObservationOrdinal(fixture.Mina);
            Assert.True(ordinal >= 2);

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.Equal(ordinal, restored.Attention.ObservationOrdinal(fixture.Mina));
        }

        [Fact]
        public void EphemeralWatchStateIsNotSavedButFollowIs()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Execute(new FollowCharacterCommand(fixture.Mina, true));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Mina));

            Assert.True(fixture.Host.World.Attention.WatchStateOf(fixture.Mina).IsVisible);

            SaveGameData saved = fixture.Host.Session.Save("slot1");
            WorldState restored = fixture.Host.SaveMapper.Restore(saved);

            Assert.True(restored.Attention.WatchStateOf(fixture.Mina).IsFollowed);
            Assert.False(restored.Attention.WatchStateOf(fixture.Mina).IsVisible);
        }

        [Fact]
        public void SaveCarriesTheVersionMetadataThatScopesReproduction()
        {
            TestWorld fixture = TestWorld.Create(contentVersion: 7);
            SaveGameData saved = fixture.Host.Session.Save("slot1");

            Assert.Equal(SaveGameData.CurrentSchemaVersion, saved.SchemaVersion);
            Assert.Equal(7, saved.ContentVersion);
            Assert.Equal(1, saved.SimulationRulesVersion);
            Assert.Equal(Domain.Randomness.RandomAlgorithmVersion.Current, saved.RandomAlgorithmVersion);
            Assert.Equal(fixture.Clock.UtcNowTicks, saved.SavedAtRealTimeUtcTicks);
        }

        [Fact]
        public void VersionDriftIsDiagnosedButDoesNotBlockLoading()
        {
            // §39.1: an ordinary balance patch must not invalidate every existing save.
            TestWorld fixture = TestWorld.Create(contentVersion: 3);
            SaveGameData saved = fixture.Host.Session.Save("slot1");

            var migrator = new SaveMigrator();
            SaveCompatibilityReport report = migrator.Migrate(saved, currentContentVersion: 4, currentRulesVersion: 2, currentRandomAlgorithmVersion: 1);

            Assert.True(report.CanLoad);
            Assert.True(report.ContentVersionDiffers);
            Assert.True(report.RulesVersionDiffers);
            Assert.False(report.RandomAlgorithmVersionDiffers);
            Assert.True(report.ReproductionIsVersionScoped);
        }

        [Fact]
        public void FutureSchemaVersionIsRefused()
        {
            SaveGameData saved = new SaveGameData { SchemaVersion = SaveGameData.CurrentSchemaVersion + 5 };

            SaveCompatibilityReport report = new SaveMigrator().Migrate(saved, 1, 1, 1);

            Assert.False(report.CanLoad);
            Assert.Contains("newer than this build", report.BlockingReason);
        }

        [Fact]
        public void OfflineElapsedTimeComesFromTheAnchorNotTheDomain()
        {
            TestWorld fixture = TestWorld.Create();
            SaveGameData saved = fixture.Host.Session.Save("slot1");

            // Three real hours pass while the game is closed.
            fixture.Clock.AdvanceMinutes(180);

            var offline = new OfflineProgressionService(fixture.Clock, new OfflineProgressionPolicy(simMinutesPerRealMinute: 1));
            SimDuration elapsed = offline.ElapsedSince(saved);

            Assert.Equal(180, elapsed.TotalMinutes);
        }

        [Fact]
        public void OfflineCatchUpIsClampedByPolicy()
        {
            TestWorld fixture = TestWorld.Create();
            SaveGameData saved = fixture.Host.Session.Save("slot1");

            fixture.Clock.AdvanceMinutes(60 * 24 * 400);

            var offline = new OfflineProgressionService(fixture.Clock, new OfflineProgressionPolicy(1, maxCatchUpMinutes: 60 * 24 * 7));

            Assert.Equal(60 * 24 * 7, offline.ElapsedSince(saved).TotalMinutes);
        }

        [Fact]
        public void ClockMovingBackwardsGrantsNothingRatherThanRunningTimeBackwards()
        {
            TestWorld fixture = TestWorld.Create();
            SaveGameData saved = fixture.Host.Session.Save("slot1");

            fixture.Clock.UtcNowTicks = saved.SavedAtRealTimeUtcTicks - 100000;

            Assert.Equal(SimDuration.Zero, new OfflineProgressionService(fixture.Clock).ElapsedSince(saved));
        }

        [Fact]
        public void ReloadedWorldResolvesIdenticallyToTheOriginalRun()
        {
            // §56: saving before resolution and loading again preserves the outcome.
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(2));
            Decision decision = fixture.CreateDecision();
            fixture.Host.Session.Advance(SimDuration.Zero);

            SaveGameData saved = fixture.Host.Session.Save("checkpoint");

            fixture.Host.Session.Advance(SimDuration.FromHours(12));
            DecisionResolution original = fixture.Host.World.Decisions.Get(decision.Id).Resolution;
            Assert.NotNull(original);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, saved.LastCommandSequence, 1, null, fixture.Store, fixture.Clock);

            restored.Session.Advance(SimDuration.FromHours(12));
            DecisionResolution reloaded = restored.World.Decisions.Get(decision.Id).Resolution;

            Assert.NotNull(reloaded);
            Assert.Equal(original.ChosenOptionId, reloaded.ChosenOptionId);
            Assert.Equal(original.Degree, reloaded.Degree);
            Assert.Equal(original.Rolls.Count, reloaded.Rolls.Count);

            for (int i = 0; i < original.Rolls.Count; i++)
            {
                Assert.Equal(original.Rolls[i].Rolled, reloaded.Rolls[i].Rolled);
            }
        }

        [Fact]
        public void NeedPressureGeneratesAndCompletesARealDecision()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                fixture.Mina,
                TestWorld.ActivityWorking,
                fixture.Bakery,
                SimDuration.FromHours(12));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(510));

            Decision generated = null;
            foreach (Decision decision in fixture.Host.World.Decisions.All)
            {
                if (decision.DefinitionId == TestWorld.DecisionLeaveWork)
                {
                    generated = decision;
                }
            }

            Assert.NotNull(generated);
            Assert.Equal(DecisionStatus.Resolved, generated.Status);
            Assert.Equal(TestWorld.OptionLeave, generated.Resolution.ChosenOptionId);

            ActivityInstance current = fixture.Host.World.Activities.Get(
                fixture.Host.World.Characters.Get(fixture.Mina).CurrentActivityId);
            Assert.Equal(WellKnownActivities.Waiting, current.DefinitionId);
            Assert.Equal(fixture.Bakery, current.SpatialContext.LocationId);
        }

        [Fact]
        public void GeneratedDecisionAndConsequenceMatchAfterSaveReload()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                fixture.Mina,
                TestWorld.ActivityWorking,
                fixture.Bakery,
                SimDuration.FromHours(12));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(500));
            Assert.Single(fixture.Host.World.Decisions.All);
            Assert.Equal(DecisionStatus.Active, fixture.Host.World.Decisions.Get(new DecisionId(1)).Status);
            SaveGameData saved = fixture.Host.Session.Save("generated-decision");

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            ActivityInstance originalActivity = fixture.Host.World.Activities.Get(
                fixture.Host.World.Characters.Get(fixture.Mina).CurrentActivityId);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, fixture.Catalog, saved.LastCommandSequence, 1, null, fixture.Store, fixture.Clock);
            restored.Session.Advance(SimDuration.FromMinutes(10));
            ActivityInstance restoredActivity = restored.World.Activities.Get(
                restored.World.Characters.Get(fixture.Mina).CurrentActivityId);

            Assert.Equal(originalActivity.Id, restoredActivity.Id);
            Assert.Equal(originalActivity.DefinitionId, restoredActivity.DefinitionId);
            Assert.Equal(originalActivity.StartedAt, restoredActivity.StartedAt);
            Assert.Equal(originalActivity.SpatialContext.LocationId, restoredActivity.SpatialContext.LocationId);
        }

        [Fact]
        public void ScheduledEventWithoutACodecFailsLoudlyRatherThanVanishing()
        {
            TestWorld fixture = TestWorld.Create();

            fixture.Host.World.Scheduler.Schedule(
                fixture.Host.World.Clock.Now.Plus(SimDuration.FromHours(1)),
                Domain.Scheduling.SchedulePhase.Bookkeeping,
                new AuthoredId("event.unregistered"),
                new UnregisteredPayload());

            Assert.Throws<KeyNotFoundException>(() => fixture.Host.Session.Save("slot1"));
        }

        private sealed class UnregisteredPayload : Domain.Scheduling.IScheduledEventPayload
        {
        }
    }
}
