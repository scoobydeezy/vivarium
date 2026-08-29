using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    /// <summary>
    /// Simulation invariant tests (§51): one primary Activity, occupancy agreeing with Activity state,
    /// travel excluded from direct occupancy, bounded held decisions, and interventions staying bound to
    /// stable influence identity.
    /// </summary>
    public sealed class SimulationInvariantTests
    {
        private static readonly AuthoredId Working = new AuthoredId("activity.working");
        private static readonly AuthoredId Waiting = WellKnownActivities.Waiting;
        private static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");

        private sealed class Fixture
        {
            public WorldState World;
            public SimulationContext Context;
            public ActivityTransitionService Transitions;
            public SettlementLoop Settlement;
            public SimulationRunner Runner;
            public LocationId Town;
            public LocationId Home;
            public LocationId Bakery;
            public CharacterId Mina;
        }

        private static Fixture Build()
        {
            var world = new WorldState(827119, SimTime.FromClockTime(0, 8, 0));
            var transitions = new ActivityTransitionService();

            var handlers = new ScheduledEventHandlerRegistry();
            handlers.Register(new TravelArrivalHandler(transitions));
            handlers.Register(new ActivityCompletionHandler(new ActivityResolutionRegistry(), transitions));

            var settlement = new SettlementLoop(handlers, new OrderedDomainEventHandlerRegistry());

            var fixture = new Fixture
            {
                World = world,
                Transitions = transitions,
                Settlement = settlement,
                Runner = new SimulationRunner(settlement),
                Context = new SimulationContext(world, new DeterministicRandomOracle(827119), SimulationMode.Live, 1, 1),
            };

            var townNode = new LocationNode(world.RuntimeIds.Locations.Next(), LocationId.None, Building, "Town");
            world.Locations.Add(townNode);
            fixture.Town = townNode.Id;

            var homeNode = new LocationNode(world.RuntimeIds.Locations.Next(), fixture.Town, Building, "Home");
            world.Locations.Add(homeNode);
            fixture.Home = homeNode.Id;

            var bakeryNode = new LocationNode(world.RuntimeIds.Locations.Next(), fixture.Town, Building, "Bakery");
            world.Locations.Add(bakeryNode);
            fixture.Bakery = bakeryNode.Id;

            world.TravelNetwork.ConnectBidirectional(fixture.Home, fixture.Bakery, SimDuration.FromMinutes(12), Walking);

            var mina = new Character(world.RuntimeIds.Characters.Next(), "Mina", world.Clock.Now);
            world.Characters.Add(mina.Id, mina);
            fixture.Mina = mina.Id;

            transitions.BeginActivity(fixture.Context, mina.Id, Waiting, fixture.Home, SimDuration.FromHours(1));
            return fixture;
        }

        [Fact]
        public void CharacterHasExactlyOneActivePrimaryActivity()
        {
            Fixture fixture = Build();

            ActivityInstance first = fixture.World.Activities.Get(fixture.World.Characters.Get(fixture.Mina).CurrentActivityId);
            ActivityInstance second = fixture.Transitions.BeginActivity(fixture.Context, fixture.Mina, Working, fixture.Bakery, SimDuration.FromHours(6));

            Assert.Equal(ActivityStatus.Abandoned, first.Status);
            Assert.Equal(ActivityStatus.Active, second.Status);
            Assert.Equal(second.Id, fixture.World.Characters.Get(fixture.Mina).CurrentActivityId);

            int active = 0;
            foreach (ActivityInstance activity in fixture.World.Activities.All)
            {
                if (activity.CharacterId == fixture.Mina && activity.Status == ActivityStatus.Active)
                {
                    active++;
                }
            }

            Assert.Equal(1, active);
        }

        [Fact]
        public void OccupancyIndexesAgreeWithActivitySpatialContext()
        {
            Fixture fixture = Build();

            Assert.Contains(fixture.Mina, fixture.World.Spatial.DirectOccupantsOf(fixture.Home));
            Assert.Equal(1, fixture.World.Spatial.CountWithin(fixture.Town));

            fixture.Transitions.BeginActivity(fixture.Context, fixture.Mina, Working, fixture.Bakery, SimDuration.FromHours(6));

            Assert.Empty(fixture.World.Spatial.DirectOccupantsOf(fixture.Home));
            Assert.Contains(fixture.Mina, fixture.World.Spatial.DirectOccupantsOf(fixture.Bakery));

            // Still one occupant of the town: the ancestor index moved with her, not double-counted.
            Assert.Equal(1, fixture.World.Spatial.CountWithin(fixture.Town));
        }

        [Fact]
        public void TravelingExcludesTheCharacterFromDirectOccupancy()
        {
            Fixture fixture = Build();

            Assert.True(fixture.Transitions.TryBeginTravel(fixture.Context, fixture.Mina, fixture.Bakery, out ActivityInstance travel));

            // Not simultaneously stationary at both ends (§29.2).
            Assert.Empty(fixture.World.Spatial.DirectOccupantsOf(fixture.Home));
            Assert.Empty(fixture.World.Spatial.DirectOccupantsOf(fixture.Bakery));
            Assert.Contains(fixture.Mina, fixture.World.Spatial.Travelers);
            Assert.True(travel.SpatialContext.IsTraveling);
            Assert.Equal(WellKnownActivities.Traveling, travel.DefinitionId);
        }

        [Fact]
        public void TravelProgressIsAnalyticalAndArrivalIsScheduled()
        {
            Fixture fixture = Build();
            fixture.Transitions.TryBeginTravel(fixture.Context, fixture.Mina, fixture.Bakery, out ActivityInstance travel);

            SimTime halfway = fixture.World.Clock.Now.Plus(SimDuration.FromMinutes(6));
            Assert.Equal(5000, travel.ProgressBasisPointsAt(halfway));

            // Arrival is a real scheduled event, not something a frame update notices.
            fixture.Runner.AdvanceUntil(travel.SpatialContext.Transit.ArrivesAt, fixture.Context);

            Assert.DoesNotContain(fixture.Mina, fixture.World.Spatial.Travelers);
            Assert.Contains(fixture.Mina, fixture.World.Spatial.DirectOccupantsOf(fixture.Bakery));
        }

        [Fact]
        public void StartingANewActivityInvalidatesThePreviousCompletionEvent()
        {
            // The revision bump on transition is what retires the outgoing Activity's queued completion.
            Fixture fixture = Build();
            ActivityInstance first = fixture.World.Activities.Get(fixture.World.Characters.Get(fixture.Mina).CurrentActivityId);
            ScheduledEventId firstCompletion = first.PendingCompletionEventId;

            fixture.Transitions.BeginActivity(fixture.Context, fixture.Mina, Working, fixture.Home, SimDuration.FromHours(6));

            Assert.False(fixture.World.Scheduler.Contains(firstCompletion));
        }

        [Fact]
        public void ContextChangeAffectsOnlyTheIntervalItAppliedFor()
        {
            // §29.7, the brief's own example: the hated boss is present 11:20–11:40.
            Fixture fixture = Build();
            ActivityInstance work = fixture.Transitions.BeginActivity(
                fixture.Context, fixture.Mina, Working, fixture.Bakery, SimDuration.FromHours(6), performanceRatePerMinute: 10);

            fixture.World.Clock.AdvanceTo(SimTime.FromClockTime(0, 11, 20));
            fixture.Transitions.ApplyContextModifier(fixture.Context, work, new ActivityContextModifier(
                new AuthoredId("activity_modifier.disliked_colleague_present"), fixture.World.Clock.Now, 2, 1));

            fixture.World.Clock.AdvanceTo(SimTime.FromClockTime(0, 11, 40));
            fixture.Transitions.RemoveContextModifier(
                fixture.Context, work, new AuthoredId("activity_modifier.disliked_colleague_present"), 10);

            fixture.World.Clock.AdvanceTo(SimTime.FromClockTime(0, 14, 0));

            // 200 minutes at 10, then 20 at 2, then 140 at 10 — not 360 minutes at whichever rate
            // happened to be in force at the end.
            Assert.Equal((200 * 10) + (20 * 2) + (140 * 10), work.Performance.ValueAt(fixture.World.Clock.Now));
            Assert.Empty(work.ActiveModifiers);
        }

        [Fact]
        public void AncestorTraversalWalksTheContainmentHierarchy()
        {
            Fixture fixture = Build();

            IReadOnlyList<LocationId> ancestors = fixture.World.Locations.AncestorsOf(fixture.Bakery);

            Assert.Equal(new[] { fixture.Town }, ancestors);
            Assert.True(fixture.World.Locations.IsDescendantOf(fixture.Bakery, fixture.Town));
            Assert.False(fixture.World.Locations.IsDescendantOf(fixture.Town, fixture.Bakery));
        }

        [Fact]
        public void HoldOverflowVictimIsChosenDeterministically()
        {
            var policy = new DecisionHoldPolicy(maxGlobalHeld: 2, maxHeldPerCharacter: 2);
            SimTime now = SimTime.Epoch;

            Decision important = MakeDecision(5, now, importance: 50);
            Decision trivialOld = MakeDecision(6, now, importance: 1);
            Decision trivialNew = MakeDecision(7, now.Plus(SimDuration.FromHours(1)), importance: 1);

            Decision victim = policy.SelectOverflowVictim(new[] { important, trivialNew, trivialOld });

            // Lowest importance, then oldest creation, then lowest id.
            Assert.Equal(trivialOld.Id, victim.Id);
        }

        [Fact]
        public void InterventionsStayBoundToStableInfluenceIdentity()
        {
            // §17.2: the influence set may change while the decision is open, and an already-applied
            // intervention must not silently retarget.
            Decision decision = MakeDecision(1837, SimTime.Epoch, 10);
            DecisionInfluence ambition = decision.AddInfluence(
                new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.ambition"), Die.D10, InfluenceVisibility.Full);

            var stepUp = new InterventionDefinition(new AuthoredId("intervention.encourage"), InterventionKind.StepDieUp, 1);
            Assert.True(DecisionInterventionRules.Evaluate(decision, stepUp, ambition.Id, new NudgeAccount(), new DecisionInterventionResources()).IsSuccess);
            DecisionInterventionRules.Apply(decision, stepUp, ambition.Id, commandSequence: 501);

            Assert.Equal(Die.D12, ambition.CurrentDie);

            // The world adds two more influences and retracts one; the intervention still points at
            // exactly the die it was spent on.
            decision.AddInfluence(new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.pay"), Die.D6, InfluenceVisibility.Full);
            DecisionInfluence extra = decision.AddInfluence(new AuthoredId("option.stay"), new AuthoredId("cat"), new AuthoredId("influence.commute"), Die.D6, InfluenceVisibility.Full);
            decision.RetractInfluence(extra.Id);

            Assert.Equal(ambition.Id, decision.Interventions[0].TargetInfluenceId);
            Assert.Equal(Die.D12, ambition.CurrentDie);
            Assert.Equal(501, decision.Interventions[0].CommandSequence);
        }

        [Fact]
        public void InterventionChangesOpposingDieMagnitudeWithoutChangingPolarity()
        {
            Decision decision = MakeDecision(1, SimTime.Epoch, 0);
            DecisionInfluence concern = decision.AddInfluence(
                new AuthoredId("option.accept"),
                new AuthoredId("cat.risk"),
                new AuthoredId("influence.risk"),
                Die.D6,
                InfluenceVisibility.Full,
                polarity: InfluencePolarity.Opposing);
            var stepUp = new InterventionDefinition(new AuthoredId("intervention.intensify"), InterventionKind.StepDieUp, 1);

            DecisionInterventionRules.Apply(decision, stepUp, concern.Id, 10);

            Assert.Equal(InfluencePolarity.Opposing, concern.Polarity);
            Assert.Equal(Die.D8, concern.CurrentDie);
        }

        [Fact]
        public void TheSameInterventionCannotBeSpentTwiceOnOneInfluence()
        {
            Decision decision = MakeDecision(1, SimTime.Epoch, 0);
            DecisionInfluence influence = decision.AddInfluence(
                new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.a"), Die.D6, InfluenceVisibility.Full);

            var stepUp = new InterventionDefinition(new AuthoredId("intervention.encourage"), InterventionKind.StepDieUp, 1);
            DecisionInterventionRules.Apply(decision, stepUp, influence.Id, 1);

            Result second = DecisionInterventionRules.Evaluate(decision, stepUp, influence.Id, new NudgeAccount(), new DecisionInterventionResources());

            Assert.True(second.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonAlreadyApplied, second.Reason);
        }

        [Fact]
        public void InterventionIsRefusedWhenTheDieIsAlreadyAtTheTopOfTheLadder()
        {
            Decision decision = MakeDecision(1, SimTime.Epoch, 0);
            DecisionInfluence influence = decision.AddInfluence(
                new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.a"), Die.D20, InfluenceVisibility.Full);

            var stepUp = new InterventionDefinition(new AuthoredId("intervention.encourage"), InterventionKind.StepDieUp, 1);
            Result result = DecisionInterventionRules.Evaluate(decision, stepUp, influence.Id, new NudgeAccount(), new DecisionInterventionResources());

            Assert.True(result.IsFailure);
            Assert.Equal(DecisionInterventionRules.ReasonDieAtLadderTop, result.Reason);
        }

        [Fact]
        public void ResolutionIsDeterministicAndRetainsEveryRoll()
        {
            var world = new WorldState(827119, SimTime.FromClockTime(0, 10, 0));
            var context = new SimulationContext(world, new DeterministicRandomOracle(827119), SimulationMode.Live, 1, 1);

            Decision first = MakeDecision(1837, world.Clock.Now, 10);
            AddJobOfferInfluences(first);

            Decision second = MakeDecision(1837, world.Clock.Now, 10);
            AddJobOfferInfluences(second);

            var service = new DecisionResolutionService();
            DecisionResolution a = service.Resolve(first, context);
            DecisionResolution b = service.Resolve(second, context);

            Assert.Equal(a.ChosenOptionId, b.ChosenOptionId);
            Assert.Equal(a.Degree, b.Degree);
            Assert.Equal(a.Rolls.Count, b.Rolls.Count);
            Assert.Equal(4, a.Rolls.Count);

            for (int i = 0; i < a.Rolls.Count; i++)
            {
                Assert.Equal(a.Rolls[i].Rolled, b.Rolls[i].Rolled);
                Assert.InRange(a.Rolls[i].Rolled, 1, a.Rolls[i].Die.Sides);
            }
        }

        [Fact]
        public void SignedOptionRelativePolicySubtractsOpposingRolls()
        {
            var world = new WorldState(77, SimTime.Epoch);
            var context = new SimulationContext(world, new DeterministicRandomOracle(77), SimulationMode.Live, 1, 1);
            Decision decision = MakeDecision(9, SimTime.Epoch, 0);
            DecisionInfluence support = decision.AddInfluence(
                new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("support"),
                Die.D8, InfluenceVisibility.Full);
            DecisionInfluence opposition = decision.AddInfluence(
                new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("opposition"),
                Die.D6, InfluenceVisibility.Full, polarity: InfluencePolarity.Opposing);

            DecisionResolution resolution = new DecisionResolutionService().Resolve(decision, context);

            int supportRoll = 0;
            int opposingRoll = 0;
            for (int i = 0; i < resolution.Rolls.Count; i++)
            {
                if (resolution.Rolls[i].InfluenceId == support.Id) supportRoll = resolution.Rolls[i].Rolled;
                if (resolution.Rolls[i].InfluenceId == opposition.Id) opposingRoll = resolution.Rolls[i].Rolled;
            }
            OptionTotal accept = resolution.OptionTotals[0];
            Assert.Equal(supportRoll - opposingRoll, accept.Total);
            Assert.Equal(InfluencePolarity.Opposing, resolution.Rolls[1].Polarity);
        }

        [Fact]
        public void RetractedInfluencesDoNotRoll()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var context = new SimulationContext(world, new DeterministicRandomOracle(1), SimulationMode.Live, 1, 1);

            Decision decision = MakeDecision(1, SimTime.Epoch, 0);
            AddJobOfferInfluences(decision);
            decision.RetractInfluence(decision.Influences[0].Id);

            DecisionResolution resolution = new DecisionResolutionService().Resolve(decision, context);

            Assert.Equal(3, resolution.Rolls.Count);
        }

        [Fact]
        public void InteractionCandidatesStayBoundedInHugeSharedContexts()
        {
            // §32: a concert is a candidate pool, not a licence for an O(k²) scan.
            var selector = new InteractionCandidateSelector(new DeterministicRandomOracle(42));
            var crowd = new List<CharacterId>();

            for (int i = 1; i <= 5000; i++)
            {
                crowd.Add(new CharacterId(i));
            }

            IReadOnlyList<CharacterId> candidates = selector.Select(
                new CharacterId(1),
                crowd,
                new RelationshipIndex(),
                maxCandidates: 6,
                scopeType: RandomScopeTypes.Location,
                scopeId: 12,
                rollIndex: 0);

            Assert.True(candidates.Count <= 6);
            Assert.DoesNotContain(new CharacterId(1), candidates);

            // And it is reproducible.
            IReadOnlyList<CharacterId> again = selector.Select(
                new CharacterId(1), crowd, new RelationshipIndex(), 6, RandomScopeTypes.Location, 12, 0);

            Assert.Equal(candidates, again);
        }

        [Fact]
        public void AcquaintancesArePreferredOverStrangers()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var index = new RelationshipIndex();

            var relationship = new Relationship(
                new RelationshipId(1),
                new CharacterId(1),
                new CharacterId(500),
                new AuthoredId("relationship.friend"),
                AnalyticalProgression.Constant(0, SimTime.Epoch),
                SimTime.Epoch);

            index.Register(relationship);

            var crowd = new List<CharacterId>();
            for (int i = 1; i <= 1000; i++)
            {
                crowd.Add(new CharacterId(i));
            }

            var selector = new InteractionCandidateSelector(new DeterministicRandomOracle(7));
            IReadOnlyList<CharacterId> candidates = selector.Select(
                new CharacterId(1), crowd, index, 3, RandomScopeTypes.Location, 1, 0);

            Assert.Equal(new CharacterId(500), candidates[0]);
        }

        [Fact]
        public void IndexedSharedContextSelectionMatchesTheGeneralDeterministicPath()
        {
            var index = new RelationshipIndex();
            var relationship = new Relationship(
                new RelationshipId(1),
                new CharacterId(1),
                new CharacterId(500),
                new AuthoredId("relationship.friend"),
                AnalyticalProgression.Constant(0, SimTime.Epoch),
                SimTime.Epoch);
            index.Register(relationship);

            var list = new List<CharacterId>();
            for (int i = 1; i <= 1000; i++) list.Add(new CharacterId(i));
            var sorted = new SortedSet<CharacterId>(list);
            var selector = new InteractionCandidateSelector(new DeterministicRandomOracle(7));

            IReadOnlyList<CharacterId> general = selector.Select(
                new CharacterId(1), list, index, 6, RandomScopeTypes.Location, 1, 3);
            IReadOnlyList<CharacterId> indexed = selector.Select(
                new CharacterId(1), sorted, index, 6, RandomScopeTypes.Location, 1, 3);

            Assert.Equal(general, indexed);

            list.Remove(new CharacterId(17));
            list.Remove(new CharacterId(123));
            list.Remove(new CharacterId(900));
            sorted = new SortedSet<CharacterId>(list);
            general = selector.Select(
                new CharacterId(1), list, index, 6, RandomScopeTypes.Location, 1, 3);
            indexed = selector.Select(
                new CharacterId(1), sorted, index, 6, RandomScopeTypes.Location, 1, 3);

            Assert.Equal(general, indexed);
        }

        [Fact]
        public void OneArrivalInALargeSharedContextProducesAtMostOneInteraction()
        {
            Fixture fixture = Build();
            const int crowdSize = 2000;

            for (int i = 0; i < crowdSize; i++)
            {
                var character = new Character(
                    fixture.World.RuntimeIds.Characters.Next(),
                    "Crowd " + i,
                    fixture.World.Clock.Now);
                fixture.World.Characters.Add(character.Id, character);
                fixture.Transitions.BeginActivity(
                    fixture.Context,
                    character.Id,
                    Waiting,
                    fixture.Home,
                    SimDuration.FromHours(1));
            }

            // Setup arrivals are irrelevant to this single-opportunity scale check.
            fixture.World.DomainEvents.Clear();
            int activitiesBefore = fixture.World.Activities.Count;
            var discovery = new KnowledgeDiscoveryService();
            var service = new InteractionService(
                new InteractionCandidateSelector(new DeterministicRandomOracle(827119)),
                discovery);

            Assert.True(service.TryInteractOnArrival(fixture.Context, fixture.Mina, fixture.Home));
            Assert.Equal(1, fixture.World.Relationships.Count);
            Assert.Equal(activitiesBefore, fixture.World.Activities.Count);
        }

        private static Decision MakeDecision(int id, SimTime createdAt, int importance) => new Decision(
            new DecisionId(id),
            new CharacterId(1),
            new AuthoredId("decision.job_offer"),
            createdAt,
            createdAt.Plus(SimDuration.FromHours(8)),
            new[]
            {
                new DecisionOption(new AuthoredId("option.accept"), new AuthoredId("label.accept"), 0),
                new DecisionOption(new AuthoredId("option.stay"), new AuthoredId("label.stay"), 1),
            },
            new DecisionConflictScope(new AuthoredId("conflict_scope.employment"), new CharacterId(1).ToRef()),
            importance);

        private static void AddJobOfferInfluences(Decision decision)
        {
            decision.AddInfluence(new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.ambition"), Die.D10, InfluenceVisibility.Full);
            decision.AddInfluence(new AuthoredId("option.accept"), new AuthoredId("cat"), new AuthoredId("influence.baking"), Die.D8, InfluenceVisibility.Full);
            decision.AddInfluence(new AuthoredId("option.stay"), new AuthoredId("cat"), new AuthoredId("influence.family"), Die.D8, InfluenceVisibility.Full);
            decision.AddInfluence(new AuthoredId("option.stay"), new AuthoredId("cat"), new AuthoredId("influence.friendship"), Die.D6, InfluenceVisibility.Full);
        }
    }
}
