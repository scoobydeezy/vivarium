using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Domain.Content;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Randomness;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class SocialModelTests
    {
        private static readonly AuthoredId CalibrationId = new AuthoredId("social.calibration.standard");

        [Fact]
        public void SparseFieldIsDirectionalAndProducesComparableStrength()
        {
            BeliefDistribution belief = CertainBelief(
                (SocialDimensions.Agency, 8000),
                (SocialDimensions.Stability, 6000),
                (SocialDimensions.Attunement, -2000));
            AppraisalCalibrationProfile calibration = StandardCalibration();

            var respect = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Respect,
                0,
                new[]
                {
                    new SocialLinearTerm(SocialDimensions.Agency, 8000),
                    new SocialLinearTerm(SocialDimensions.Stability, 6000),
                },
                null,
                null,
                null,
                null,
                CalibrationId);
            var comfort = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Comfort,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Attunement, 9000) },
                new[] { new SocialPairwiseTerm(SocialDimensions.Agency, SocialDimensions.Attunement, 8000) },
                null,
                null,
                null,
                CalibrationId);

            var evaluator = new SocialAppraisalEvaluator();
            SocialEvaluationResult respectResult = evaluator.Evaluate(
                new CharacterId(2), belief, respect, new SocialEvaluationContext(), calibration);
            SocialEvaluationResult comfortResult = evaluator.Evaluate(
                new CharacterId(2), belief, comfort, new SocialEvaluationContext(), calibration);

            Assert.True(respectResult.NormalizedAppraisal > 0);
            Assert.True(comfortResult.NormalizedAppraisal < 0);
            Assert.True(respectResult.Strength >= AppraisalStrength.Moderate);
            Assert.Equal(new CharacterId(1), respectResult.ObserverId);
            Assert.Equal(new CharacterId(2), respectResult.TargetId);
        }

        [Fact]
        public void CovarianceChangesExpectedScoreWithoutChangingPointEstimate()
        {
            BeliefDistribution independent = CertainBelief(
                (SocialDimensions.Agency, 8000),
                (SocialDimensions.Attunement, -1000));
            BeliefDistribution correlated = independent.Copy();
            correlated.SetCovariance(SocialDimensions.Agency, SocialDimensions.Attunement, 20000000);
            var field = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Comfort,
                0,
                null,
                new[] { new SocialPairwiseTerm(SocialDimensions.Agency, SocialDimensions.Attunement, -5000) },
                null,
                null,
                null,
                CalibrationId);
            var evaluator = new SocialAppraisalEvaluator();

            SocialEvaluationResult point = evaluator.Evaluate(
                new CharacterId(2), independent, field, new SocialEvaluationContext(), StandardCalibration());
            SocialEvaluationResult uncertain = evaluator.Evaluate(
                new CharacterId(2), correlated, field, new SocialEvaluationContext(), StandardCalibration());

            Assert.Equal(point.PointLatentScore, uncertain.PointLatentScore);
            Assert.NotEqual(point.ExpectedLatentScore, uncertain.ExpectedLatentScore);
            Assert.Equal(-1000, uncertain.UncertaintyEffect);
        }

        [Fact]
        public void IdealTolerancePenalizesUncertaintyAtTheIdealPoint()
        {
            BeliefDistribution belief = CertainBelief((SocialDimensions.Warmth, 0));
            belief.SetCovariance(SocialDimensions.Warmth, SocialDimensions.Warmth, 25000000);
            var factor = new IdealFactor(
                new AuthoredId("social.factor.warmth"),
                new[] { new SocialLinearTerm(SocialDimensions.Warmth, SocialNumeric.Scale) });
            var field = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Affiliation,
                0,
                null,
                null,
                new SocialVector(),
                new[] { factor },
                null,
                CalibrationId);

            SocialEvaluationResult result = new SocialAppraisalEvaluator().Evaluate(
                new CharacterId(2), belief, field, new SocialEvaluationContext(), StandardCalibration());

            Assert.Equal(0, result.PointLatentScore);
            Assert.Equal(-1250, result.ExpectedLatentScore);
            Assert.Equal(-1250, result.UncertaintyEffect);
        }

        [Fact]
        public void ContextChangesTheFieldRatherThanPersonalityTruth()
        {
            BeliefDistribution belief = CertainBelief((SocialDimensions.Agency, 8000));
            var supervisorDelta = new AppraisalContextModifier(
                new AuthoredId("social.context.target_is_supervisor"),
                linearDeltas: new[] { new SocialLinearTerm(SocialDimensions.Agency, -10000) });
            var field = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Comfort,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Agency, 4000) },
                null,
                null,
                null,
                new[] { supervisorDelta },
                CalibrationId);
            var evaluator = new SocialAppraisalEvaluator();

            SocialEvaluationResult privateResult = evaluator.Evaluate(
                new CharacterId(2), belief, field, new SocialEvaluationContext(), StandardCalibration());
            SocialEvaluationResult supervisedResult = evaluator.Evaluate(
                new CharacterId(2),
                belief,
                field,
                new SocialEvaluationContext(new[] { supervisorDelta.ContextId }),
                StandardCalibration());

            Assert.True(privateResult.NormalizedAppraisal > 0);
            Assert.True(supervisedResult.NormalizedAppraisal < 0);
            Assert.Equal(8000, belief.Mean[SocialDimensions.Agency]);
        }

        [Fact]
        public void ExplanationTraceRetainsAuthoredAndContextProvenance()
        {
            var authoredSource = new AuthoredId("social.provenance.values_kindness");
            var contextSource = new AuthoredId("social.provenance.audience_pressure");
            var contextId = new AuthoredId("social.context.public");
            var field = new AppraisalField(
                new CharacterId(1),
                AppraisalLenses.Affiliation,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Warmth, 5000, authoredSource) },
                null,
                null,
                null,
                new[]
                {
                    new AppraisalContextModifier(
                        contextId,
                        biasDelta: -1000,
                        linearDeltas: new[] { new SocialLinearTerm(SocialDimensions.Warmth, -2000) },
                        provenance: contextSource),
                },
                CalibrationId);

            SocialEvaluationResult result = new SocialAppraisalEvaluator().Evaluate(
                new CharacterId(2),
                CertainBelief((SocialDimensions.Warmth, 8000)),
                field,
                new SocialEvaluationContext(new[] { contextId }),
                StandardCalibration());

            Assert.Contains(result.Contributions, item =>
                item.Kind == SocialContributionKind.Linear &&
                item.Explanation.Contains(authoredSource.Value) &&
                item.Explanation.Contains(contextSource.Value));
            Assert.Contains(result.Contributions, item => item.SourceId == contextSource && item.Kind == SocialContributionKind.Context);
        }

        [Fact]
        public void ReputationIsBoundedObserverKnowledgeAndDoesNotLeakIntoPlayerCount()
        {
            var world = new WorldState(19, SimTime.Epoch);
            var recipient = new Character(world.RuntimeIds.Characters.Next(), "Recipient", world.Clock.Now);
            var target = new Character(world.RuntimeIds.Characters.Next(), "Target", world.Clock.Now);
            var informant = new Character(world.RuntimeIds.Characters.Next(), "Informant", world.Clock.Now);
            world.Characters.Add(recipient.Id, recipient);
            world.Characters.Add(target.Id, target);
            world.Characters.Add(informant.Id, informant);

            new SocialReputationService().RecordReport(
                world,
                ObserverRef.Character(recipient.Id),
                target.Id,
                informant.Id,
                AppraisalLenses.Respect,
                17000);

            var key = new FactKey(FactKinds.ReportedSocialBelief, target.Id.ToRef(), AppraisalLenses.Respect);
            Assert.True(world.Knowledge.TryGet(ObserverRef.Character(recipient.Id), key, out KnowledgeEntry report));
            Assert.Equal(10000, report.ObservedValue.Magnitude);
            Assert.Equal(0, world.Knowledge.Count);
            Assert.Equal(1, world.Knowledge.AllObserverCount);
            Assert.False(world.Knowledge.Knows(key));
        }

        [Fact]
        public void CharacterObserversMaintainSparseDirectionalBeliefsAndEvidenceNarrowsThem()
        {
            var world = new WorldState(17, new SimTime(0));
            var mira = new Character(world.RuntimeIds.Characters.Next(), "Mira", world.Clock.Now);
            var darius = new Character(world.RuntimeIds.Characters.Next(), "Darius", world.Clock.Now);
            world.Characters.Add(mira.Id, mira);
            world.Characters.Add(darius.Id, darius);
            ObserverRef miraObserver = ObserverRef.Character(mira.Id);
            var measurement = new SocialEvidenceMeasurement(
                new AuthoredId("social.measurement.takes_charge"),
                new[]
                {
                    new SocialLinearTerm(SocialDimensions.Agency, 8000),
                    new SocialLinearTerm(SocialDimensions.Stability, 6000),
                },
                8000,
                10000000);
            var definition = new SocialEvidenceDefinition(
                new AuthoredId("social.action.takes_charge_calmly"),
                new[] { measurement },
                new AuthoredId("social.explanation.composed_assertiveness"));
            var evidence = new ObservedSocialEvidence(
                darius.Id,
                miraObserver,
                definition.ActionDefinitionId,
                world.Clock.Now,
                new AuthoredId("social.context.crisis"));
            var service = new SocialBeliefUpdateService();

            BeliefDistribution updated = service.Apply(world, evidence, definition);

            Assert.True(updated.Mean[SocialDimensions.Agency] > 0);
            Assert.True(updated.Mean[SocialDimensions.Stability] > 0);
            Assert.True(updated.Covariance(SocialDimensions.Agency, SocialDimensions.Agency) < SocialNumeric.MaxVariance);
            Assert.Equal(1, updated.EvidenceRevision);
            Assert.True(world.Knowledge.TryGetSocialBelief(miraObserver, darius.Id, out BeliefDistribution same));
            Assert.Same(updated, same);
            Assert.False(world.Knowledge.TryGetSocialBelief(ObserverRef.Character(darius.Id), mira.Id, out BeliefDistribution _));
        }

        [Fact]
        public void CompositeEvaluationKeepsRespectAffectionAndCurrentStressDistinct()
        {
            var world = new WorldState(5, new SimTime(100));
            var mira = new Character(world.RuntimeIds.Characters.Next(), "Mira", world.Clock.Now);
            var darius = new Character(world.RuntimeIds.Characters.Next(), "Darius", world.Clock.Now);
            world.Characters.Add(mira.Id, mira);
            world.Characters.Add(darius.Id, darius);
            mira.SetAppraisalField(new AppraisalField(
                mira.Id,
                AppraisalLenses.Respect,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Discipline, 7000) },
                null,
                null,
                null,
                null,
                CalibrationId));
            BeliefDistribution belief = CertainBelief((SocialDimensions.Discipline, 8000));
            world.Knowledge.SetSocialBelief(ObserverRef.Character(mira.Id), darius.Id, belief);
            mira.Affect.Set(AffectKinds.Stress, AnalyticalProgression.Constant(7000, world.Clock.Now));

            var relationship = new Relationship(
                world.RuntimeIds.Relationships.Next(),
                mira.Id,
                darius.Id,
                new AuthoredId("relationship.coworker"),
                AnalyticalProgression.Constant(0, world.Clock.Now),
                world.Clock.Now);
            relationship.From(mira.Id).ApplyChannelDelta(RelationshipChannels.Affection, world.Clock.Now, -6000);
            world.Relationships.Add(relationship.Id, relationship);
            world.RelationshipIndex.Register(relationship);

            var pressure = new SocialPressureDefinition(
                new AuthoredId("social.pressure.ask_for_help"),
                new[]
                {
                    new SocialFactorRule(
                        AppraisalLenses.Respect,
                        SocialFactorSourceKind.RelationshipChannel,
                        RelationshipChannels.Affection,
                        3000,
                        new AuthoredId("social.explanation.dislikes_target")),
                    new SocialFactorRule(
                        AppraisalLenses.Respect,
                        SocialFactorSourceKind.ObserverAffect,
                        AffectKinds.Stress,
                        -2000,
                        new AuthoredId("social.explanation.current_stress")),
                });
            DefinitionCatalog catalog = new DefinitionCatalog.Builder()
                .Add(StandardCalibration())
                .Add(pressure)
                .Build();

            CompositeSocialEvaluationResult result = new SocialPressureEvaluator().Evaluate(
                world,
                mira.Id,
                darius.Id,
                AppraisalLenses.Respect,
                new SocialEvaluationContext(),
                pressure,
                catalog);

            Assert.True(result.PersonalityAppraisal.NormalizedAppraisal > 0);
            Assert.Equal(2, result.AdditionalContributions.Count);
            Assert.Contains(result.AdditionalContributions, contribution => contribution.SourceId == new AuthoredId("social.explanation.current_stress"));
            Assert.Equal(-6000, relationship.From(mira.Id).ChannelAt(RelationshipChannels.Affection, world.Clock.Now));
            Assert.Equal(7000, mira.Affect.ValueAt(AffectKinds.Stress, world.Clock.Now));
        }

        [Fact]
        public void GeneratedProfilesAreStableForSeedAndCharacterId()
        {
            SocialVector firstPersonality;
            AppraisalField firstRespect;
            {
                var world = new WorldState(827119, new SimTime(0));
                var character = new Character(world.RuntimeIds.Characters.Next(), "Mira", world.Clock.Now);
                new SocialProfileGenerator(new DeterministicRandomOracle(world.WorldSeed)).Generate(character, CalibrationId);
                firstPersonality = character.Personality.Copy();
                Assert.True(character.TryGetAppraisalField(AppraisalLenses.Respect, out firstRespect));
            }
            {
                var world = new WorldState(827119, new SimTime(0));
                var character = new Character(world.RuntimeIds.Characters.Next(), "Mira", world.Clock.Now);
                new SocialProfileGenerator(new DeterministicRandomOracle(world.WorldSeed)).Generate(character, CalibrationId);
                Assert.True(character.TryGetAppraisalField(AppraisalLenses.Respect, out AppraisalField secondRespect));
                foreach (AuthoredId dimension in SocialDimensions.Provisional)
                {
                    Assert.Equal(firstPersonality[dimension], character.Personality[dimension]);
                }
                Assert.Equal(firstRespect.LinearTerms.Count, secondRespect.LinearTerms.Count);
                for (int i = 0; i < firstRespect.LinearTerms.Count; i++)
                {
                    Assert.Equal(firstRespect.LinearTerms[i].Coefficient, secondRespect.LinearTerms[i].Coefficient);
                }
            }
        }

        [Fact]
        public void StrongContradictoryEvidenceBroadensAnOverconfidentBelief()
        {
            var world = new WorldState(19, new SimTime(0));
            var mira = new Character(world.RuntimeIds.Characters.Next(), "Mira", world.Clock.Now);
            var darius = new Character(world.RuntimeIds.Characters.Next(), "Darius", world.Clock.Now);
            world.Characters.Add(mira.Id, mira);
            world.Characters.Add(darius.Id, darius);
            var priorMean = new SocialVector();
            priorMean.Set(SocialDimensions.Agency, 8000);
            var prior = new BeliefDistribution(priorMean);
            prior.SetCovariance(SocialDimensions.Agency, SocialDimensions.Agency, 1000000);
            world.Knowledge.SetSocialBelief(ObserverRef.Character(mira.Id), darius.Id, prior);
            var definition = new SocialEvidenceDefinition(
                new AuthoredId("social.action.yields_under_pressure"),
                new[]
                {
                    new SocialEvidenceMeasurement(
                        new AuthoredId("social.measurement.low_agency"),
                        new[] { new SocialLinearTerm(SocialDimensions.Agency, 10000) },
                        -8000,
                        1000000),
                },
                new AuthoredId("social.explanation.yielded"));

            new SocialBeliefUpdateService().Apply(
                world,
                new ObservedSocialEvidence(
                    darius.Id,
                    ObserverRef.Character(mira.Id),
                    definition.ActionDefinitionId,
                    world.Clock.Now,
                    new AuthoredId("social.context.crisis")),
                definition);

            Assert.True(prior.Mean[SocialDimensions.Agency] < 8000);
            Assert.True(prior.Covariance(SocialDimensions.Agency, SocialDimensions.Agency) > 1000000);
        }

        private static BeliefDistribution CertainBelief(params (AuthoredId Dimension, long Value)[] values)
        {
            var vector = new SocialVector();
            for (int i = 0; i < values.Length; i++)
            {
                vector.Set(values[i].Dimension, values[i].Value);
            }

            var belief = new BeliefDistribution(vector);
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                belief.SetCovariance(SocialDimensions.Provisional[i], SocialDimensions.Provisional[i], 0);
            }

            return belief;
        }

        private static AppraisalCalibrationProfile StandardCalibration() => new AppraisalCalibrationProfile(
            CalibrationId,
            new[]
            {
                new AppraisalStrengthThreshold(1000, AppraisalStrength.Minor),
                new AppraisalStrengthThreshold(2500, AppraisalStrength.Moderate),
                new AppraisalStrengthThreshold(5000, AppraisalStrength.Strong),
                new AppraisalStrengthThreshold(7500, AppraisalStrength.Extreme),
            },
            1);
    }
}
