using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Converts one baked pack index into immutable Domain content and validates its assets.</summary>
    public static class ContentPackIndexConversion
    {
        /// <summary>Converts the baked per-entity inventory into one immutable pack contribution.</summary>
        public static DefinitionSet BuildDefinitionSet(this ContentPackIndexAsset index)
        {
            if (index == null) throw new System.ArgumentNullException(nameof(index));
            if (index.Manifest == null)
                throw new System.InvalidOperationException("Content pack index needs a manifest.");
            var builder = new DefinitionCatalog.Builder { ContentVersion = index.Manifest.PackVersion };
            if (index.DecisionImportancePolicy != null)
                builder.SetDecisionImportancePolicy(index.DecisionImportancePolicy.ToDefinition());

            TraitDefinitionAsset[] traitAssets = index.Traits;
            for (int i = 0; i < traitAssets.Length; i++)
            {
                if (traitAssets[i] != null) builder.Add(traitAssets[i].ToDefinition());
            }

            NeedDefinitionAsset[] needAssets = index.Needs;
            for (int i = 0; i < needAssets.Length; i++)
            {
                if (needAssets[i] != null) builder.Add(needAssets[i].ToDefinition());
            }

            ActivityDefinitionAsset[] activityAssets = index.Activities;
            for (int i = 0; i < activityAssets.Length; i++)
            {
                if (activityAssets[i] != null) builder.Add(activityAssets[i].ToDefinition());
            }

            LocationKindDefinitionAsset[] locationKindAssets = index.LocationKinds;
            for (int i = 0; i < locationKindAssets.Length; i++)
            {
                if (locationKindAssets[i] != null) builder.Add(locationKindAssets[i].ToDefinition());
            }

            DecisionDefinitionAsset[] decisionAssets = index.Decisions;
            for (int i = 0; i < decisionAssets.Length; i++)
            {
                if (decisionAssets[i] != null) builder.Add(decisionAssets[i].ToDefinition());
            }

            InterventionDefinitionAsset[] interventionAssets = index.Interventions;
            for (int i = 0; i < interventionAssets.Length; i++)
            {
                if (interventionAssets[i] != null) builder.Add(interventionAssets[i].ToDefinition());
            }

            AppraisalCalibrationAsset[] calibrationAssets = index.AppraisalCalibrations;
            for (int i = 0; i < calibrationAssets.Length; i++)
            {
                if (calibrationAssets[i] != null) builder.Add(calibrationAssets[i].ToDefinition());
            }
            SocialEvidenceAsset[] evidenceAssets = index.SocialEvidence;
            for (int i = 0; i < evidenceAssets.Length; i++)
            {
                if (evidenceAssets[i] != null) builder.Add(evidenceAssets[i].ToDefinition());
            }
            var accountabilityPolicies = new Dictionary<AuthoredId, CommitmentAccountabilityPolicy>();
            CommitmentAccountabilityPolicyAsset[] accountabilityPolicyAssets = index.CommitmentAccountabilityPolicies;
            for (int i = 0; i < accountabilityPolicyAssets.Length; i++)
            {
                if (accountabilityPolicyAssets[i] == null) continue;
                CommitmentAccountabilityPolicy policy = accountabilityPolicyAssets[i].ToDefinition();
                accountabilityPolicies.Add(policy.Id, policy);
                builder.Add(policy);
            }
            EmploymentDefinitionAsset[] employmentAssets = index.Employments;
            for (int i = 0; i < employmentAssets.Length; i++)
            {
                if (employmentAssets[i] != null)
                    builder.Add(employmentAssets[i].ToDefinition(accountabilityPolicies));
            }
            SocialPressureAsset[] pressureAssets = index.SocialPressures;
            for (int i = 0; i < pressureAssets.Length; i++)
            {
                if (pressureAssets[i] != null) builder.Add(pressureAssets[i].ToDefinition());
            }

            return builder.BuildSet();
        }

        /// <summary>Collects every authoring-time problem without throwing. Used by the editor menu.</summary>
        public static List<string> ValidateContent(this ContentPackIndexAsset index)
        {
            var problems = new List<string>();
            var seenIds = new HashSet<string>();

            if (index == null)
            {
                problems.Add("content pack index is missing");
                return problems;
            }
            if (index.Manifest == null) problems.Add("content pack manifest is missing");

            if (index.DecisionImportancePolicy != null)
                foreach (string problem in index.DecisionImportancePolicy.Validate()) problems.Add(problem);

            TraitDefinitionAsset[] traitAssets = index.Traits;
            for (int i = 0; i < traitAssets.Length; i++)
            {
                if (traitAssets[i] == null)
                {
                    problems.Add($"trait slot {i} is empty");
                    continue;
                }

                foreach (string problem in traitAssets[i].Validate())
                {
                    problems.Add(problem);
                }

                if (!seenIds.Add(traitAssets[i].AuthoredId))
                {
                    problems.Add($"duplicate trait id '{traitAssets[i].AuthoredId}'");
                }
            }

            NeedDefinitionAsset[] needAssets = index.Needs;
            for (int i = 0; i < needAssets.Length; i++)
            {
                if (needAssets[i] == null)
                {
                    problems.Add($"need slot {i} is empty");
                    continue;
                }
                foreach (string problem in needAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(needAssets[i].AuthoredId))
                    problems.Add($"duplicate need id '{needAssets[i].AuthoredId}'");
            }

            DecisionDefinitionAsset[] decisionAssets = index.Decisions;
            for (int i = 0; i < decisionAssets.Length; i++)
            {
                if (decisionAssets[i] == null)
                {
                    problems.Add($"decision slot {i} is empty");
                    continue;
                }
                foreach (string problem in decisionAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(decisionAssets[i].AuthoredId))
                    problems.Add($"duplicate decision id '{decisionAssets[i].AuthoredId}'");
            }

            InterventionDefinitionAsset[] interventionAssets = index.Interventions;
            for (int i = 0; i < interventionAssets.Length; i++)
            {
                if (interventionAssets[i] == null)
                {
                    problems.Add($"intervention slot {i} is empty");
                    continue;
                }
                foreach (string problem in interventionAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(interventionAssets[i].AuthoredId))
                    problems.Add($"duplicate intervention id '{interventionAssets[i].AuthoredId}'");
            }

            ActivityDefinitionAsset[] activityAssets = index.Activities;
            for (int i = 0; i < activityAssets.Length; i++)
            {
                if (activityAssets[i] == null)
                {
                    problems.Add($"activity slot {i} is empty");
                    continue;
                }
                foreach (string problem in activityAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(activityAssets[i].AuthoredId))
                    problems.Add($"duplicate activity id '{activityAssets[i].AuthoredId}'");
            }

            LocationKindDefinitionAsset[] locationKindAssets = index.LocationKinds;
            for (int i = 0; i < locationKindAssets.Length; i++)
            {
                if (locationKindAssets[i] == null)
                {
                    problems.Add($"location kind slot {i} is empty");
                    continue;
                }
                foreach (string problem in locationKindAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(locationKindAssets[i].AuthoredId))
                    problems.Add($"duplicate location kind id '{locationKindAssets[i].AuthoredId}'");
            }

            AppraisalCalibrationAsset[] calibrationAssets = index.AppraisalCalibrations;
            for (int i = 0; i < calibrationAssets.Length; i++)
            {
                if (calibrationAssets[i] == null)
                {
                    problems.Add($"appraisal calibration slot {i} is empty");
                    continue;
                }
                foreach (string problem in calibrationAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(calibrationAssets[i].AuthoredId))
                    problems.Add($"duplicate appraisal calibration id '{calibrationAssets[i].AuthoredId}'");
            }

            SocialEvidenceAsset[] evidenceAssets = index.SocialEvidence;
            for (int i = 0; i < evidenceAssets.Length; i++)
            {
                if (evidenceAssets[i] == null)
                {
                    problems.Add($"social evidence slot {i} is empty");
                    continue;
                }
                foreach (string problem in evidenceAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(evidenceAssets[i].AuthoredId))
                    problems.Add($"duplicate social evidence id '{evidenceAssets[i].AuthoredId}'");
            }

            SocialPressureAsset[] pressureAssets = index.SocialPressures;
            for (int i = 0; i < pressureAssets.Length; i++)
            {
                if (pressureAssets[i] == null)
                {
                    problems.Add($"social pressure slot {i} is empty");
                    continue;
                }
                foreach (string problem in pressureAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(pressureAssets[i].AuthoredId))
                    problems.Add($"duplicate social pressure id '{pressureAssets[i].AuthoredId}'");
            }

            EmploymentDefinitionAsset[] employmentAssets = index.Employments;
            for (int i = 0; i < employmentAssets.Length; i++)
            {
                if (employmentAssets[i] == null)
                {
                    problems.Add($"employment slot {i} is empty");
                    continue;
                }
                foreach (string problem in employmentAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(employmentAssets[i].AuthoredId))
                    problems.Add($"duplicate employment id '{employmentAssets[i].AuthoredId}'");
            }

            CommitmentAccountabilityPolicyAsset[] accountabilityPolicyAssets = index.CommitmentAccountabilityPolicies;
            for (int i = 0; i < accountabilityPolicyAssets.Length; i++)
            {
                if (accountabilityPolicyAssets[i] == null)
                {
                    problems.Add($"accountability policy slot {i} is empty");
                    continue;
                }
                foreach (string problem in accountabilityPolicyAssets[i].Validate()) problems.Add(problem);
                if (!seenIds.Add(accountabilityPolicyAssets[i].AuthoredId))
                    problems.Add($"duplicate accountability policy id '{accountabilityPolicyAssets[i].AuthoredId}'");
            }

            return problems;
        }

    }
}
