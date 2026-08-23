using System.Collections.Generic;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Application.Tests
{
    /// <summary>
    /// Command ingress, quiescent read boundaries, and knowledge-filtered projections
    /// (§2.2.1, §13.1, §26, §51).
    /// </summary>
    public sealed class CommandAndProjectionTests
    {
        [Fact]
        public void CommandsExecuteInIngressOrderOneAtATime()
        {
            TestWorld fixture = TestWorld.Create();

            CommandEnvelope first = fixture.Host.Session.Enqueue(new FollowCharacterCommand(fixture.Mina, true));
            CommandEnvelope second = fixture.Host.Session.Enqueue(new InspectCharacterCommand(fixture.Mina));
            CommandEnvelope third = fixture.Host.Session.Enqueue(new FollowCharacterCommand(fixture.Mina, false));

            Assert.Equal(1, first.CommandSequence);
            Assert.Equal(2, second.CommandSequence);
            Assert.Equal(3, third.CommandSequence);

            Assert.Equal(3, fixture.Host.Session.Pump());

            // Last command wins because they ran in order, not because of any other reason.
            Assert.False(fixture.Host.World.Attention.WatchStateOf(fixture.Mina).IsFollowed);
            Assert.Equal(3, fixture.Host.Session.Commands.LastIssuedSequence);
        }

        [Fact]
        public void EnqueuingDoesNotMutateUntilThePumpRuns()
        {
            TestWorld fixture = TestWorld.Create();

            fixture.Host.Session.Enqueue(new FollowCharacterCommand(fixture.Mina, true));

            Assert.False(fixture.Host.World.Attention.WatchStateOf(fixture.Mina).IsFollowed);

            fixture.Host.Session.Pump();

            Assert.True(fixture.Host.World.Attention.WatchStateOf(fixture.Mina).IsFollowed);
        }

        [Fact]
        public void ProjectionsArePublishedOnlyAtQuiescence()
        {
            TestWorld fixture = TestWorld.Create();
            var publishedAt = new List<long>();

            fixture.Host.Projections.Subscribe((world, context) =>
            {
                publishedAt.Add(world.Clock.Now.TotalMinutes);

                // The contract: at a publish point there is no unsettled work left (§13.1).
                Assert.False(world.DomainEvents.HasPending);
                Domain.Scheduling.ScheduledEvent next = world.Scheduler.PeekNext();
                Assert.True(next == null || next.DueAt > world.Clock.Now);
            });

            fixture.Host.Session.Advance(SimDuration.FromHours(6));

            Assert.NotEmpty(publishedAt);
        }

        [Fact]
        public void CommandSequenceIsSeparateFromEventSequence()
        {
            // §34: deliberately distinct counters with different scope and lifetime.
            TestWorld fixture = TestWorld.Create();
            long eventSequenceBefore = fixture.Host.World.RuntimeIds.EventSequence.Issued;

            fixture.Host.Session.Enqueue(new FollowCharacterCommand(fixture.Mina, true));
            fixture.Host.Session.Pump();

            Assert.Equal(1, fixture.Host.Session.Commands.LastIssuedSequence);
            Assert.Equal(eventSequenceBefore, fixture.Host.World.RuntimeIds.EventSequence.Issued);
        }

        [Fact]
        public void HoldRespectsThePerCharacterCapAcrossConcurrentDecisions()
        {
            // §17.1 plus §20: the cap counts every one of the character's decisions, not one per decision.
            TestWorld fixture = TestWorld.Create();

            var held = new List<DecisionId>();
            for (int i = 0; i < 5; i++)
            {
                Decision decision = fixture.CreateDecision();
                Result result = fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id));

                if (result.IsSuccess)
                {
                    held.Add(decision.Id);
                }
            }

            Assert.Equal(fixture.Host.HoldPolicy.MaxHeldPerCharacter, held.Count);
            Assert.Equal(held.Count, fixture.Host.World.Attention.HeldCount);
        }

        [Fact]
        public void ReleasingAHeldDecisionRearmsItsResolution()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();

            fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id));
            fixture.Host.Session.Advance(SimDuration.FromHours(12));

            // Still open: the player is holding it, and holding does not freeze the rest of the world.
            Assert.True(decision.IsActive);
            Assert.True(fixture.Host.World.Clock.Now > decision.ResolveAt);

            fixture.Host.Session.Execute(new ReleaseDecisionCommand(decision.Id));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(DecisionStatus.Resolved, decision.Status);
        }

        [Fact]
        public void HoldingIsRefusedDuringOfflineCatchUp()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();

            // Offline catch-up is a formally distinct mode, not just a fast Live (§21).
            SimulationContext offline = fixture.Host.Simulation.WithMode(SimulationMode.OfflineCatchUp);
            var context = new CommandContext(offline, 1);

            Result result = new Commands.Handlers.HoldDecisionHandler(fixture.Host.HoldPolicy)
                .Handle(new HoldDecisionCommand(decision.Id), context);

            Assert.True(result.IsFailure);
            Assert.Equal(Commands.Handlers.HoldDecisionHandler.ReasonModeDisallows, result.Reason);
        }

        [Fact]
        public void UiAvailabilityAndCommandValidationAgree()
        {
            // §19, invariant 57: one authority, consulted by both the projection and the handler.
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();

            var projector = new DecisionProjector(fixture.Catalog.Interventions);
            DecisionView before = projector.Project(fixture.Host.World, decision);
            InfluenceView ambitionView = FindInfluenceView(before, decision.Influences[0].Id.Value);

            Assert.True(ambitionView.CanBeIntervenedOn);
            Assert.True(fixture.Host.Session.Execute(
                new ApplyDecisionInterventionCommand(decision.Id, TestWorld.InterventionStepUp, decision.Influences[0].Id)).IsSuccess);

            // Spent: the projection must now agree that the control should be disabled.
            DecisionView after = projector.Project(fixture.Host.World, decision);
            Assert.False(FindInfluenceView(after, decision.Influences[0].Id.Value).CanBeIntervenedOn);

            Result second = fixture.Host.Session.Execute(
                new ApplyDecisionInterventionCommand(decision.Id, TestWorld.InterventionStepUp, decision.Influences[0].Id));
            Assert.True(second.IsFailure);
        }

        [Fact]
        public void DecisionHistoryFeedExplainsAppearanceInterventionAndResolutionNewestFirst()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();
            fixture.Host.World.Publish(new DecisionCreatedEvent(decision.Id, decision.CharacterId, decision.DefinitionId));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Result intervention = fixture.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id,
                TestWorld.InterventionStepUp,
                decision.Influences[0].Id));
            Assert.True(intervention.IsSuccess);

            fixture.Host.Session.Advance(SimDuration.FromHours(8));

            DecisionHistoryView feed = new DecisionHistoryProjector().Project(fixture.Host.World, 3);
            Assert.Equal(3, feed.Entries.Count);
            Assert.Contains("resolved", feed.Entries[0].Message);
            Assert.Contains("You influenced Mina Cairn", feed.Entries[1].Message);
            Assert.Contains("Mina Cairn faces", feed.Entries[2].Message);
        }

        [Fact]
        public void DecisionHistoryFeedIsBoundedAndIgnoresUnrelatedHistory()
        {
            TestWorld fixture = TestWorld.Create();
            fixture.Host.World.HistoryLedger.Record(
                new AuthoredId("history.unrelated"),
                fixture.Host.World.Clock.Now,
                Domain.History.RetentionTier.Recent,
                "not a decision");

            for (int i = 0; i < 7; i++)
            {
                Decision decision = fixture.CreateDecision();
                fixture.Host.World.Publish(new DecisionCreatedEvent(decision.Id, decision.CharacterId, decision.DefinitionId));
            }
            fixture.Host.Session.Advance(SimDuration.Zero);

            DecisionHistoryView feed = new DecisionHistoryProjector().Project(fixture.Host.World, 5);
            Assert.Equal(5, feed.Entries.Count);
            Assert.All(feed.Entries, entry => Assert.DoesNotContain("not a decision", entry.Message));
            Assert.True(feed.Entries[0].HistoryEntryId > feed.Entries[4].HistoryEntryId);
        }

        [Fact]
        public void HiddenInfluencesAreNotShownAndTheirCountIsNotExposed()
        {
            // §26: the number of hidden influences is not inherently exposed either.
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();

            DecisionView view = new DecisionProjector(fixture.Catalog.Interventions).Project(fixture.Host.World, decision);

            int visible = 0;
            for (int i = 0; i < view.Options.Count; i++)
            {
                visible += view.Options[i].Influences.Count;
            }

            Assert.Equal(4, decision.Influences.Count);
            Assert.Equal(3, visible);

            DecisionOptionView stay = FindOption(view, TestWorld.OptionStay.Value);
            Assert.Single(stay.Influences);
        }

        [Fact]
        public void GeneralizedInfluenceShowsCategoryAndDieButNotItsLabel()
        {
            TestWorld fixture = TestWorld.Create();
            Decision decision = fixture.CreateDecision();

            DecisionView view = new DecisionProjector().Project(fixture.Host.World, decision);
            InfluenceView ambition = FindInfluenceView(view, decision.Influences[0].Id.Value);

            // "Personal concern d10" — magnitude visible, specific reason not (§2.3, §26).
            Assert.Null(ambition.Label);
            Assert.Equal("cat.personal", ambition.Category);
            Assert.Equal(10, ambition.DieSides);
        }

        [Fact]
        public void TwoPlayersWithDifferentKnowledgeSeeDifferentViewsOfTheSameDecision()
        {
            // §56: presentation separation, demonstrated rather than asserted.
            TestWorld knows = TestWorld.Create();
            TestWorld doesNot = TestWorld.Create();

            Decision knownDecision = knows.CreateDecision();
            Decision unknownDecision = doesNot.CreateDecision();

            // Only the first player inspects Mina and learns she is ambitious.
            knows.Host.Session.Execute(new InspectCharacterCommand(knows.Mina));

            var projector = new DecisionProjector();
            InfluenceView withKnowledge = FindInfluenceView(
                projector.Project(knows.Host.World, knownDecision), knownDecision.Influences[0].Id.Value);
            InfluenceView withoutKnowledge = FindInfluenceView(
                projector.Project(doesNot.Host.World, unknownDecision), unknownDecision.Influences[0].Id.Value);

            Assert.Equal(TestWorld.TraitAmbitious.Value, withKnowledge.Label);
            Assert.Null(withoutKnowledge.Label);

            // Same truth underneath, both times.
            Assert.Equal(withKnowledge.DieSides, withoutKnowledge.DieSides);
        }

        [Fact]
        public void ObservationCreatesKnowledgeAndTruthCanThenDriftFromIt()
        {
            // §22: knowledge records an observation of truth and may become stale. That is intended.
            TestWorld fixture = TestWorld.Create();
            fixture.Host.Session.Execute(new InspectCharacterCommand(fixture.Mina));

            var needKey = new FactKey(FactKinds.CharacterNeed, fixture.Mina.ToRef(), TestWorld.NeedHunger);
            Assert.True(fixture.Host.World.Knowledge.TryGet(needKey, out KnowledgeEntry observed));

            long observedValue = observed.ObservedValue.Magnitude ?? 0;

            fixture.Host.Session.Advance(SimDuration.FromHours(4));

            long truthNow = fixture.Host.World.Characters.Get(fixture.Mina).Needs[TestWorld.NeedHunger].ValueAt(fixture.Host.World.Clock.Now);

            // The ledger still holds the old observation; truth has moved on.
            Assert.True(truthNow > observedValue);
            Assert.True(fixture.Host.World.Knowledge.TryGet(needKey, out KnowledgeEntry stillRecorded));
            Assert.Equal(observedValue, stillRecorded.ObservedValue.Magnitude ?? 0);
        }

        [Fact]
        public void PlayerProvidedActivityResultEntersThroughAValidatedCommand()
        {
            // §29.6: the mini-game never mutates Domain state; it submits a normalized result.
            TestWorld fixture = TestWorld.Create();

            ActivityInstance work = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, fixture.Mina, TestWorld.ActivityWorking, fixture.Home, SimDuration.FromHours(6));

            Result accepted = fixture.Host.Session.Execute(new SubmitActivityPerformanceCommand(
                work.Id, ActivityPerformanceResult.FromPlayer(PerformanceGrade.Excellent, 42)));

            Assert.True(accepted.IsSuccess);
            Assert.True(work.AcceptedResult.HasValue);
            Assert.Equal(OutcomeSource.PlayerProvided, work.AcceptedResult.Value.Source);
            Assert.Equal(PerformanceGrade.Excellent, work.AcceptedResult.Value.Grade);
        }

        [Fact]
        public void ResultsClaimingToBeAutomaticAreRejectedFromTheCommandPath()
        {
            // Diagnostics must be able to tell a human-played outcome from an RNG one (§53).
            TestWorld fixture = TestWorld.Create();

            ActivityInstance work = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, fixture.Mina, TestWorld.ActivityWorking, fixture.Home, SimDuration.FromHours(6));

            Result rejected = fixture.Host.Session.Execute(new SubmitActivityPerformanceCommand(
                work.Id, ActivityPerformanceResult.Automatic(PerformanceGrade.Excellent, 42)));

            Assert.True(rejected.IsFailure);
            Assert.Equal(
                Commands.Handlers.SubmitActivityPerformanceHandler.ReasonNotPlayerProvided,
                rejected.Reason);
        }

        [Fact]
        public void ActivitiesResolveAutomaticallyWithoutAnyPlayerInput()
        {
            // The invariant that makes ten thousand characters possible (§29.6, invariant 45).
            TestWorld fixture = TestWorld.Create();

            ActivityInstance work = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, fixture.Mina, TestWorld.ActivityWorking, fixture.Home, SimDuration.FromHours(6));

            fixture.Host.Session.Advance(SimDuration.FromHours(7));

            Assert.Equal(ActivityStatus.Completed, work.Status);
            Assert.True(work.AcceptedResult.HasValue);
            Assert.Equal(OutcomeSource.Automatic, work.AcceptedResult.Value.Source);
        }

        [Fact]
        public void BuildLocationReturnsANewDeterministicId()
        {
            TestWorld fixture = TestWorld.Create();

            Result<LocationId> built = fixture.Host.Session.Execute(
                new BuildLocationCommand(fixture.Town, TestWorld.KindBuilding, "New bakery"));

            Assert.True(built.IsSuccess);
            Assert.True(fixture.Host.World.Locations.TryGet(built.Value, out Domain.Spatial.LocationNode node));
            Assert.Equal("New bakery", node.DisplayName);
        }

        [Fact]
        public void UnknownParentIsRefusedRatherThanCorruptingTheHierarchy()
        {
            TestWorld fixture = TestWorld.Create();

            Result<LocationId> built = fixture.Host.Session.Execute(
                new BuildLocationCommand(new LocationId(9999), TestWorld.KindBuilding, "Nowhere"));

            Assert.True(built.IsFailure);
            Assert.Equal(Commands.Handlers.BuildLocationHandler.ReasonUnknownParent, built.Reason);
        }

        [Fact]
        public void AttentionPolicyIsGameplayStateNotPresentationState()
        {
            TestWorld fixture = TestWorld.Create();

            fixture.Host.Session.Execute(new SetAttentionPolicyCommand(fixture.Mina, AttentionPolicy.Watch));

            Assert.Equal(AttentionPolicy.Watch, fixture.Host.World.Attention.PolicyFor(fixture.Mina));
        }

        private static InfluenceView FindInfluenceView(DecisionView view, int influenceId)
        {
            for (int o = 0; o < view.Options.Count; o++)
            {
                for (int i = 0; i < view.Options[o].Influences.Count; i++)
                {
                    if (view.Options[o].Influences[i].InfluenceId == influenceId)
                    {
                        return view.Options[o].Influences[i];
                    }
                }
            }

            Assert.Fail($"Influence {influenceId} is not visible in this projection.");
            return null;
        }

        private static DecisionOptionView FindOption(DecisionView view, string optionId)
        {
            for (int i = 0; i < view.Options.Count; i++)
            {
                if (view.Options[i].OptionId == optionId)
                {
                    return view.Options[i];
                }
            }

            Assert.Fail($"Option {optionId} is missing from the projection.");
            return null;
        }
    }
}
