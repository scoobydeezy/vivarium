using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class WorkContextTests
    {
        [Fact]
        public void DislikedColleaguePressureIsIntervalAccurateAndReevaluatesLivingDecision()
        {
            TestWorld fixture = TestWorld.Create();
            var darius = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Darius", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(darius.Id, darius);
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, darius.Id, WellKnownActivities.Waiting, fixture.Bakery, SimDuration.FromHours(12));

            var relationship = new Relationship(
                fixture.Host.World.RuntimeIds.Relationships.Next(),
                fixture.Mina,
                darius.Id,
                new AuthoredId("relationship.disliked_boss"),
                AnalyticalProgression.Constant(-5000, fixture.Host.World.Clock.Now),
                fixture.Host.World.Clock.Now);
            fixture.Host.World.Relationships.Add(relationship.Id, relationship);
            fixture.Host.World.RelationshipIndex.Register(relationship);

            fixture.Host.DecisionReevaluation.Register(new ActivityContextInfluenceReevaluator(
                TestWorld.DecisionLeaveWork,
                TestWorld.ContextWorkPressure,
                TestWorld.ModifierDislikedColleague,
                TestWorld.InfluenceBadWorkContext,
                Die.D10,
                Die.D6));
            var pressure = new WorkContextPressureService(
                fixture.Host.Transitions,
                fixture.Host.DecisionReevaluation,
                TestWorld.ActivityWorking,
                TestWorld.ModifierDislikedColleague,
                TestWorld.ContextWorkPressure,
                -1000,
                pressuredRate: 2);
            fixture.Host.DomainEventHandlers.Register(new WorkContextArrivalHandler(pressure), 200);
            fixture.Host.DomainEventHandlers.Register(new WorkContextDepartureHandler(pressure), 100);

            ActivityInstance work = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                fixture.Mina,
                TestWorld.ActivityWorking,
                fixture.Bakery,
                SimDuration.FromHours(12),
                performanceRatePerMinute: 10);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.True(work.HasModifier(TestWorld.ModifierDislikedColleague));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(500));
            Decision decision = fixture.Host.World.Decisions.Get(new DecisionId(1));
            DecisionInfluence pressureInfluence = FindInfluence(decision, TestWorld.InfluenceBadWorkContext);
            DecisionInfluenceId stableId = pressureInfluence.Id;
            Assert.Equal(Die.D10, pressureInfluence.CurrentDie);

            InfluenceView beforeDiscovery = FindInfluenceView(new DecisionProjector().Project(fixture.Host.World, decision), stableId);
            Assert.Null(beforeDiscovery.Label);
            Assert.Equal("cat.social", beforeDiscovery.Category);

            fixture.Host.Session.Execute(new BeginObservingCharacterCommand(fixture.Mina));
            var fact = new FactKey(FactKinds.DecisionInfluence, fixture.Mina.ToRef(), TestWorld.InfluenceBadWorkContext);
            Assert.True(fixture.Host.World.Knowledge.Knows(fact));
            InfluenceView afterDiscovery = FindInfluenceView(new DecisionProjector().Project(fixture.Host.World, decision), stableId);
            Assert.Equal(TestWorld.InfluenceBadWorkContext.Value, afterDiscovery.Label);
            Assert.True(fixture.Host.Session.Execute(new HoldDecisionCommand(decision.Id)).IsSuccess);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, darius.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.False(work.HasModifier(TestWorld.ModifierDislikedColleague));
            Assert.Equal(stableId, pressureInfluence.Id);
            Assert.Equal(Die.D6, pressureInfluence.CurrentDie);
            Assert.Equal(3, decision.InfluenceRevision);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(20));
            Assert.Equal((500 * 2) + (20 * 10), work.Performance.ValueAt(fixture.Host.World.Clock.Now));

            WorldState restored = fixture.Host.SaveMapper.Restore(fixture.Host.Session.Save("work-context"));
            Decision restoredDecision = restored.Decisions.Get(decision.Id);
            Assert.True(restoredDecision.TryGetInfluence(stableId, out DecisionInfluence restoredInfluence));
            Assert.Equal(Die.D6, restoredInfluence.CurrentDie);
        }

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

        private static InfluenceView FindInfluenceView(DecisionView decision, DecisionInfluenceId influenceId)
        {
            for (int option = 0; option < decision.Options.Count; option++)
            {
                for (int influence = 0; influence < decision.Options[option].Influences.Count; influence++)
                {
                    InfluenceView view = decision.Options[option].Influences[influence];
                    if (view.InfluenceId == influenceId.Value)
                    {
                        return view;
                    }
                }
            }

            return null;
        }
    }
}
