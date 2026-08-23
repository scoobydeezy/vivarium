using Vivarium.Application.Commands;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Domain.Social;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class InteractionTests
    {
        [Fact]
        public void SharedLocationInteractionIsSubordinateAndObservable()
        {
            TestWorld fixture = TestWorld.Create();
            Character mina = fixture.Host.World.Characters.Get(fixture.Mina);
            ActivityInstanceId minasActivity = mina.CurrentActivityId;

            var glen = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Glen", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(glen.Id, glen);
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, glen.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));

            fixture.Host.Session.Execute(new FollowCharacterCommand(fixture.Mina, true));
            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(minasActivity, mina.CurrentActivityId);
            Assert.True(fixture.Host.World.TryGetCurrentActivity(glen.Id, out ActivityInstance glensActivity));
            Assert.Equal(WellKnownActivities.Waiting, glensActivity.DefinitionId);
            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(fixture.Mina, glen.Id, out RelationshipId relationshipId));

            Relationship relationship = fixture.Host.World.Relationships.Get(relationshipId);
            Assert.Equal(100, relationship.From(fixture.Mina).ChannelAt(RelationshipChannels.Affection, fixture.Host.World.Clock.Now));
            Assert.Equal(250, relationship.From(fixture.Mina).FamiliarityAt(fixture.Host.World.Clock.Now));
            Assert.Equal(100, relationship.From(glen.Id).ChannelAt(RelationshipChannels.Affection, fixture.Host.World.Clock.Now));
            Assert.Equal(250, relationship.From(glen.Id).FamiliarityAt(fixture.Host.World.Clock.Now));
            Assert.Equal(fixture.Host.World.Clock.Now, relationship.LastInteractionAt);
            Assert.Equal(1, fixture.Host.World.Attention.ObservationOrdinal(fixture.Mina));

            var traitFact = new FactKey(FactKinds.CharacterTrait, fixture.Mina.ToRef(), TestWorld.TraitAmbitious);
            Assert.True(fixture.Host.World.Knowledge.TryGet(traitFact, out KnowledgeEntry _));
            Assert.True(fixture.Host.World.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(fixture.Mina), glen.Id, out BeliefDistribution minasBelief));
            Assert.True(minasBelief.Mean[SocialDimensions.Warmth] > 0);
            Assert.True(fixture.Host.World.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(glen.Id), fixture.Mina, out BeliefDistribution glensBelief));
            Assert.True(glensBelief.Mean[SocialDimensions.Warmth] > 0);

            WorldState restored = fixture.Host.SaveMapper.Restore(fixture.Host.Session.Save("interaction"));
            Assert.True(restored.RelationshipIndex.TryGetBetween(fixture.Mina, glen.Id, out RelationshipId restoredId));
            Relationship restoredRelationship = restored.Relationships.Get(restoredId);
            Assert.Equal(
                relationship.From(fixture.Mina).ChannelAt(RelationshipChannels.Affection, fixture.Host.World.Clock.Now),
                restoredRelationship.From(fixture.Mina).ChannelAt(RelationshipChannels.Affection, restored.Clock.Now));
            Assert.Equal(
                relationship.From(glen.Id).FamiliarityAt(fixture.Host.World.Clock.Now),
                restoredRelationship.From(glen.Id).FamiliarityAt(restored.Clock.Now));
            Assert.Equal(relationship.LastInteractionAt, restoredRelationship.LastInteractionAt);
        }

        [Fact]
        public void UnwatchedInteractionDoesNotCreateObservationKnowledge()
        {
            TestWorld fixture = TestWorld.Create();
            var glen = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Glen", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(glen.Id, glen);
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, glen.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));

            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(fixture.Mina, glen.Id, out RelationshipId _));
            Assert.Equal(0, fixture.Host.World.Attention.ObservationOrdinal(fixture.Mina));
            Assert.Equal(0, fixture.Host.World.Knowledge.Count);
        }

        [Fact]
        public void SharedTravelSegmentInteractionKeepsBothTravelActivitiesAndRebuildsAfterLoad()
        {
            TestWorld fixture = TestWorld.Create();
            var glen = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Glen", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(glen.Id, glen);

            Assert.True(fixture.Host.Transitions.TryBeginTravel(
                fixture.Host.Simulation, fixture.Mina, fixture.Bakery, out ActivityInstance minaTravel));
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, glen.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));
            Assert.True(fixture.Host.Transitions.TryBeginTravel(
                fixture.Host.Simulation, glen.Id, fixture.Bakery, out ActivityInstance glenTravel));

            fixture.Host.Session.Advance(SimDuration.Zero);

            Assert.Equal(minaTravel.Id, fixture.Host.World.Characters.Get(fixture.Mina).CurrentActivityId);
            Assert.Equal(glenTravel.Id, fixture.Host.World.Characters.Get(glen.Id).CurrentActivityId);
            Assert.True(minaTravel.SpatialContext.IsTraveling);
            Assert.True(glenTravel.SpatialContext.IsTraveling);
            Assert.True(fixture.Host.World.RelationshipIndex.TryGetBetween(fixture.Mina, glen.Id, out RelationshipId _));

            WorldState restored = fixture.Host.SaveMapper.Restore(fixture.Host.Session.Save("shared-travel"));
            var segment = new Vivarium.Domain.Spatial.TravelSegmentKey(fixture.Home, fixture.Bakery);
            Assert.Contains(fixture.Mina, restored.Spatial.TravelersOn(segment));
            Assert.Contains(glen.Id, restored.Spatial.TravelersOn(segment));
            Assert.True(restored.RelationshipIndex.TryGetBetween(fixture.Mina, glen.Id, out RelationshipId _));
        }
    }
}
