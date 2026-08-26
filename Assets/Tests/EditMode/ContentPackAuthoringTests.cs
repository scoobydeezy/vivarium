using NUnit.Framework;
using UnityEditor;
using Vivarium.Application.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Unity.Authoring;
using Vivarium.Unity.EditorTools;

namespace Vivarium.Unity.Tests
{
    public sealed class ContentPackAuthoringTests
    {
        [Test]
        public void Baked_pack_indexes_match_their_authored_folders()
        {
            Assert.That(ContentPackBaker.ValidateAllFresh(), Is.Empty);
        }

        [Test]
        public void BaseGame_migrated_assets_build_the_expected_catalog()
        {
            var index = AssetDatabase.LoadAssetAtPath<ContentPackIndexAsset>(
                "Assets/Game/Authoring/Packs/BaseGame/pack.index.asset");
            Assert.That(index, Is.Not.Null);

            ContentPackManifestAsset manifest = index.Manifest;
            var authoredOverrides = manifest.Overrides;
            var overrides = new ContentOverrideDeclaration[authoredOverrides.Length];
            for (int i = 0; i < overrides.Length; i++)
                overrides[i] = new ContentOverrideDeclaration(
                    authoredOverrides[i].family,
                    new AuthoredId(authoredOverrides[i].authoredId),
                    authoredOverrides[i].expectedSourcePackId);
            DefinitionCatalog catalog = ContentPackResolver.Resolve(new[]
            {
                new ContentPackContribution(
                    manifest.PackId,
                    manifest.DisplayName,
                    manifest.PackVersion,
                    index.BuildDefinitionSet(),
                    overrides),
            }).Catalog;

            Assert.That(catalog.Activities.Count, Is.EqualTo(11));
            Assert.That(catalog.Traits.Count, Is.EqualTo(4));
            Assert.That(catalog.Needs.Count, Is.EqualTo(4));
            Assert.That(catalog.EmploymentDefinitions.Count, Is.EqualTo(2));
            Assert.That(catalog.CommitmentAccountabilityPolicies.Count, Is.EqualTo(1));
            Assert.That(catalog.LocationKinds.Count, Is.EqualTo(3));
            Assert.That(catalog.AppraisalCalibrations.Count, Is.EqualTo(1));
            Assert.That(catalog.SocialEvidence.Count, Is.EqualTo(3));
            Assert.That(catalog.SocialPressures.Count, Is.EqualTo(2));
            Assert.That(catalog.Decisions.Count, Is.EqualTo(4));
            Assert.That(catalog.Interventions.Count, Is.EqualTo(3));
            AssertActivity(catalog, "activity.working", "Working", 360, producesOutcome: true);
            AssertActivity(catalog, "activity.cafe_hosting", "Hosting at the cafe", 90);
            AssertActivity(catalog, "activity.traveling", "Traveling", 10, isTravel: true);
            AssertActivity(catalog, "activity.sleeping", "Sleeping", 480);
            AssertActivity(catalog, "activity.waiting", "Waiting", 60);

            NeedDefinition energy = catalog.Needs[WellKnownNeeds.Energy];
            Assert.That(energy.DefaultRateNumerator, Is.EqualTo(-10));
            Assert.That(energy.BehaviouralThresholds, Is.EqualTo(new long[] { 2000, 8000 }));
            Assert.That(energy.RestRoutine, Is.Not.Null);
            Assert.That(energy.RestRoutine.ActivityDefinitionId, Is.EqualTo(new AuthoredId("activity.sleeping")));
            Assert.That(energy.RestRoutine.ActivationThreshold, Is.EqualTo(2000));
            Assert.That(energy.RestRoutine.RecoveredThreshold, Is.EqualTo(8000));

            NeedDefinition hunger = catalog.Needs[new AuthoredId("need.hunger")];
            Assert.That(hunger.BehaviouralThresholds, Is.EqualTo(new long[] { 6000, 8000, 9500 }));
            Assert.That(hunger.SatisfactionRoutine.ActivityDefinitionId,
                Is.EqualTo(new AuthoredId("activity.eating")));

            Assert.That(catalog.DecisionImportancePolicy.AdmissionFloor, Is.EqualTo(6500));
            Assert.That(catalog.DecisionImportancePolicy.PrioritizedFeedFloor, Is.EqualTo(6500));
            Assert.That(catalog.DecisionImportancePolicy.NormalFeedFloor, Is.EqualTo(7000));
            Assert.That(catalog.DecisionImportancePolicy.AutoHoldFloor, Is.EqualTo(7000));

            EmploymentDefinition employment = catalog.EmploymentDefinitions[
                new AuthoredId("employment.bakery_worker")];
            Assert.That(employment.RoleId, Is.EqualTo(new AuthoredId("employment.role.baker")));
            Assert.That(employment.ObligationPatterns.Count, Is.EqualTo(2));
            Assert.That(employment.ObligationPatterns[0].Id,
                Is.EqualTo(new AuthoredId("routine.bakery_closing_duty")));
            Assert.That(employment.ObligationPatterns[1].Id,
                Is.EqualTo(new AuthoredId("routine.bakery_shift")));
            Assert.That(employment.ObligationPatterns[0].AccountabilityPolicy.Id,
                Is.EqualTo(new AuthoredId("accountability.social_commitment")));

            EmploymentDefinition cafeHost = catalog.EmploymentDefinitions[
                new AuthoredId("employment.cafe_host")];
            Assert.That(cafeHost.RoleId, Is.EqualTo(new AuthoredId("employment.role.cafe_host")));
            Assert.That(cafeHost.ObligationPatterns.Count, Is.EqualTo(1));
            Assert.That(cafeHost.ObligationPatterns[0].Id,
                Is.EqualTo(new AuthoredId("routine.cafe_hosting_shift")));

            CommitmentAccountabilityPolicy accountability = catalog.CommitmentAccountabilityPolicies[
                new AuthoredId("accountability.social_commitment")];
            Assert.That(accountability.ByOutcome[CommitmentOutcomeKind.Fulfilled].EvidenceActionId,
                Is.EqualTo(new AuthoredId("social.action.commitment_fulfilled")));
            CommitmentConsequenceSet breach = accountability.ByOutcome[CommitmentOutcomeKind.Relinquished];
            Assert.That(breach.Memory.MemoryKind,
                Is.EqualTo(new AuthoredId("relationship.memory.commitment_breach")));
            Assert.That(breach.ChannelDeltas[new AuthoredId("relationship.channel.trust_judgment")],
                Is.EqualTo(-1200));
            Assert.That(breach.ChannelDeltas[new AuthoredId("relationship.channel.resentment")],
                Is.EqualTo(900));

            var building = catalog.LocationKinds[new AuthoredId("location_kind.building")];
            Assert.That(building.DisplayName, Is.EqualTo("location_kind.building"));
            Assert.That(building.OccupiableByDefault, Is.True);

            AppraisalCalibrationProfile calibration = catalog.AppraisalCalibrations[
                new AuthoredId("social.calibration.standard")];
            Assert.That(calibration.Version, Is.EqualTo(1));
            Assert.That(calibration.Thresholds.Count, Is.EqualTo(4));
            Assert.That(calibration.Thresholds[2].MinimumMagnitude, Is.EqualTo(5000));
            Assert.That(calibration.Thresholds[2].Strength, Is.EqualTo(AppraisalStrength.Strong));

            SocialEvidenceDefinition breachEvidence = catalog.SocialEvidence[
                new AuthoredId("social.action.commitment_breach")];
            Assert.That(breachEvidence.ExplanationId,
                Is.EqualTo(new AuthoredId("social.explanation.commitment_breach")));
            Assert.That(breachEvidence.Measurements[0].ObservedValue, Is.EqualTo(-6000));
            Assert.That(breachEvidence.Measurements[0].NoiseVariance, Is.EqualTo(30000000));
            Assert.That(breachEvidence.Measurements[0].Projection.Count, Is.EqualTo(2));

            Assert.That(catalog.SocialPressures[new AuthoredId("social.pressure.seek_company")].Rules,
                Is.Empty);

            DecisionDefinition leaveWork = catalog.Decisions[new AuthoredId("decision.leave_work_early")];
            Assert.That(leaveWork.Options.Count, Is.EqualTo(2));
            Assert.That(leaveWork.Trigger.NeedId, Is.EqualTo(new AuthoredId("need.hunger")));
            Assert.That(leaveWork.ReasoningProgram.Bindings.Count, Is.EqualTo(3));
            Assert.That(leaveWork.ActivityOutcomes.Count, Is.EqualTo(1));
            Assert.That(catalog.Decisions[new AuthoredId("decision.commitment_conflict")]
                .CommitmentConflictTrigger, Is.Not.Null);
            Assert.That(catalog.Decisions[new AuthoredId("decision.seek_company")].SocialTrigger,
                Is.Not.Null);
            Assert.That(catalog.Decisions[new AuthoredId("decision.choose_recreation")]
                .ReasoningProgram.Bindings.Count, Is.EqualTo(1));

            InterventionDefinition loadedTwenty = catalog.Interventions[
                new AuthoredId("intervention.loaded_twenty")];
            Assert.That(loadedTwenty.Kind, Is.EqualTo(InterventionKind.ReplaceDie));
            Assert.That(loadedTwenty.ReplacementDie.Sides, Is.EqualTo(20));
            Assert.That(loadedTwenty.ReplacementDie.FixedResult, Is.EqualTo(20));
            Assert.That(catalog.Interventions[new AuthoredId("intervention.re_roll")].Kind,
                Is.EqualTo(InterventionKind.Reroll));
        }

        private static void AssertActivity(
            DefinitionCatalog catalog,
            string id,
            string displayName,
            int durationMinutes,
            bool producesOutcome = false,
            bool isTravel = false)
        {
            var definition = catalog.Activities[new AuthoredId(id)];
            Assert.That(definition.DisplayName, Is.EqualTo(displayName));
            Assert.That(definition.DefaultDuration, Is.EqualTo(SimDuration.FromMinutes(durationMinutes)));
            Assert.That(definition.ProducesOutcome, Is.EqualTo(producesOutcome));
            Assert.That(definition.IsTravel, Is.EqualTo(isTravel));
        }
    }
}
