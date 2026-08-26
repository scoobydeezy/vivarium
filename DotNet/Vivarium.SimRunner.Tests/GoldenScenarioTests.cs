using System.Text;
using System.Linq;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Application.Session;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
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
using Vivarium.Domain.Social;
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
            public MinimumPlayableWorldLayout Layout;
            public InMemorySaveGameStore Store;
            public FixedRealWorldClock Clock;
        }

        [Fact]
        public void MpsWorldStartsWithTheLockedTenPersonCastAndStaggeredLifeStates()
        {
            Fixture fixture = Create();
            WorldState world = fixture.Host.World;

            Assert.Equal(10, world.Characters.Count);
            Assert.Equal(10, new[]
            {
                fixture.Layout.Mina, fixture.Layout.Glen, fixture.Layout.Darius, fixture.Layout.Lena,
                fixture.Layout.Priya, fixture.Layout.Marcus, fixture.Layout.Tess, fixture.Layout.Owen,
                fixture.Layout.Jo, fixture.Layout.Ravi,
            }.Distinct().Count());
            Assert.Equal(fixture.Layout.Cafe, fixture.Layout.Commons);
            Assert.True(world.Locations.Get(fixture.Layout.Commons).SupportsPlayerManagedAvailability);

            Assert.Equal(WellKnownActivities.Eating, Current(world, fixture.Layout.Priya).DefinitionId);
            Assert.Equal(SampleContent.ActivityWorking, Current(world, fixture.Layout.Marcus).DefinitionId);
            Assert.Equal(WellKnownActivities.Sleeping, Current(world, fixture.Layout.Jo).DefinitionId);
            ActivityInstance ravi = Current(world, fixture.Layout.Ravi);
            Assert.Equal(WellKnownActivities.Traveling, ravi.DefinitionId);
            Assert.Equal(fixture.Layout.Bakery, ravi.SpatialContext.Transit.DestinationLocationId);

            Assert.Contains(world.Memberships.GroupsOf(fixture.Layout.Mina),
                group => world.Memberships.GroupsOf(fixture.Layout.Tess).Contains(group));
            Assert.Contains(world.Memberships.GroupsOf(fixture.Layout.Glen),
                group => world.Memberships.GroupsOf(fixture.Layout.Jo).Contains(group));
            Assert.Equal(6, world.Employments.Count);
            Assert.Contains(world.Commitments.All,
                commitment => commitment.CharacterId == fixture.Layout.Jo &&
                    commitment.LocationId == fixture.Layout.Commons);
        }

        [Fact]
        public void CharacterProfileCombinesScheduleAndPlayerKnowledgeWithoutRevealingUnknownRelationships()
        {
            Fixture fixture = Create();
            var projector = new CharacterProfileProjector();
            fixture.Host.Session.Execute(new InspectCharacterCommand(fixture.Layout.Mina));

            Assert.True(projector.TryProject(
                fixture.Host.World, fixture.Layout.Mina, out CharacterProfileView before));
            Assert.NotEmpty(before.Schedule.Entries);
            Assert.NotEmpty(before.KnownNeeds);
            Assert.Empty(before.KnownRelationships);

            Relationship friends = RelationshipBetween(
                fixture.Host.World, fixture.Layout.Mina, fixture.Layout.Glen);
            fixture.Host.World.Knowledge.Record(new KnowledgeEntry(
                new FactKey(FactKinds.RelationshipStanding, friends.Id.ToRef()),
                ObservedValue.Of(ValueBands.Strong),
                fixture.Host.World.Clock.Now,
                KnowledgeConfidence.Known,
                DiscoverySource.Channel(DiscoveryChannels.DirectObservation)));

            Assert.True(projector.TryProject(
                fixture.Host.World, fixture.Layout.Mina, out CharacterProfileView after));
            KnownRelationshipView known = Assert.Single(after.KnownRelationships);
            Assert.Equal("Glen Ashby", known.OtherCharacterName);
            Assert.Equal(ValueBands.Strong.ToString(), Assert.Single(known.KnownFacts).ValueLabel);
        }

        [Fact]
        public void MpsSocialTopologyIsDirectionalAndOrdinaryEvidenceChangesOwensLaterReasonAcrossReload()
        {
            Fixture fixture = Create();
            WorldState world = fixture.Host.World;
            SimTime now = world.Clock.Now;

            Relationship friends = RelationshipBetween(world, fixture.Layout.Mina, fixture.Layout.Glen);
            Assert.True(friends.From(fixture.Layout.Mina).FamiliarityAt(now) >
                        friends.From(fixture.Layout.Glen).FamiliarityAt(now));
            Assert.True(friends.From(fixture.Layout.Mina).ChannelAt(RelationshipChannels.Affection, now) >
                        friends.From(fixture.Layout.Glen).ChannelAt(RelationshipChannels.Affection, now));
            Assert.Contains(friends.From(fixture.Layout.Mina).Memories,
                memory => memory.ChannelEffects[RelationshipChannels.Affection] > 0);
            Assert.Contains(friends.From(fixture.Layout.Glen).Memories,
                memory => memory.ChannelEffects[RelationshipChannels.Affection] < 0);

            Relationship boss = RelationshipBetween(world, fixture.Layout.Mina, fixture.Layout.Darius);
            Assert.True(boss.From(fixture.Layout.Mina).FamiliarityAt(now) >
                        boss.From(fixture.Layout.Darius).FamiliarityAt(now));
            Assert.True(boss.From(fixture.Layout.Mina).ChannelAt(RelationshipChannels.Resentment, now) > 0);
            Relationship weak = RelationshipBetween(world, fixture.Layout.Owen, fixture.Layout.Lena);
            Assert.True(weak.From(fixture.Layout.Owen).FamiliarityAt(now) < 1000);

            Assert.True(world.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(fixture.Layout.Owen), fixture.Layout.Lena, out BeliefDistribution prior));
            long priorWarmth = prior.Mean[SocialDimensions.Warmth];
            long priorVariance = prior.Covariance(SocialDimensions.Warmth, SocialDimensions.Warmth);
            int priorEvidenceRevision = prior.EvidenceRevision;
            long priorReason = EvaluateAffiliation(fixture, fixture.Layout.Owen, fixture.Layout.Lena);
            SaveGameData saved = fixture.Host.Session.Save("before-owen-lena-evidence");

            SimDuration throughArrival = SimDuration.FromHours(8).Plus(SimDuration.FromMinutes(26));
            fixture.Host.Session.Advance(throughArrival);
            Assert.True(weak.LastInteractionAt > now,
                $"Owen={Current(world, fixture.Layout.Owen).DefinitionId}; " +
                $"Lena={Current(world, fixture.Layout.Lena).DefinitionId}; " +
                $"last interaction={weak.LastInteractionAt}");
            Assert.True(world.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(fixture.Layout.Owen), fixture.Layout.Lena, out BeliefDistribution revised));
            Assert.True(revised.EvidenceRevision > priorEvidenceRevision);
            Assert.True(revised.Mean[SocialDimensions.Warmth] < priorWarmth);
            Assert.True(revised.Covariance(SocialDimensions.Warmth, SocialDimensions.Warmth) < priorVariance);

            // Let the overlapping social choice resolve, then exercise the same ordinary interaction
            // occurrence again while both characters are still at the Commons. This later Decision is
            // built from the evidence-revised belief rather than Owen's authored first impression.
            fixture.Host.Session.Advance(SimDuration.FromMinutes(5));
            world.Publish(new InteractionOccurredEvent(
                fixture.Layout.Owen,
                fixture.Layout.Lena,
                fixture.Layout.Commons,
                weak.Id));
            fixture.Host.Session.Advance(SimDuration.Zero);
            Decision decision = Assert.Single(world.Decisions.All, item =>
                item.CharacterId == fixture.Layout.Owen &&
                item.DefinitionId == SampleContent.DecisionSeekCompany &&
                item.SnapshottedParameters.TryGetValue(
                    SocialInteractionDecisionGenerationHandler.TargetParameter, out long target) &&
                target == fixture.Layout.Lena.Value);
            DecisionInfluence laterReason = Assert.Single(decision.Influences, item => !item.IsRetracted);
            Assert.NotEqual(priorReason, laterReason.Evaluation.ExpectedScore);

            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            restored.Session.Advance(throughArrival);
            restored.Session.Advance(SimDuration.FromMinutes(5));
            Relationship restoredWeak = RelationshipBetween(
                restored.World, fixture.Layout.Owen, fixture.Layout.Lena);
            restored.World.Publish(new InteractionOccurredEvent(
                fixture.Layout.Owen,
                fixture.Layout.Lena,
                fixture.Layout.Commons,
                restoredWeak.Id));
            restored.Session.Advance(SimDuration.Zero);

            Assert.Equal(AuthoritativeSignature(world), AuthoritativeSignature(restored.World));
        }

        [Fact]
        public void MpsCastRunsTwoDaysDeterministicallyWithContinuousPrimaryActivitiesAndRoutineDiversity()
        {
            Fixture first = Create();
            Fixture second = Create();

            first.Host.Session.Advance(SimDuration.FromDays(2), SimulationMode.PlayerFastForward);
            second.Host.Session.Advance(SimDuration.FromDays(2), SimulationMode.PlayerFastForward);

            Assert.Equal(AuthoritativeSignature(first.Host.World), AuthoritativeSignature(second.Host.World));
            foreach (Character character in first.Host.World.Characters.All)
            {
                ActivityInstance current = Current(first.Host.World, character.Id);
                Assert.Equal(ActivityStatus.Active, current.Status);
                Assert.Contains(first.Host.World.Activities.All,
                    activity => activity.CharacterId == character.Id &&
                        activity.DefinitionId == WellKnownActivities.Sleeping &&
                        activity.Status != ActivityStatus.Active);
            }

            AuthoredId[] occurred = first.Host.World.Activities.All.Select(activity => activity.DefinitionId).Distinct().ToArray();
            Assert.Contains(WellKnownActivities.Sleeping, occurred);
            Assert.Contains(WellKnownActivities.Eating, occurred);
            Assert.Contains(SampleContent.ActivityWorking, occurred);
            Assert.Contains(WellKnownActivities.Traveling, occurred);
            Assert.Contains(SampleContent.ActivityTabletopGames, occurred);
            Assert.Contains(SampleContent.ActivityReading, occurred);
            Assert.Contains(SampleContent.ActivitySocializing, occurred);
        }

        [Fact]
        public void ClosingCommonsBeforeOwensAfternoonPlanningChangesHisOrdinaryBranch()
        {
            Fixture open = Create();
            Fixture managed = Create();

            Result close = managed.Host.Session.Execute(
                new SetLocationAvailabilityCommand(managed.Layout.Commons, open: false));
            Assert.True(close.IsSuccess);

            SimDuration untilAfterPlanning = SimDuration.FromHours(8).Plus(SimDuration.FromMinutes(30));
            open.Host.Session.Advance(untilAfterPlanning);
            managed.Host.Session.Advance(untilAfterPlanning);

            ActivityInstance openActivity = Current(open.Host.World, open.Layout.Owen);
            Assert.True(openActivity.DefinitionId == WellKnownActivities.Traveling ||
                openActivity.DefinitionId == SampleContent.ActivityTabletopGames);
            if (openActivity.SpatialContext.IsTraveling)
                Assert.Equal(open.Layout.Commons, openActivity.SpatialContext.Transit.DestinationLocationId);

            ActivityInstance managedActivity = Current(managed.Host.World, managed.Layout.Owen);
            Assert.Equal(SampleContent.ActivityReading, managedActivity.DefinitionId);
            Assert.Equal(managed.Layout.Home, managedActivity.SpatialContext.LocationId);
            Assert.DoesNotContain(managed.Host.World.Decisions.All,
                decision => decision.CharacterId == managed.Layout.Owen &&
                    decision.DefinitionId == SampleContent.DecisionChooseRecreation);
            Assert.False(managed.Host.World.Locations.Get(managed.Layout.Commons).IsOpen);
        }

        [Fact]
        public void ClosingCommonsDuringOwensTravelRedirectsTheTripAndReopeningRestoresAvailability()
        {
            Fixture fixture = Create();
            fixture.Host.Session.Advance(
                SimDuration.FromHours(8).Plus(SimDuration.FromMinutes(21)));

            ActivityInstance outbound = Current(fixture.Host.World, fixture.Layout.Owen);
            Assert.Equal(WellKnownActivities.Traveling, outbound.DefinitionId);
            Assert.Equal(fixture.Layout.Commons, outbound.SpatialContext.Transit.DestinationLocationId);

            Assert.True(fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Layout.Commons, open: false)).IsSuccess);
            ActivityInstance redirected = Current(fixture.Host.World, fixture.Layout.Owen);
            Assert.True(redirected.DefinitionId == WellKnownActivities.Traveling ||
                        redirected.DefinitionId == SampleContent.ActivityReading);
            if (redirected.SpatialContext.IsTraveling)
                Assert.Equal(fixture.Layout.Home, redirected.SpatialContext.Transit.DestinationLocationId);

            Assert.True(fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Layout.Commons, open: true)).IsSuccess);
            Assert.True(fixture.Host.World.Locations.Get(fixture.Layout.Commons).IsOpen);
            Assert.Equal(1, fixture.Host.World.Nudges.Balance);
            Assert.Equal(2, fixture.Host.World.HistoryLedger.Entries.Count(entry =>
                entry.Kind == LocationAvailabilityHistoryHandler.HistoryKind));
        }

        [Fact]
        public void AutoHoldPersistsAndQuietDoesNotReleaseOrEraseTheOutcomeRecap()
        {
            Fixture fixture = Create();
            Assert.True(fixture.Host.Session.Execute(new SetAttentionPolicyCommand(
                fixture.Layout.Owen, AttentionPolicy.AutoHold)).IsSuccess);
            fixture.Host.Session.Advance(
                SimDuration.FromHours(8).Plus(SimDuration.FromMinutes(11)));
            Decision decision = fixture.Host.World.Decisions.All.Single(candidate =>
                candidate.IsActive && candidate.CharacterId == fixture.Layout.Owen);
            Assert.True(decision.Importance >= fixture.Catalog.DecisionImportancePolicy.AutoHoldFloor,
                $"Owen importance {decision.Importance}, Auto-Hold floor {fixture.Catalog.DecisionImportancePolicy.AutoHoldFloor}.");
            Assert.True(fixture.Host.World.Attention.IsHeld(decision.Id));
            Assert.Contains(new DecisionFeedProjector(
                    fixture.Catalog.DecisionImportancePolicy, fixture.Host.HoldPolicy)
                .Project(fixture.Host.World).Entries, entry => entry.DecisionId == decision.Id.Value);

            Assert.True(fixture.Host.Session.Execute(new SetAttentionPolicyCommand(
                fixture.Layout.Owen, AttentionPolicy.Quiet)).IsSuccess);
            Assert.True(fixture.Host.World.Attention.IsHeld(decision.Id));
            SaveGameData saved = fixture.Host.Session.Save("mps-auto-held-quiet");
            SimulationHost restored = RestoreHost(fixture, saved);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(AttentionPolicy.Quiet, restored.World.Attention.PolicyFor(fixture.Layout.Owen));
            Assert.True(restored.World.Attention.IsHeld(restoredDecision.Id));

            Assert.True(fixture.Host.Session.Execute(new ReleaseDecisionCommand(decision.Id)).IsSuccess);
            Assert.True(restored.Session.Execute(new ReleaseDecisionCommand(restoredDecision.Id)).IsSuccess);
            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            restored.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.Equal(decision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Contains(new DecisionHistoryProjector().Project(fixture.Host.World, 5).Entries,
                entry => entry.Message.Contains("resolved"));
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
        }

        [Fact]
        public void QuietSuppressesANewDecisionFromTheFeedWithoutChangingItsSimulation()
        {
            Fixture normal = Create();
            Fixture quiet = Create();
            Assert.True(normal.Host.Session.Execute(new SetAttentionPolicyCommand(
                normal.Layout.Mina, AttentionPolicy.Normal)).IsSuccess);
            Assert.True(quiet.Host.Session.Execute(new SetAttentionPolicyCommand(
                quiet.Layout.Mina, AttentionPolicy.Quiet)).IsSuccess);
            SimDuration untilDecision = SimDuration.FromHours(5).Plus(SimDuration.FromMinutes(35));
            normal.Host.Session.Advance(untilDecision);
            quiet.Host.Session.Advance(untilDecision);

            Decision normalDecision = FindDecision(normal.Host.World, normal.Layout.Mina);
            Decision quietDecision = FindDecision(quiet.Host.World, quiet.Layout.Mina);
            var attentionTestPolicy = new DecisionImportancePolicyDefinition(0, 0, 0, 0);
            var normalFeed = new DecisionFeedProjector(
                attentionTestPolicy, normal.Host.HoldPolicy).Project(normal.Host.World);
            var quietFeed = new DecisionFeedProjector(
                attentionTestPolicy, quiet.Host.HoldPolicy).Project(quiet.Host.World);
            Assert.Contains(normalFeed.Entries, entry => entry.DecisionId == normalDecision.Id.Value);
            Assert.DoesNotContain(quietFeed.Entries, entry => entry.DecisionId == quietDecision.Id.Value);
            Assert.Equal(normalDecision.Importance, quietDecision.Importance);
            Assert.Equal(normalDecision.Influences.Count, quietDecision.Influences.Count);

            normal.Host.Session.Advance(SimDuration.FromMinutes(10));
            quiet.Host.Session.Advance(SimDuration.FromMinutes(10));
            Assert.Equal(normalDecision.Resolution.ChosenOptionId, quietDecision.Resolution.ChosenOptionId);
            Assert.Equal(normalDecision.Resolution.Degree, quietDecision.Resolution.Degree);
            Assert.Contains(new DecisionHistoryProjector().Project(quiet.Host.World, 5).Entries,
                entry => entry.Message.Contains("resolved"));
        }

        [Fact]
        public void FollowingACharacterPrioritizesTheirQualifyingDecisionAndUnfollowIsDurableStateOnly()
        {
            Fixture fixture = Create();
            Assert.True(fixture.Host.Session.Execute(new SetAttentionPolicyCommand(
                fixture.Layout.Mina, AttentionPolicy.Normal)).IsSuccess);
            fixture.Host.Session.Advance(
                SimDuration.FromHours(5).Plus(SimDuration.FromMinutes(35)));
            Decision glenDecision = FindDecision(fixture.Host.World, fixture.Layout.Glen);
            var projector = new DecisionFeedProjector(
                new DecisionImportancePolicyDefinition(0, 0, 0, 0), fixture.Host.HoldPolicy);

            Assert.True(fixture.Host.Session.Execute(
                new FollowCharacterCommand(fixture.Layout.Glen, true)).IsSuccess);
            DecisionFeedView followed = projector.Project(fixture.Host.World);
            DecisionFeedEntryView firstUnheld = followed.Entries.First(entry => !entry.IsHeld);
            Assert.Equal(fixture.Layout.Glen.Value, firstUnheld.CharacterId);
            Assert.Contains(followed.Entries, entry => entry.DecisionId == glenDecision.Id.Value);
            Assert.True(fixture.Host.World.Attention.WatchStateOf(fixture.Layout.Glen).IsFollowed);

            Assert.True(fixture.Host.Session.Execute(
                new FollowCharacterCommand(fixture.Layout.Glen, false)).IsSuccess);
            Assert.False(fixture.Host.World.Attention.WatchStateOf(fixture.Layout.Glen).IsFollowed);
            Assert.False(fixture.Host.World.Attention.WatchStateOf(fixture.Layout.Glen).IsWatched);
        }

        [Fact]
        public void ClosedCommonsBranchMatchesSaveReloadAndOfflineCatchUpInTheFullCastWorld()
        {
            Fixture fixture = Create();
            fixture.Host.Session.Advance(SimDuration.FromMinutes(61));
            Assert.True(fixture.Host.Session.Execute(
                new SetLocationAvailabilityCommand(fixture.Layout.Commons, open: false)).IsSuccess);
            Assert.Equal(2, fixture.Host.World.Nudges.Balance);
            SaveGameData saved = fixture.Host.Session.Save("mps-commons-closed");

            fixture.Clock.AdvanceMinutes(8 * 60);
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
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            restored.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);

            Assert.Equal(uninterrupted, AuthoritativeSignature(restored.World));
            Assert.False(restored.World.Locations.Get(fixture.Layout.Commons).IsOpen);
            Assert.Equal(SampleContent.ActivityReading,
                Current(restored.World, fixture.Layout.Owen).DefinitionId);
        }

        [Fact]
        public void ProductionRecreationRetriesAfterAnAcceptedSocialInvitationInterruptsTheFirstPlan()
        {
            Fixture fixture = Create();

            fixture.Host.Session.Advance(SimDuration.FromMinutes(5));

            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Glen, out ActivityInstance travel));
            Assert.Equal(WellKnownActivities.Traveling, travel.DefinitionId);
            Assert.Equal(fixture.Layout.Cafe, travel.SpatialContext.Transit.DestinationLocationId);
            Assert.DoesNotContain(fixture.Host.World.Decisions.All,
                decision => decision.CharacterId == fixture.Layout.Glen);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(5));

            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Glen, out ActivityInstance firstPlan));
            Assert.Equal(SampleContent.ActivityTabletopGames, firstPlan.DefinitionId);
            Decision invitation = fixture.Host.World.Decisions.All.Single(
                d => d.DefinitionId == SampleContent.DecisionSocialInvitation);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(SampleContent.OptionJoinInvitation, invitation.Resolution.ChosenOptionId);
            Assert.Equal(ActivityStatus.Abandoned, firstPlan.Status);
            Assert.Equal(SampleContent.ActivitySocializing,
                fixture.Host.World.Activities.Get(
                    fixture.Host.World.Characters.Get(fixture.Layout.Glen).CurrentActivityId).DefinitionId);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(30));

            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Glen, out ActivityInstance retriedPlan));
            Assert.Equal(SampleContent.ActivityTabletopGames, retriedPlan.DefinitionId);
            Assert.NotEqual(firstPlan.Id, retriedPlan.Id);
        }

        [Fact]
        public void ProductionSocialNeedInvitesACharacterWhoAlreadyHasAPlan()
        {
            Fixture fixture = Create();

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Lena, out ActivityInstance socializing));
            Assert.Equal(SampleContent.ActivitySocializing, socializing.DefinitionId);
            Assert.Equal(fixture.Layout.Glen.Value, socializing.CommittedParameterOr(
                SocializingRoutineService.TargetCharacterParameter,
                0));
            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Glen, out ActivityInstance tabletop));
            Assert.Equal(SampleContent.ActivityTabletopGames, tabletop.DefinitionId);
            Decision invitation = fixture.Host.World.Decisions.All.Single(
                d => d.DefinitionId == SampleContent.DecisionSocialInvitation);
            Assert.Equal(fixture.Layout.Glen, invitation.CharacterId);
            Assert.True(invitation.IsActive);
            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(
                fixture.Layout.Lena,
                fixture.Layout.Glen,
                out RelationshipId _));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(SampleContent.OptionJoinInvitation, invitation.Resolution.ChosenOptionId);
            Assert.Equal(SampleContent.ActivitySocializing,
                fixture.Host.World.Activities.Get(
                    fixture.Host.World.Characters.Get(fixture.Layout.Glen).CurrentActivityId).DefinitionId);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(20));

            Assert.True(fixture.Host.World.TryGetCurrentActivity(fixture.Layout.Lena, out ActivityInstance waiting));
            Assert.Equal(WellKnownActivities.Waiting, waiting.DefinitionId);
            Character lena = fixture.Host.World.Characters.Get(fixture.Layout.Lena);
            Assert.True(lena.TryGetNeed(WellKnownNeeds.Social, out NeedState social));
            Assert.True(social.ValueAt(fixture.Host.World.Clock.Now) < social.BehaviouralThreshold);
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
            Assert.Equal(WellKnownActivities.Traveling, consequence.DefinitionId);
            Assert.Equal(fixture.Layout.Home, consequence.SpatialContext.Transit.DestinationLocationId);

            // The shared segment was derived state: neither character remains indexed there after arrival.
            Assert.DoesNotContain(fixture.Layout.Mina, world.Spatial.TravelersOn(sharedSegment));
            Assert.DoesNotContain(fixture.Layout.Glen, world.Spatial.TravelersOn(sharedSegment));
        }

        [Fact]
        public void FullCastHeldDecisionSupportsEmphasizeAndTemperAcrossLivingReevaluationAndReload()
        {
            AssertPreRollNudgeBranch(
                SampleContent.InterventionStepUp,
                Die.D12,
                Die.D8,
                "mps-emphasize");
            AssertPreRollNudgeBranch(
                SampleContent.InterventionTemper,
                Die.D8,
                Die.D4,
                "mps-temper");
        }

        [Fact]
        public void FullCastHeldDecisionSubstitutesLoadedTwentyAndResolvesNormallyAcrossReload()
        {
            Fixture fixture = Create();
            Decision decision = PrepareHeldVisibleMinaDecision(fixture);
            DecisionInfluence influence = FindInfluence(decision, SampleContent.InfluenceBadWorkContext);
            DecisionInfluenceId stableId = influence.Id;
            var projector = new DecisionProjector(fixture.Catalog.Interventions);
            InterventionAvailabilityView availability = FindInfluenceView(
                    projector.Project(fixture.Host.World, decision), stableId)
                .Interventions.Single(item =>
                    item.InterventionDefinitionId == SampleContent.InterventionLoadedTwenty.Value);

            Assert.True(availability.IsAvailable);
            Assert.Equal("ReplacementDie", availability.ResourceKind);
            Assert.Equal(1, availability.Cost);
            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, SampleContent.InterventionLoadedTwenty, stableId)).IsSuccess);
            Assert.Equal(new Die(20, 20), influence.CurrentDie);
            Assert.Equal(0, ResourceBalance(fixture.Host.World, InterventionResourceKind.ReplacementDie));
            Assert.Equal(3, fixture.Host.World.Nudges.Balance);
            Assert.Null(decision.Resolution);

            SaveGameData saved = fixture.Host.Session.Save("mps-loaded-twenty");
            SimulationHost restored = RestoreHost(fixture, saved);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);
            Assert.True(restoredDecision.TryGetInfluence(stableId, out DecisionInfluence restoredInfluence));
            Assert.Equal(new Die(20, 20), restoredInfluence.CurrentDie);
            Assert.Equal(0, ResourceBalance(restored.World, InterventionResourceKind.ReplacementDie));

            Assert.True(fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id)).IsSuccess);
            Assert.True(restored.Session.Execute(new BeginDecisionResolutionCommand(restoredDecision.Id)).IsSuccess);
            InfluenceRoll originalRoll = Assert.Single(
                decision.PendingResolution.AcceptedRolls, item => item.InfluenceId == stableId);
            InfluenceRoll restoredRoll = Assert.Single(
                restoredDecision.PendingResolution.AcceptedRolls, item => item.InfluenceId == stableId);
            Assert.Equal(20, originalRoll.Rolled);
            Assert.Equal(new Die(20, 20), originalRoll.Die);
            Assert.Equal(originalRoll.Rolled, restoredRoll.Rolled);
            Assert.Null(decision.Resolution);

            Assert.True(fixture.Host.Session.Execute(new CommitDecisionResolutionCommand(decision.Id)).IsSuccess);
            Assert.True(restored.Session.Execute(new CommitDecisionResolutionCommand(restoredDecision.Id)).IsSuccess);
            AssertNormalResolutionOwnsWinner(decision.Resolution);
            Assert.Equal(decision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Equal(decision.Resolution.Degree, restoredDecision.Resolution.Degree);
            Assert.Equal(20, Assert.Single(
                restoredDecision.Resolution.Rolls, item => item.InfluenceId == stableId).Rolled);
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
        }

        [Fact]
        public void FullCastKnownRollRerollsNextScopedIndexAndPreservesDiscardedEvidenceAcrossReload()
        {
            Fixture fixture = Create();
            Decision decision = PrepareHeldVisibleMinaDecision(fixture);
            DecisionInfluence target = FindInfluence(decision, SampleContent.InfluenceBadWorkContext);
            Assert.True(fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id)).IsSuccess);
            InfluenceRoll initial = Assert.Single(
                decision.PendingResolution.AcceptedRolls, item => item.InfluenceId == target.Id);
            SaveGameData saved = fixture.Host.Session.Save("mps-before-reroll");

            SimulationHost restored = RestoreHost(fixture, saved);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);
            InterventionAvailabilityView availability = FindInfluenceView(
                    new DecisionProjector(fixture.Catalog.Interventions).Project(
                        fixture.Host.World, decision), target.Id)
                .Interventions.Single(item =>
                    item.InterventionDefinitionId == SampleContent.InterventionReroll.Value);
            Assert.True(availability.IsAvailable);
            Assert.Equal("ReRoll", availability.ResourceKind);
            Assert.Equal(1, availability.Cost);

            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, SampleContent.InterventionReroll, target.Id)).IsSuccess);
            Assert.True(restored.Session.Execute(new ApplyDecisionInterventionCommand(
                restoredDecision.Id, SampleContent.InterventionReroll, target.Id)).IsSuccess);
            InfluenceRoll accepted = Assert.Single(
                decision.PendingResolution.AcceptedRolls, item => item.InfluenceId == target.Id);
            InfluenceRoll restoredAccepted = Assert.Single(
                restoredDecision.PendingResolution.AcceptedRolls, item => item.InfluenceId == target.Id);
            InfluenceRoll discarded = Assert.Single(decision.PendingResolution.SupersededRolls);
            InfluenceRoll restoredDiscarded = Assert.Single(restoredDecision.PendingResolution.SupersededRolls);

            Assert.Equal(initial.RollIndex + 1, accepted.RollIndex);
            Assert.Equal(initial.Rolled, discarded.Rolled);
            Assert.Equal(initial.RollIndex, discarded.RollIndex);
            Assert.Equal(accepted.Rolled, restoredAccepted.Rolled);
            Assert.Equal(accepted.RollIndex, restoredAccepted.RollIndex);
            Assert.Equal(discarded.Rolled, restoredDiscarded.Rolled);
            Assert.All(decision.PendingResolution.AcceptedRolls.Where(item => item.InfluenceId != target.Id),
                item => Assert.Equal(0, item.RollIndex));
            Assert.Equal(0, ResourceBalance(fixture.Host.World, InterventionResourceKind.ReRoll));
            Assert.Equal(0, ResourceBalance(restored.World, InterventionResourceKind.ReRoll));
            Assert.Equal(3, fixture.Host.World.Nudges.Balance);

            Assert.True(fixture.Host.Session.Execute(new CommitDecisionResolutionCommand(decision.Id)).IsSuccess);
            Assert.True(restored.Session.Execute(new CommitDecisionResolutionCommand(restoredDecision.Id)).IsSuccess);
            Assert.Equal(accepted.Rolled, Assert.Single(
                decision.Resolution.Rolls, item => item.InfluenceId == target.Id).Rolled);
            Assert.Equal(initial.Rolled, Assert.Single(decision.Resolution.SupersededRolls).Rolled);
            Assert.Equal(decision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Equal(decision.Resolution.Degree, restoredDecision.Resolution.Degree);
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
        }

        [Fact]
        public void FullCastPendingRollExpiryCommitsOfflineWithoutSpendingRerollAcrossReload()
        {
            Fixture fixture = Create();
            Decision decision = PrepareHeldVisibleMinaDecision(fixture);
            Assert.True(fixture.Host.Session.Execute(new BeginDecisionResolutionCommand(decision.Id)).IsSuccess);
            SaveGameData saved = fixture.Host.Session.Save("mps-pending-expiry");
            SimulationHost restored = RestoreHost(fixture, saved);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(15), SimulationMode.OfflineCatchUp);
            restored.Session.Advance(SimDuration.FromMinutes(15), SimulationMode.OfflineCatchUp);

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
            Assert.Equal(DecisionStatus.Resolved, restoredDecision.Status);
            Assert.Equal(1, ResourceBalance(fixture.Host.World, InterventionResourceKind.ReRoll));
            Assert.Equal(1, ResourceBalance(restored.World, InterventionResourceKind.ReRoll));
            Assert.Equal(decision.Resolution.ChosenOptionId, restoredDecision.Resolution.ChosenOptionId);
            Assert.Equal(decision.Resolution.Degree, restoredDecision.Resolution.Degree);
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
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
            MinimumPlayableWorld.ConfigureScenarioServices(restored);

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
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(minaDecision.Id)).IsSuccess);
            Assert.False(world.Attention.IsHeld(glenDecision.Id));
            Assert.Null(FindDecision(world, fixture.Layout.Darius));

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
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            restored.Session.Advance(elapsed, SimulationMode.OfflineCatchUp);

            Assert.Equal(uninterrupted, AuthoritativeSignature(restored.World));
            Assert.Equal(DecisionStatus.Resolved, restored.World.Decisions.Get(minaDecision.Id).Status);
            Assert.Equal(DecisionStatus.Resolved, restored.World.Decisions.Get(glenDecision.Id).Status);
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
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
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
            Assert.DoesNotContain(keptFriendship.From(kept.Layout.Glen).Memories,
                memory => memory.SourceOutcomeId.IsSet);
            RelationshipMemory breachMemory = Assert.Single(
                breachedFriendship.From(breached.Layout.Glen).Memories,
                memory => memory.SourceOutcomeId == relinquished.Id);
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
            MinimumPlayableWorld.ConfigureScenarioServices(replayHost);
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
                Layout = MinimumPlayableWorld.Populate(host),
                Store = store,
                Clock = clock,
            };
        }

        private static ActivityInstance Current(WorldState world, CharacterId characterId) =>
            world.Activities.Get(world.Characters.Get(characterId).CurrentActivityId);

        private static void AssertPreRollNudgeBranch(
            AuthoredId interventionId,
            Die expectedAppliedDie,
            Die expectedReevaluatedDie,
            string saveSlot)
        {
            Fixture fixture = Create();
            Decision decision = PrepareHeldVisibleMinaDecision(fixture);
            DecisionInfluence influence = FindInfluence(decision, SampleContent.InfluenceBadWorkContext);
            DecisionInfluenceId stableId = influence.Id;
            var projector = new DecisionProjector(fixture.Catalog.Interventions);
            InterventionAvailabilityView availability = FindInfluenceView(
                    projector.Project(fixture.Host.World, decision), stableId)
                .Interventions.Single(item => item.InterventionDefinitionId == interventionId.Value);

            Assert.True(availability.IsAvailable);
            Assert.Equal("Nudge", availability.ResourceKind);
            Assert.Equal(1, availability.Cost);
            Assert.Equal(Die.D10, influence.CurrentDie);
            Assert.True(fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id, interventionId, stableId)).IsSuccess);

            Assert.Equal(2, fixture.Host.World.Nudges.Balance);
            Assert.Equal(expectedAppliedDie, influence.CurrentDie);
            Assert.Equal(DecisionStatus.Active, decision.Status);
            Assert.Null(decision.Resolution);
            Assert.True(fixture.Host.World.Attention.IsHeld(decision.Id));
            Assert.Equal(SampleContent.ActivityWorking, Current(fixture.Host.World, fixture.Layout.Mina).DefinitionId);
            AppliedIntervention applied = Assert.Single(
                decision.Interventions, item => item.InterventionDefinitionId == interventionId);
            Assert.Equal(stableId, applied.TargetInfluenceId);
            Assert.Equal(InterventionResourceKind.Nudge, applied.ResourceKind);
            Assert.Equal(1, applied.ResourceCost);

            SaveGameData saved = fixture.Host.Session.Save(saveSlot);
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);
            Assert.True(restoredDecision.TryGetInfluence(stableId, out DecisionInfluence restoredInfluence));
            Assert.Equal(2, restored.World.Nudges.Balance);
            Assert.Equal(expectedAppliedDie, restoredInfluence.CurrentDie);
            Assert.True(restored.World.Attention.IsHeld(restoredDecision.Id));
            Assert.Null(restoredDecision.Resolution);
            Assert.Single(restoredDecision.Interventions,
                item => item.InterventionDefinitionId == interventionId && item.TargetInfluenceId == stableId);

            MoveDariusOutOfWorkContext(fixture.Host, fixture.Layout);
            MoveDariusOutOfWorkContext(restored, fixture.Layout);

            Assert.Equal(stableId, influence.Id);
            Assert.Equal(stableId, restoredInfluence.Id);
            Assert.Equal(expectedReevaluatedDie, influence.CurrentDie);
            Assert.Equal(expectedReevaluatedDie, restoredInfluence.CurrentDie);
            Assert.Equal(DecisionStatus.Active, decision.Status);
            Assert.Equal(DecisionStatus.Active, restoredDecision.Status);
            Assert.Null(decision.Resolution);
            Assert.Null(restoredDecision.Resolution);
            Assert.Equal(AuthoritativeSignature(fixture.Host.World), AuthoritativeSignature(restored.World));
        }

        private static Decision PrepareHeldVisibleMinaDecision(Fixture fixture)
        {
            fixture.Host.Session.Advance(SimDuration.FromHours(5));
            fixture.Host.Session.Execute(new FollowCharacterCommand(fixture.Layout.Mina, true));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Layout.Mina));
            fixture.Host.Session.Advance(SimDuration.FromMinutes(35));
            Decision decision = FindDecision(fixture.Host.World, fixture.Layout.Mina);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);
            fixture.Host.Session.Execute(new EndObservingCharacterCommand(fixture.Layout.Mina));
            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Layout.Mina));
            return decision;
        }

        private static void MoveDariusOutOfWorkContext(SimulationHost host, MinimumPlayableWorldLayout layout)
        {
            host.Transitions.BeginActivity(
                host.Simulation,
                layout.Darius,
                WellKnownActivities.Waiting,
                layout.Commons,
                SimDuration.FromHours(1));
            host.Session.Advance(SimDuration.Zero);
        }

        private static SimulationHost RestoreHost(Fixture fixture, SaveGameData saved)
        {
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            return restored;
        }

        private static int ResourceBalance(WorldState world, InterventionResourceKind kind) =>
            world.InterventionResources.All.Single(pair => pair.Key == kind).Value.Balance;

        private static void AssertNormalResolutionOwnsWinner(DecisionResolution resolution)
        {
            OptionTotal expected = resolution.OptionTotals
                .OrderByDescending(total => total.Total)
                .ThenBy(total => total.OrderIndex)
                .First();
            Assert.Equal(expected.OptionId, resolution.ChosenOptionId);
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
            text.Append("|nudges:").Append(world.Nudges.Balance).Append(',').Append(world.Nudges.Revision);

            foreach (LocationNode location in world.Locations.Nodes.All)
            {
                text.Append("|location:").Append(location.Id.Value).Append(',')
                    .Append(location.IsOpen ? 1 : 0).Append(',')
                    .Append(location.SupportsPlayerManagedAvailability ? 1 : 0);
            }

            foreach (ScheduledEvent scheduled in world.Scheduler.PendingEvents)
            {
                text.Append("|event:").Append(scheduled.Id.Value).Append(',').Append(scheduled.DueAt.TotalMinutes)
                    .Append(',').Append((int)scheduled.Phase).Append(',').Append(scheduled.EventSequence)
                    .Append(',').Append(scheduled.EventType.Value);
            }
            foreach (Character character in world.Characters.All)
            {
                WatchState watch = world.Attention.WatchStateOf(character.Id);
                text.Append("|char:").Append(character.Id.Value).Append(',').Append(character.CurrentActivityId.Value)
                    .Append(",attention:").Append((int)world.Attention.PolicyFor(character.Id)).Append(',')
                    .Append(watch.IsFollowed ? 1 : 0);
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
            foreach (LocationNode location in world.Locations.Nodes.All)
            {
                text.Append("|location:").Append(location.Id.Value);
                for (int i = 0; i < location.ActivityAffordances.Count; i++)
                    text.Append(",affords:").Append(location.ActivityAffordances[i].Value);
            }
            foreach (Commitment commitment in world.Commitments.All)
            {
                text.Append("|commitment:").Append(commitment.Id.Value).Append(',').Append((int)commitment.Status)
                    .Append(',').Append(commitment.FulfillingActivityId.Value);
            }
            foreach (Decision decision in world.Decisions.All)
            {
                text.Append("|decision:").Append(decision.Id.Value).Append(',').Append((int)decision.Status)
                    .Append(',').Append(decision.InfluenceRevision).Append(',')
                    .Append(world.Attention.IsHeld(decision.Id) ? 1 : 0).Append(',')
                    .Append((int)world.Attention.PolicyFor(decision.Id)).Append(',');
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

        private static Relationship RelationshipBetween(
            WorldState world,
            CharacterId first,
            CharacterId second)
        {
            Assert.True(world.RelationshipIndex.TryGetBetween(first, second, out RelationshipId id));
            return world.Relationships.Get(id);
        }

        private static long EvaluateAffiliation(
            Fixture fixture,
            CharacterId observer,
            CharacterId target) =>
            new SocialPressureEvaluator().Evaluate(
                fixture.Host.World,
                observer,
                target,
                AppraisalLenses.Affiliation,
                new SocialEvaluationContext(),
                fixture.Catalog.SocialPressures[SampleContent.SocialPressureSeekCompany],
                fixture.Catalog).NormalizedAppraisal;

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
