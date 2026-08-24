using System.Text;
using System.Linq;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.SimRunner.Tests
{
    public sealed class GoldenScenarioTests
    {
        private const long Seed = 827119;

        private sealed class Fixture
        {
            public DefinitionCatalog Catalog;
            public SimulationHost Host;
            public SampleWorldLayout Layout;
            public InMemorySaveGameStore Store;
            public FixedRealWorldClock Clock;
        }

        [Fact]
        public void GoldenScenarioConnectsTheWholeCausalChain()
        {
            Fixture fixture = Create();
            WorldState world = fixture.Host.World;

            fixture.Host.Session.Advance(SimDuration.FromHours(5));

            Assert.True(world.TryGetCurrentActivity(fixture.Layout.Mina, out ActivityInstance work));
            Assert.Equal(SampleContent.ActivityWorking, work.DefinitionId);
            Assert.True(work.HasModifier(SampleContent.ModifierDislikedColleague));

            var sharedSegment = new TravelSegmentKey(fixture.Layout.Home, fixture.Layout.Bakery);
            Assert.True(world.RelationshipIndex.TryGetBetween(fixture.Layout.Mina, fixture.Layout.Glen, out RelationshipId friendshipId));
            Relationship friendship = world.Relationships.Get(friendshipId);
            Assert.NotNull(friendship.LastInteractionAt);
            Assert.True(friendship.LastInteractionAt < world.Clock.Now);

            fixture.Host.Session.Execute(new FollowCharacterCommand(fixture.Layout.Mina, true));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Layout.Mina));
            fixture.Host.Session.Advance(SimDuration.FromMinutes(35));

            Decision decision = FindDecision(world, fixture.Layout.Mina);
            Assert.NotNull(decision.ReasoningProgram);
            Assert.Empty(fixture.Catalog.Decisions[SampleContent.DecisionLeaveWork].InfluenceTemplates);
            DecisionInfluence pressure = FindInfluence(decision, SampleContent.InfluenceBadWorkContext);
            DecisionInfluenceId stableInfluenceId = pressure.Id;
            Assert.Equal(Die.D10, pressure.CurrentDie);
            Assert.Equal(SampleContent.ContextWorkPressure, pressure.Evaluation.Signals[0].SignalId);
            Assert.Equal(10000, pressure.Evaluation.Signals[0].Mean);

            InfluenceView concealed = FindInfluenceView(
                new DecisionProjector(fixture.Catalog.Interventions).Project(world, decision),
                stableInfluenceId);
            Assert.Null(concealed.Label);
            Assert.Equal(SampleContent.CategorySocial.Value, concealed.Category);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);

            fixture.Host.Session.Execute(new EndObservingCharacterCommand(fixture.Layout.Mina));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Layout.Mina));
            InfluenceView revealed = FindInfluenceView(
                new DecisionProjector(fixture.Catalog.Interventions).Project(world, decision),
                stableInfluenceId);
            Assert.Equal(SampleContent.InfluenceBadWorkContext.Value, revealed.Label);

            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id,
                SampleContent.InterventionStepUp,
                stableInfluenceId)).IsSuccess);
            Assert.Equal(Die.D12, pressure.CurrentDie);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                fixture.Layout.Darius,
                WellKnownActivities.Waiting,
                fixture.Layout.Cafe,
                SimDuration.FromHours(1));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.False(work.HasModifier(SampleContent.ModifierDislikedColleague));
            Assert.Equal(stableInfluenceId, pressure.Id);
            Assert.Equal(Die.D8, pressure.CurrentDie);
            Assert.Equal(0, pressure.Evaluation.Signals[0].Mean);

            Assert.True(fixture.Host.Session.Execute(new ReleaseDecisionCommand(decision.Id)).IsSuccess);
            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.Equal(SampleContent.OptionLeave, decision.Resolution.ChosenOptionId);
            InfluenceRoll? frozenPressure = null;
            for (int i = 0; i < decision.Resolution.Rolls.Count; i++)
            {
                if (decision.Resolution.Rolls[i].InfluenceId == stableInfluenceId)
                {
                    frozenPressure = decision.Resolution.Rolls[i];
                    break;
                }
            }
            Assert.NotNull(frozenPressure);
            Assert.Equal(new AuthoredId("binding.leave_work.work_context"), frozenPressure.Value.Reason.BindingId);
            Assert.Equal(0, frozenPressure.Value.Reason.Evaluation.Signals[0].Mean);
            Assert.True(world.TryGetCurrentActivity(fixture.Layout.Mina, out ActivityInstance consequence));
            Assert.Equal(WellKnownActivities.Waiting, consequence.DefinitionId);
            Assert.Equal(fixture.Layout.Bakery, consequence.SpatialContext.LocationId);

            // The shared segment was derived state: neither character remains indexed there after arrival.
            Assert.DoesNotContain(fixture.Layout.Mina, world.Spatial.TravelersOn(sharedSegment));
            Assert.DoesNotContain(fixture.Layout.Glen, world.Spatial.TravelersOn(sharedSegment));
        }

        [Fact]
        public void OfflineCatchUpResolvesHeldGeneratedDecisionAndMatchesReload()
        {
            Fixture fixture = Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(5));
            fixture.Host.Session.Execute(new FollowCharacterCommand(fixture.Layout.Mina, true));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Layout.Mina));
            fixture.Host.Session.Advance(SimDuration.FromMinutes(35));

            Decision decision = FindDecision(fixture.Host.World, fixture.Layout.Mina);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);
            SaveGameData saved = fixture.Host.Session.Save("offline-golden");

            fixture.Clock.AdvanceMinutes(20);
            SimDuration elapsed = new OfflineProgressionService(fixture.Clock).ElapsedSince(saved);
            Assert.Equal(20, elapsed.TotalMinutes);

            fixture.Host.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);
            DecisionResolution uninterrupted = decision.Resolution;
            ActivityInstance uninterruptedActivity = fixture.Host.World.Activities.Get(
                fixture.Host.World.Characters.Get(fixture.Layout.Mina).CurrentActivityId);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);

            Assert.True(restored.World.Attention.WatchStateOf(fixture.Layout.Mina).IsFollowed);
            Assert.False(restored.World.Attention.WatchStateOf(fixture.Layout.Mina).IsVisible);
            restored.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);

            Decision reloadedDecision = restored.World.Decisions.Get(decision.Id);
            ActivityInstance reloadedActivity = restored.World.Activities.Get(
                restored.World.Characters.Get(fixture.Layout.Mina).CurrentActivityId);

            Assert.Equal(DecisionStatus.Resolved, reloadedDecision.Status);
            Assert.Equal(uninterrupted.ChosenOptionId, reloadedDecision.Resolution.ChosenOptionId);
            Assert.Equal(uninterrupted.Degree, reloadedDecision.Resolution.Degree);
            Assert.Equal(uninterruptedActivity.Id, reloadedActivity.Id);
            Assert.Equal(uninterruptedActivity.DefinitionId, reloadedActivity.DefinitionId);
            Assert.Equal(uninterruptedActivity.SpatialContext.LocationId, reloadedActivity.SpatialContext.LocationId);
        }

        [Fact]
        public void MixedActiveStateRemainsEquivalentAcrossOfflineCatchUpAndReload()
        {
            Fixture fixture = Create();
            fixture.Host.Session.Advance(SimDuration.FromHours(5));
            fixture.Host.Session.Advance(SimDuration.FromMinutes(35));

            WorldState world = fixture.Host.World;
            Decision minaDecision = FindDecision(world, fixture.Layout.Mina);
            Decision glenDecision = FindDecision(world, fixture.Layout.Glen);
            Decision dariusDecision = FindDecision(world, fixture.Layout.Darius);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(minaDecision.Id)).IsSuccess);
            Assert.False(world.Attention.IsHeld(glenDecision.Id));
            Assert.False(world.Attention.IsHeld(dariusDecision.Id));

            RearmNextHungerThreshold(fixture.Host, fixture.Layout.Mina);
            RearmNextHungerThreshold(fixture.Host, fixture.Layout.Glen);
            RearmNextHungerThreshold(fixture.Host, fixture.Layout.Darius);

            Assert.True(fixture.Host.Transitions.TryBeginTravel(
                fixture.Host.Simulation, fixture.Layout.Mina, fixture.Layout.Home, out ActivityInstance minaTravel));
            Assert.True(fixture.Host.Transitions.TryBeginTravel(
                fixture.Host.Simulation, fixture.Layout.Glen, fixture.Layout.Home, out ActivityInstance glenTravel));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.True(minaTravel.SpatialContext.IsTraveling);
            Assert.True(glenTravel.SpatialContext.IsTraveling);
            Assert.Contains(fixture.Layout.Mina, world.Spatial.Travelers);
            Assert.Contains(fixture.Layout.Glen, world.Spatial.Travelers);

            int activeCommitments = 0;
            foreach (Commitment commitment in world.Commitments.All)
            {
                if (commitment.Status == CommitmentStatus.Active)
                {
                    activeCommitments++;
                }
            }
            Assert.True(activeCommitments >= 2);
            AssertPendingNeedCrossing(world, fixture.Layout.Mina);
            AssertPendingNeedCrossing(world, fixture.Layout.Glen);
            AssertPendingNeedCrossing(world, fixture.Layout.Darius);

            SaveGameData saved = fixture.Host.Session.Save("mixed-offline");
            fixture.Clock.AdvanceMinutes(40);
            SimDuration elapsed = new OfflineProgressionService(fixture.Clock).ElapsedSince(saved);

            fixture.Host.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);
            string uninterrupted = AuthoritativeSignature(fixture.Host.World);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            restored.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);

            Assert.Equal(uninterrupted, AuthoritativeSignature(restored.World));
            Assert.Equal(DecisionStatus.Resolved, restored.World.Decisions.Get(minaDecision.Id).Status);
            Assert.Equal(DecisionStatus.Resolved, restored.World.Decisions.Get(glenDecision.Id).Status);
            Assert.Equal(DecisionStatus.Resolved, restored.World.Decisions.Get(dariusDecision.Id).Status);
            Assert.DoesNotContain(fixture.Layout.Mina, restored.World.Spatial.Travelers);
            Assert.DoesNotContain(fixture.Layout.Glen, restored.World.Spatial.Travelers);
            Assert.Contains(fixture.Layout.Mina, restored.World.Spatial.DirectOccupantsOf(fixture.Layout.Home));
            Assert.Contains(fixture.Layout.Glen, restored.World.Spatial.DirectOccupantsOf(fixture.Layout.Home));
        }

        [Fact]
        public void GoldenScenarioIntroducesAndResolvesAPlayerFacingCommitmentConflictAcrossReload()
        {
            Fixture fixture = Create();

            // The original leave-work encounter has completed; the future obligations are not known yet.
            fixture.Host.Session.Advance(SimDuration.FromHours(5).Plus(SimDuration.FromMinutes(45)));
            Assert.DoesNotContain(fixture.Host.World.Decisions.All,
                d => d.IsActive && d.DefinitionId == SampleContent.DecisionCommitmentConflict);
            SaveGameData beforeReveal = fixture.Host.Session.Save("before-commitment-conflict");

            fixture.Host.Session.Advance(SimDuration.FromMinutes(15));
            Decision uninterrupted = FindCommitmentConflict(fixture.Host.World, fixture.Layout.Mina);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(beforeReveal);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                beforeReveal.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            restored.Session.Advance(SimDuration.FromMinutes(15));
            Decision reloaded = FindCommitmentConflict(restored.World, fixture.Layout.Mina);

            Assert.Equal(uninterrupted.Id, reloaded.Id);
            Assert.Equal(uninterrupted.ResolveAt, reloaded.ResolveAt);
            Assert.Equal(2, uninterrupted.CommitmentConflictKey.ParticipatingCommitmentIds.Count);
            Assert.True(fixture.Host.World.Commitments.All.Count() > 2); // unrelated routine intent coexists

            Commitment closing = FindCommitment(
                fixture.Host.World, SampleContent.CommitmentHelpDariusCloseBakery);
            Commitment dinner = FindCommitment(
                fixture.Host.World, SampleContent.CommitmentDinnerWithGlen);
            Assert.Equal(fixture.Layout.MinaEmployment.ToRef(), closing.Source);
            Assert.Equal(StakeholderRole.Authority, Assert.Single(closing.Stakeholders).Role);
            Assert.False(closing.OverlapsWindowOf(dinner));
            Assert.True(fixture.Host.World.TravelNetwork.TryPlanRoute(
                fixture.Layout.Bakery, fixture.Layout.Cafe, out TravelPlan closingToDinner));
            Assert.True(closing.EarliestStart.Plus(closing.ExpectedDuration).Plus(closingToDinner.TotalCost) >
                dinner.LatestStart);

            DecisionView view = new DecisionProjector(fixture.Catalog.Interventions)
                .Project(fixture.Host.World, uninterrupted);
            Assert.True(view.HasHardDeadline);
            DecisionOptionView dinnerPlan = Assert.Single(
                view.Options,
                option => option.IntentSummary.Contains("Keep Dinner With Glen"));
            Assert.Contains("give up Help Darius Close Bakery", dinnerPlan.IntentSummary);
            Assert.Equal(dinnerPlan.IntentSummary, dinnerPlan.Label);

            SimDuration untilDeadline = uninterrupted.ResolveAt - fixture.Host.World.Clock.Now;
            fixture.Host.Session.Advance(untilDeadline);
            restored.Session.Advance(untilDeadline);

            Assert.Equal(DecisionStatus.Resolved, uninterrupted.Status);
            Assert.Equal(uninterrupted.Resolution.ChosenOptionId, reloaded.Resolution.ChosenOptionId);
            Assert.Equal(1, CountCommitmentsWithStatus(fixture.Host.World, CommitmentStatus.Relinquished));
            Assert.Equal(1, CountConflictCommitmentsNotWithStatus(
                fixture.Host.World, uninterrupted, CommitmentStatus.Relinquished));
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
        }

        [Fact]
        public void CommitmentAccountabilityChangesTheLaterRelianceDecisionAndReplaysAcrossSave()
        {
            Fixture kept = Create();
            Fixture breached = Create();
            kept.Host.Session.Advance(SimDuration.FromHours(6));
            breached.Host.Session.Advance(SimDuration.FromHours(6));

            Commitment keptDinner = FindCommitment(kept.Host.World, SampleContent.CommitmentDinnerWithGlen);
            Commitment breachedDinner = FindCommitment(breached.Host.World, SampleContent.CommitmentDinnerWithGlen);
            var lifecycle = new CommitmentLifecycleService();
            lifecycle.Start(kept.Host.World, keptDinner, kept.Host.World.RuntimeIds.Activities.Next());
            CommitmentOutcome fulfilled = lifecycle.Fulfill(kept.Host.World, keptDinner);
            Decision breachConflict = FindCommitmentConflict(breached.Host.World, breached.Layout.Mina);
            CommitmentOutcome relinquished = lifecycle.Relinquish(
                breached.Host.World, breachedDinner, breachConflict.Id);
            kept.Host.Session.Advance(SimDuration.Zero);
            breached.Host.Session.Advance(SimDuration.Zero);

            long keptReliance = GenerateRelianceInfluence(kept);
            long breachedReliance = GenerateRelianceInfluence(breached);
            Assert.True(keptReliance > breachedReliance,
                $"Fulfillment should support more Reliance than relinquishment ({keptReliance} vs {breachedReliance}).");

            Relationship keptFriendship = Friendship(kept);
            Relationship breachedFriendship = Friendship(breached);
            Assert.Empty(keptFriendship.From(kept.Layout.Glen).Memories);
            RelationshipMemory breachMemory = Assert.Single(
                breachedFriendship.From(breached.Layout.Glen).Memories);
            Assert.Equal(relinquished.Id, breachMemory.SourceOutcomeId);
            Assert.Equal(-1200, breachedFriendship.From(breached.Layout.Glen)
                .ChannelAt(RelationshipChannels.TrustJudgment, breached.Host.World.Clock.Now));
            Assert.Equal(0, keptFriendship.From(kept.Layout.Glen)
                .ChannelAt(RelationshipChannels.TrustJudgment, kept.Host.World.Clock.Now));
            Assert.Contains(breached.Host.World.Knowledge.AllObservers,
                entry => entry.Observer.Equals(ObserverRef.Character(breached.Layout.Glen)) &&
                         entry.Key.Kind == FactKinds.CommitmentOutcomeAttribution &&
                         entry.Source.SourceOutcomeId == relinquished.Id);
            Assert.DoesNotContain(kept.Host.World.HistoryLedger.Entries,
                entry => entry.SourceOutcomeId == fulfilled.Id);

            // Save before the conflict is introduced, then replay the breach path and require the exact
            // later Influence evaluation. The policy itself is carried by the pending reveal payload.
            Fixture replaySource = Create();
            replaySource.Host.Session.Advance(
                SimDuration.FromHours(5).Plus(SimDuration.FromMinutes(45)));
            SaveGameData beforeConflict = replaySource.Host.Session.Save("before-accountability-conflict");
            WorldState replayWorld = replaySource.Host.SaveMapper.Restore(beforeConflict);
            SimulationHost replayHost = SimulationBootstrapper.CreateFromRestoredWorld(
                replayWorld,
                replaySource.Catalog,
                beforeConflict.LastCommandSequence,
                1,
                null,
                replaySource.Store,
                replaySource.Clock);
            var replay = new Fixture
            {
                Catalog = replaySource.Catalog,
                Host = replayHost,
                Layout = replaySource.Layout,
                Store = replaySource.Store,
                Clock = replaySource.Clock,
            };
            replay.Host.Session.Advance(SimDuration.FromMinutes(15));
            Commitment replayDinner = FindCommitment(replay.Host.World, SampleContent.CommitmentDinnerWithGlen);
            Decision replayConflict = FindCommitmentConflict(replay.Host.World, replay.Layout.Mina);
            new CommitmentLifecycleService().Relinquish(replay.Host.World, replayDinner, replayConflict.Id);
            replay.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(breachedReliance, GenerateRelianceInfluence(replay));
        }

        private static Fixture Create()
        {
            DefinitionCatalog catalog = SampleContent.Build();
            var store = new InMemorySaveGameStore();
            var clock = new FixedRealWorldClock(1000000000000L);
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                Seed,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                1,
                null,
                store,
                clock);

            return new Fixture
            {
                Catalog = catalog,
                Host = host,
                Layout = SampleWorld.Populate(host),
                Store = store,
                Clock = clock,
            };
        }

        private static void RearmNextHungerThreshold(SimulationHost host, CharacterId characterId)
        {
            Character character = host.World.Characters.Get(characterId);
            host.Needs.SetThreshold(host.Simulation, character, SampleContent.NeedHunger, 8000);
        }

        private static void AssertPendingNeedCrossing(WorldState world, CharacterId characterId)
        {
            Assert.True(world.Characters.Get(characterId).TryGetNeed(SampleContent.NeedHunger, out NeedState need));
            Assert.True(need.PendingThresholdEventId.IsSet);
            Assert.True(world.Scheduler.Contains(need.PendingThresholdEventId));
        }

        private static string AuthoritativeSignature(WorldState world)
        {
            var text = new StringBuilder();
            RuntimeIdCounters ids = world.RuntimeIds.Snapshot();
            text.Append("clock:").Append(world.Clock.Now.TotalMinutes)
                .Append("|ids:").Append(ids.Characters).Append(',').Append(ids.Activities).Append(',')
                .Append(ids.Commitments).Append(',').Append(ids.CommitmentOutcomes).Append(',')
                .Append(ids.Relationships).Append(',').Append(ids.Decisions).Append(',').Append(ids.Employments)
                .Append(',').Append(ids.ScheduledEvents).Append(',').Append(ids.HistoryEntries).Append(',').Append(ids.EventSequence);

            foreach (ScheduledEvent scheduled in world.Scheduler.PendingEvents)
            {
                text.Append("|event:").Append(scheduled.Id.Value).Append(',').Append(scheduled.DueAt.TotalMinutes)
                    .Append(',').Append((int)scheduled.Phase).Append(',').Append(scheduled.EventSequence)
                    .Append(',').Append(scheduled.EventType.Value);
            }
            foreach (Character character in world.Characters.All)
            {
                text.Append("|char:").Append(character.Id.Value).Append(',').Append(character.CurrentActivityId.Value);
                foreach (var needPair in character.Needs)
                {
                    NeedState need = needPair.Value;
                    text.Append(",need:").Append(need.NeedId.Value).Append(',').Append(need.ValueAt(world.Clock.Now))
                        .Append(',').Append(need.BehaviouralThreshold).Append(',').Append(need.PendingThresholdEventId.Value);
                }
            }
            foreach (ActivityInstance activity in world.Activities.All)
            {
                text.Append("|activity:").Append(activity.Id.Value).Append(',').Append(activity.CharacterId.Value)
                    .Append(',').Append(activity.DefinitionId.Value).Append(',').Append((int)activity.Status)
                    .Append(',').Append(activity.StartedAt.TotalMinutes).Append(',').Append((int)activity.SpatialContext.Kind)
                    .Append(',').Append(activity.SpatialContext.DirectOccupancy.Value);
            }
            foreach (Commitment commitment in world.Commitments.All)
            {
                text.Append("|commitment:").Append(commitment.Id.Value).Append(',').Append((int)commitment.Status)
                    .Append(',').Append(commitment.FulfillingActivityId.Value);
            }
            foreach (Decision decision in world.Decisions.All)
            {
                text.Append("|decision:").Append(decision.Id.Value).Append(',').Append((int)decision.Status)
                    .Append(',').Append(decision.InfluenceRevision).Append(',');
                text.Append(decision.Resolution == null ? "-" : decision.Resolution.ChosenOptionId.Value.ToString());
                if (decision.Resolution != null)
                {
                    for (int i = 0; i < decision.Resolution.Rolls.Count; i++)
                    {
                        text.Append(',').Append(decision.Resolution.Rolls[i].Rolled);
                    }
                }
            }
            foreach (Relationship relationship in world.Relationships.All)
            {
                text.Append("|relationship:").Append(relationship.Id.Value).Append(',')
                    .Append(relationship.LowToHigh.ChannelAt(RelationshipChannels.Affection, world.Clock.Now)).Append(',')
                    .Append(relationship.LowToHigh.FamiliarityAt(world.Clock.Now)).Append(',')
                    .Append(relationship.HighToLow.ChannelAt(RelationshipChannels.Affection, world.Clock.Now)).Append(',')
                    .Append(relationship.HighToLow.FamiliarityAt(world.Clock.Now))
                    .Append(',').Append(relationship.LastInteractionAt?.TotalMinutes ?? -1);
            }
            foreach (Employment employment in world.Employments.All)
            {
                text.Append("|employment:").Append(employment.Id.Value).Append(',')
                    .Append(employment.EmployeeId.Value).Append(',').Append(employment.EmployerGroupId.Value).Append(',')
                    .Append(employment.DefinitionId.Value).Append(',').Append(employment.SupervisorId.Value);
            }

            return text.ToString();
        }

        private static Decision FindDecision(WorldState world, CharacterId character)
        {
            foreach (Decision decision in world.Decisions.All)
            {
                if (decision.IsActive && decision.CharacterId == character && decision.DefinitionId == SampleContent.DecisionLeaveWork)
                {
                    return decision;
                }
            }

            return null;
        }

        private static Decision FindCommitmentConflict(WorldState world, CharacterId character)
        {
            foreach (Decision decision in world.Decisions.All)
                if (decision.IsActive && decision.CharacterId == character &&
                    decision.DefinitionId == SampleContent.DecisionCommitmentConflict)
                    return decision;
            return null;
        }

        private static Commitment FindCommitment(WorldState world, AuthoredId kind) =>
            world.Commitments.All.Single(commitment => commitment.Kind == kind);

        private static Relationship Friendship(Fixture fixture)
        {
            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(
                fixture.Layout.Mina, fixture.Layout.Glen, out RelationshipId relationshipId));
            return fixture.Host.World.Relationships.Get(relationshipId);
        }

        private static long GenerateRelianceInfluence(Fixture fixture)
        {
            Relationship friendship = Friendship(fixture);
            fixture.Host.World.Publish(new InteractionOccurredEvent(
                fixture.Layout.Glen,
                fixture.Layout.Mina,
                fixture.Layout.Cafe,
                friendship.Id));
            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision decision = fixture.Host.World.Decisions.All.Single(item =>
                item.IsActive && item.CharacterId == fixture.Layout.Glen &&
                item.DefinitionId == SampleContent.DecisionRelyOnPerson);
            return Assert.Single(decision.Influences).Evaluation.ExpectedScore;
        }

        private static int CountCommitmentsWithStatus(WorldState world, CommitmentStatus status)
        {
            int count = 0;
            foreach (Commitment commitment in world.Commitments.All)
                if (commitment.Status == status) count++;
            return count;
        }

        private static int CountConflictCommitmentsWithStatus(
            WorldState world,
            Decision decision,
            CommitmentStatus status)
        {
            int count = 0;
            for (int i = 0; i < decision.CommitmentConflictKey.ParticipatingCommitmentIds.Count; i++)
                if (world.Commitments.Get(decision.CommitmentConflictKey.ParticipatingCommitmentIds[i]).Status == status)
                    count++;
            return count;
        }

        private static int CountConflictCommitmentsNotWithStatus(
            WorldState world,
            Decision decision,
            CommitmentStatus status) =>
            decision.CommitmentConflictKey.ParticipatingCommitmentIds.Count -
            CountConflictCommitmentsWithStatus(world, decision, status);

        private static DecisionInfluence FindInfluence(Decision decision, AuthoredId label)
        {
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                if (decision.Influences[i].LabelId == label)
                {
                    return decision.Influences[i];
                }
            }

            return null;
        }

        private static InfluenceView FindInfluenceView(DecisionView decision, DecisionInfluenceId id)
        {
            for (int option = 0; option < decision.Options.Count; option++)
            {
                for (int influence = 0; influence < decision.Options[option].Influences.Count; influence++)
                {
                    InfluenceView view = decision.Options[option].Influences[influence];
                    if (view.InfluenceId == id.Value)
                    {
                        return view;
                    }
                }
            }

            return null;
        }
    }
}
