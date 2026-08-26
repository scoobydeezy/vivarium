using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Unity.Authoring;

namespace Vivarium.Unity.EditorTools
{
    /// <summary>Deterministically bakes loose per-entity assets into build-included pack indexes.</summary>
    public static class ContentPackBaker
    {
        [MenuItem("Vivarium/Bake Content Pack Indexes")]
        public static void BakeAll()
        {
            string[] manifestGuids = AssetDatabase.FindAssets("t:" + nameof(ContentPackManifestAsset));
            Array.Sort(manifestGuids, StringComparer.Ordinal);
            for (int i = 0; i < manifestGuids.Length; i++)
                Bake(AssetDatabase.LoadAssetAtPath<ContentPackManifestAsset>(
                    AssetDatabase.GUIDToAssetPath(manifestGuids[i])));
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {manifestGuids.Length} content pack index(es).");
        }

        public static List<string> ValidateAllFresh()
        {
            var problems = new List<string>();
            string[] manifestGuids = AssetDatabase.FindAssets("t:" + nameof(ContentPackManifestAsset));
            Array.Sort(manifestGuids, StringComparer.Ordinal);
            for (int i = 0; i < manifestGuids.Length; i++)
            {
                string manifestPath = AssetDatabase.GUIDToAssetPath(manifestGuids[i]);
                var manifest = AssetDatabase.LoadAssetAtPath<ContentPackManifestAsset>(manifestPath);
                string indexPath = IndexPathFor(manifestPath);
                var index = AssetDatabase.LoadAssetAtPath<ContentPackIndexAsset>(indexPath);
                if (index == null)
                {
                    problems.Add($"{manifestPath}: missing baked index at {indexPath}");
                    continue;
                }

                string packFolder = PackFolder(manifestPath);
                TraitDefinitionAsset[] traits = Discover<TraitDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                NeedDefinitionAsset[] needs = Discover<NeedDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                ActivityDefinitionAsset[] activities = Discover<ActivityDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                DecisionDefinitionAsset[] decisions = Discover<DecisionDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                InterventionDefinitionAsset[] interventions = Discover<InterventionDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                LocationKindDefinitionAsset[] locationKinds = Discover<LocationKindDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                AppraisalCalibrationAsset[] calibrations = Discover<AppraisalCalibrationAsset>(packFolder, asset => asset.AuthoredId);
                SocialEvidenceAsset[] evidence = Discover<SocialEvidenceAsset>(packFolder, asset => asset.AuthoredId);
                DecisionImportancePolicyAsset[] importancePolicies = Discover<DecisionImportancePolicyAsset>(packFolder, asset => asset.AuthoredId);
                CommitmentAccountabilityPolicyAsset[] accountabilityPolicies =
                    Discover<CommitmentAccountabilityPolicyAsset>(packFolder, asset => asset.AuthoredId);
                EmploymentDefinitionAsset[] employments = Discover<EmploymentDefinitionAsset>(packFolder, asset => asset.AuthoredId);
                SocialPressureAsset[] pressures = Discover<SocialPressureAsset>(packFolder, asset => asset.AuthoredId);
                if (importancePolicies.Length > 1)
                    problems.Add($"{packFolder}: more than one Decision Importance policy is present");
                DecisionImportancePolicyAsset importance = importancePolicies.Length == 0 ? null : importancePolicies[0];
                string expected = Fingerprint(
                    traits, needs, activities, decisions, interventions, locationKinds, calibrations,
                    evidence, importancePolicies, accountabilityPolicies, employments, pressures);
                if (index.Manifest != manifest)
                    problems.Add($"{indexPath}: index references the wrong manifest");
                if (!SameAssets(index.Traits, traits) || !SameAssets(index.Needs, needs) ||
                    !SameAssets(index.Activities, activities) || !SameAssets(index.Decisions, decisions) ||
                    !SameAssets(index.Interventions, interventions) || !SameAssets(index.LocationKinds, locationKinds) ||
                    !SameAssets(index.AppraisalCalibrations, calibrations) || !SameAssets(index.SocialEvidence, evidence) ||
                    index.DecisionImportancePolicy != importance ||
                    !SameAssets(index.CommitmentAccountabilityPolicies, accountabilityPolicies) ||
                    !SameAssets(index.Employments, employments) || !SameAssets(index.SocialPressures, pressures) ||
                    index.BakeFingerprint != expected)
                    problems.Add($"{indexPath}: baked inventory is stale; run Vivarium/Bake Content Pack Indexes");
            }
            return problems;
        }

        private static void Bake(ContentPackManifestAsset manifest)
        {
            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            string indexPath = IndexPathFor(manifestPath);
            var index = AssetDatabase.LoadAssetAtPath<ContentPackIndexAsset>(indexPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<ContentPackIndexAsset>();
                AssetDatabase.CreateAsset(index, indexPath);
            }

            string packFolder = PackFolder(manifestPath);
            TraitDefinitionAsset[] traits = Discover<TraitDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            NeedDefinitionAsset[] needs = Discover<NeedDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            ActivityDefinitionAsset[] activities = Discover<ActivityDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            DecisionDefinitionAsset[] decisions = Discover<DecisionDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            InterventionDefinitionAsset[] interventions = Discover<InterventionDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            LocationKindDefinitionAsset[] locationKinds = Discover<LocationKindDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            AppraisalCalibrationAsset[] calibrations = Discover<AppraisalCalibrationAsset>(packFolder, asset => asset.AuthoredId);
            SocialEvidenceAsset[] evidence = Discover<SocialEvidenceAsset>(packFolder, asset => asset.AuthoredId);
            DecisionImportancePolicyAsset[] importancePolicies = Discover<DecisionImportancePolicyAsset>(packFolder, asset => asset.AuthoredId);
            CommitmentAccountabilityPolicyAsset[] accountabilityPolicies =
                Discover<CommitmentAccountabilityPolicyAsset>(packFolder, asset => asset.AuthoredId);
            EmploymentDefinitionAsset[] employments = Discover<EmploymentDefinitionAsset>(packFolder, asset => asset.AuthoredId);
            SocialPressureAsset[] pressures = Discover<SocialPressureAsset>(packFolder, asset => asset.AuthoredId);
            if (importancePolicies.Length > 1)
                throw new InvalidOperationException($"Pack folder '{packFolder}' contains more than one Decision Importance policy.");
            var serialized = new SerializedObject(index);
            serialized.FindProperty("manifest").objectReferenceValue = manifest;
            SetArray(serialized.FindProperty("traits"), traits);
            SetArray(serialized.FindProperty("needs"), needs);
            SetArray(serialized.FindProperty("activities"), activities);
            SetArray(serialized.FindProperty("decisions"), decisions);
            SetArray(serialized.FindProperty("interventions"), interventions);
            SetArray(serialized.FindProperty("locationKinds"), locationKinds);
            SetArray(serialized.FindProperty("appraisalCalibrations"), calibrations);
            SetArray(serialized.FindProperty("socialEvidence"), evidence);
            serialized.FindProperty("decisionImportancePolicy").objectReferenceValue =
                importancePolicies.Length == 0 ? null : importancePolicies[0];
            SetArray(serialized.FindProperty("commitmentAccountabilityPolicies"), accountabilityPolicies);
            SetArray(serialized.FindProperty("employments"), employments);
            SetArray(serialized.FindProperty("socialPressures"), pressures);
            serialized.FindProperty("bakeFingerprint").stringValue =
                Fingerprint(
                    traits, needs, activities, decisions, interventions, locationKinds, calibrations,
                    evidence, importancePolicies, accountabilityPolicies, employments, pressures);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(index);
        }

        private static T[] Discover<T>(string packFolder, Func<T, string> authoredId) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { packFolder });
            var assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            Array.Sort(assets, (left, right) =>
            {
                int id = string.CompareOrdinal(authoredId(left), authoredId(right));
                return id != 0
                    ? id
                    : string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right));
            });
            for (int i = 1; i < assets.Length; i++)
                if (string.Equals(authoredId(assets[i - 1]), authoredId(assets[i]), StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Pack folder '{packFolder}' contains duplicate {typeof(T).Name} id '{authoredId(assets[i])}'.");
            return assets;
        }

        private static string Fingerprint(
            TraitDefinitionAsset[] traits,
            NeedDefinitionAsset[] needs,
            ActivityDefinitionAsset[] activities,
            DecisionDefinitionAsset[] decisions,
            InterventionDefinitionAsset[] interventions,
            LocationKindDefinitionAsset[] locationKinds,
            AppraisalCalibrationAsset[] calibrations,
            SocialEvidenceAsset[] evidence,
            DecisionImportancePolicyAsset[] importancePolicies,
            CommitmentAccountabilityPolicyAsset[] accountabilityPolicies,
            EmploymentDefinitionAsset[] employments,
            SocialPressureAsset[] pressures)
        {
            var inventory = new System.Text.StringBuilder();
            AppendFingerprint(inventory, "Trait", traits, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "Need", needs, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "Activity", activities, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "Decision", decisions, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "Intervention", interventions, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "LocationKind", locationKinds, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "AppraisalCalibration", calibrations, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "SocialEvidence", evidence, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "DecisionImportancePolicy", importancePolicies, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "CommitmentAccountabilityPolicy", accountabilityPolicies, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "Employment", employments, asset => asset.AuthoredId);
            AppendFingerprint(inventory, "SocialPressure", pressures, asset => asset.AuthoredId);
            return StableHash.OfString(inventory.ToString()).ToString("x16");
        }

        private static void AppendFingerprint<T>(
            System.Text.StringBuilder inventory,
            string family,
            T[] assets,
            Func<T, string> authoredId) where T : UnityEngine.Object
        {
            for (int i = 0; i < assets.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(assets[i]);
                inventory.Append(family).Append('|').Append(authoredId(assets[i])).Append('|')
                    .Append(AssetDatabase.AssetPathToGUID(path)).Append('\n');
            }
        }

        private static bool SameAssets<T>(T[] left, T[] right) where T : UnityEngine.Object
        {
            if (left == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static string PackFolder(string manifestPath) =>
            manifestPath.Substring(0, manifestPath.LastIndexOf('/'));

        private static string IndexPathFor(string manifestPath) => PackFolder(manifestPath) + "/pack.index.asset";
    }

    public sealed class ContentPackBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> problems = ContentPackBaker.ValidateAllFresh();
            if (problems.Count > 0)
                throw new BuildFailedException("Content pack indexes are not build-ready:\n - " + string.Join("\n - ", problems));
        }
    }
}
