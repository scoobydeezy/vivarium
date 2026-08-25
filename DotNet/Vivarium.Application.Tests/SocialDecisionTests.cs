using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class SocialDecisionTests
    {
        [Fact]
        public void InteractionCreatesLivingSocialDecisionAndDirectionalConsequenceSurvivesReload()
        {
            TestWorld fixture = TestWorld.Create(includeSocialDecision: true);
            Character mina = fixture.Host.World.Characters.Get(fixture.Mina);
            var darius = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Darius", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(darius.Id, darius);
            mina.SetAppraisalField(new AppraisalField(
                mina.Id,
                AppraisalLenses.Affiliation,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Warmth, 10000) },
                null,
                null,
                null,
                null,
                new AuthoredId("social.calibration.standard")));
            var belief = SocialBeliefUpdateService.BroadPrior();
            belief.Mean.Set(SocialDimensions.Warmth, 9000);
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                belief.SetCovariance(SocialDimensions.Provisional[i], SocialDimensions.Provisional[i], 0);
            }
            fixture.Host.World.Knowledge.SetSocialBelief(
                ObserverRef.Character(mina.Id), darius.Id, belief, fixture.Host.World.Clock.Now);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                darius.Id,
                WellKnownActivities.Waiting,
                fixture.Home,
                SimDuration.FromHours(1));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(darius.Id, mina.Id, out RelationshipId _));
            Assert.Single(fixture.Host.World.Decisions.All);
            Decision decision = fixture.Host.World.Decisions.Get(new DecisionId(1));
            Assert.Equal(mina.Id, decision.CharacterId);
            Assert.Single(decision.Influences);
            Assert.Equal(new AuthoredId("option.seek_company"), decision.Influences[0].OptionId);
            Assert.Equal(System.Math.Abs(decision.Influences[0].Evaluation.ExpectedScore), decision.Importance);
            DecisionInfluenceId influenceId = decision.Influences[0].Id;

            fixture.Host.Simulation.World.Publish(new SocialBeliefChangedEvent(
                ObserverRef.Character(mina.Id), darius.Id, belief.EvidenceRevision));
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.Equal(influenceId, decision.Influences[0].Id);

            var saved = fixture.Host.Session.Save("social-decision");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                fixture.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                fixture.Store,
                fixture.Clock);
            Assert.Equal(decision.Importance, restored.World.Decisions.Get(decision.Id).Importance);
            restored.Session.Advance(SimDuration.FromMinutes(10));

            Decision restoredDecision = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(new AuthoredId("option.seek_company"), restoredDecision.Resolution.ChosenOptionId);
            Assert.True(restored.World.RelationshipIndex.TryGetBetween(darius.Id, mina.Id, out RelationshipId relationshipId));
            Relationship relationship = restored.World.Relationships.Get(relationshipId);
            Assert.True(relationship.From(mina.Id).ChannelAt(RelationshipChannels.Affection, restored.World.Clock.Now) >= 1100);
            Assert.True(relationship.From(darius.Id).ChannelAt(RelationshipChannels.Affection, restored.World.Clock.Now) <
                        relationship.From(mina.Id).ChannelAt(RelationshipChannels.Affection, restored.World.Clock.Now));
        }
    }
}
