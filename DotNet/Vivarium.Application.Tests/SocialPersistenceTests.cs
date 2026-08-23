using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Xunit;
using Vivarium.Application.Persistence;
using Vivarium.Infrastructure.Persistence;

namespace Vivarium.Application.Tests
{
    public sealed class SocialPersistenceTests
    {
        [Fact]
        public void DirectionalSocialTruthBeliefFieldsAndHistoryRoundTrip()
        {
            TestWorld fixture = TestWorld.Create();
            Character mina = fixture.Host.World.Characters.Get(fixture.Mina);
            var darius = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Darius", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(darius.Id, darius);

            var personality = new SocialVector();
            personality.Set(SocialDimensions.Warmth, 6000);
            personality.Set(SocialDimensions.Discipline, 8000);
            mina.SetPersonality(personality);
            mina.Values.Set(new AuthoredId("value.family"), 9000);
            mina.Interests.Set(new AuthoredId("interest.clocks"), 7000);
            mina.Affect.Set(AffectKinds.Stress, AnalyticalProgression.Linear(3000, fixture.Host.World.Clock.Now, -1, 10));

            var field = new AppraisalField(
                mina.Id,
                AppraisalLenses.Respect,
                100,
                new[] { new SocialLinearTerm(SocialDimensions.Discipline, 8000) },
                new[] { new SocialPairwiseTerm(SocialDimensions.Agency, SocialDimensions.Stability, 2000) },
                new SocialVector(),
                null,
                null,
                new AuthoredId("social.calibration.standard"),
                3);
            mina.SetAppraisalField(field);

            var belief = SocialBeliefUpdateService.BroadPrior();
            belief.Mean.Set(SocialDimensions.Agency, 7500);
            belief.SetCovariance(SocialDimensions.Agency, SocialDimensions.Agency, 20000000);
            belief.MarkEvidenceApplied();
            fixture.Host.World.Knowledge.SetSocialBelief(ObserverRef.Character(mina.Id), darius.Id, belief);

            var relationship = new Relationship(
                fixture.Host.World.RuntimeIds.Relationships.Next(),
                mina.Id,
                darius.Id,
                new AuthoredId("relationship.coworker"),
                AnalyticalProgression.Constant(0, fixture.Host.World.Clock.Now),
                fixture.Host.World.Clock.Now);
            var effects = new SortedDictionary<AuthoredId, long>
            {
                { RelationshipChannels.TrustJudgment, 1200 },
            };
            relationship.RecordDirectionalInteraction(
                mina.Id,
                fixture.Host.World.Clock.Now,
                effects,
                45,
                300,
                new RelationshipMemory(
                    new AuthoredId("relationship.memory.kept_promise"),
                    fixture.Host.World.Clock.Now,
                    new AuthoredId("social.explanation.kept_promise"),
                    effects));
            fixture.Host.World.Relationships.Add(relationship.Id, relationship);
            fixture.Host.World.RelationshipIndex.Register(relationship);

            WorldState restored = fixture.Host.SaveMapper.Restore(fixture.Host.Session.Save("social"));
            Character restoredMina = restored.Characters.Get(mina.Id);
            Relationship restoredRelationship = restored.Relationships.Get(relationship.Id);
            DirectionalRelationshipState restoredDirection = restoredRelationship.From(mina.Id);

            Assert.Equal(6000, restoredMina.Personality[SocialDimensions.Warmth]);
            Assert.Equal(9000, restoredMina.Values.Intensity(new AuthoredId("value.family")));
            Assert.Equal(7000, restoredMina.Interests.Intensity(new AuthoredId("interest.clocks")));
            Assert.Equal(3000, restoredMina.Affect.ValueAt(AffectKinds.Stress, restored.Clock.Now));
            Assert.True(restoredMina.TryGetAppraisalField(AppraisalLenses.Respect, out AppraisalField restoredField));
            Assert.Equal(3, restoredField.Revision);
            Assert.True(restored.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(mina.Id), darius.Id, out BeliefDistribution restoredBelief));
            Assert.Equal(7500, restoredBelief.Mean[SocialDimensions.Agency]);
            Assert.Equal(20000000, restoredBelief.Covariance(SocialDimensions.Agency, SocialDimensions.Agency));
            Assert.Equal(1200, restoredDirection.ChannelAt(RelationshipChannels.TrustJudgment, restored.Clock.Now));
            Assert.Equal(300, restoredDirection.FamiliarityAt(restored.Clock.Now));
            Assert.Equal(45, restoredDirection.ExposureMinutes);
            Assert.Single(restoredDirection.Memories);
            Assert.Equal(0, restoredRelationship.From(darius.Id).ChannelAt(
                RelationshipChannels.TrustJudgment, restored.Clock.Now));
        }

        [Fact]
        public void SchemaOneAffinityMigratesIntoTwoEqualDirectionalStartingStates()
        {
            var save = new SaveGameData { SchemaVersion = 1 };
            save.Relationships.Add(new RelationshipData
            {
                Id = 1,
                LowCharacterId = 2,
                HighCharacterId = 3,
                Familiarity = 700,
                EstablishedAtMinutes = 10,
                LastInteractionAtMinutes = 20,
                Affinity = new ProgressionData
                {
                    ValueAtAnchor = -4000,
                    AnchoredAtMinutes = 20,
                    RateDenominator = 1,
                    MinValue = -10000,
                    MaxValue = 10000,
                },
            });

            var report = new SaveMigrator().Migrate(save, 1, 1, 1);

            Assert.True(report.CanLoad);
            Assert.Equal(SaveGameData.CurrentSchemaVersion, save.SchemaVersion);
            Assert.Equal(2, save.Relationships[0].LowToHigh.ObserverId);
            Assert.Equal(3, save.Relationships[0].HighToLow.ObserverId);
            Assert.Equal(-4000, save.Relationships[0].LowToHigh.Channels[0].Progression.ValueAtAnchor);
            Assert.Equal(-4000, save.Relationships[0].HighToLow.Channels[0].Progression.ValueAtAnchor);
            Assert.Equal(700, save.Relationships[0].LowToHigh.FamiliarityProgression.ValueAtAnchor);
        }
    }
}
