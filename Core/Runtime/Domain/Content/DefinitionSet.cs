using System.Collections.Generic;
using System.Collections.ObjectModel;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;

namespace Vivarium.Domain.Content
{
    /// <summary>
    /// Immutable definitions contributed by one content source before catalog-wide resolution and
    /// validation. A set may be incomplete and may reference definitions supplied by another set.
    /// </summary>
    public sealed class DefinitionSet
    {
        internal DefinitionSet(
            IDictionary<AuthoredId, TraitDefinition> traits,
            IDictionary<AuthoredId, NeedDefinition> needs,
            IDictionary<AuthoredId, ActivityDefinition> activities,
            IDictionary<AuthoredId, DecisionDefinition> decisions,
            IDictionary<AuthoredId, InterventionDefinition> interventions,
            IDictionary<AuthoredId, LocationKindDefinition> locationKinds,
            IDictionary<AuthoredId, CommitmentTemplate> commitmentTemplates,
            IDictionary<AuthoredId, AppraisalCalibrationProfile> appraisalCalibrations,
            IDictionary<AuthoredId, SocialEvidenceDefinition> socialEvidence,
            IDictionary<AuthoredId, CommitmentAccountabilityPolicy> commitmentAccountabilityPolicies,
            IDictionary<AuthoredId, SocialPressureDefinition> socialPressures,
            IDictionary<AuthoredId, EmploymentDefinition> employmentDefinitions,
            DecisionImportancePolicyDefinition decisionImportancePolicy)
        {
            Traits = Snapshot(traits);
            Needs = Snapshot(needs);
            Activities = Snapshot(activities);
            Decisions = Snapshot(decisions);
            Interventions = Snapshot(interventions);
            LocationKinds = Snapshot(locationKinds);
            CommitmentTemplates = Snapshot(commitmentTemplates);
            AppraisalCalibrations = Snapshot(appraisalCalibrations);
            SocialEvidence = Snapshot(socialEvidence);
            CommitmentAccountabilityPolicies = Snapshot(commitmentAccountabilityPolicies);
            SocialPressures = Snapshot(socialPressures);
            EmploymentDefinitions = Snapshot(employmentDefinitions);
            DecisionImportancePolicy = decisionImportancePolicy;
        }

        public IReadOnlyDictionary<AuthoredId, TraitDefinition> Traits { get; }
        public IReadOnlyDictionary<AuthoredId, NeedDefinition> Needs { get; }
        public IReadOnlyDictionary<AuthoredId, ActivityDefinition> Activities { get; }
        public IReadOnlyDictionary<AuthoredId, DecisionDefinition> Decisions { get; }
        public IReadOnlyDictionary<AuthoredId, InterventionDefinition> Interventions { get; }
        public IReadOnlyDictionary<AuthoredId, LocationKindDefinition> LocationKinds { get; }
        public IReadOnlyDictionary<AuthoredId, CommitmentTemplate> CommitmentTemplates { get; }
        public IReadOnlyDictionary<AuthoredId, AppraisalCalibrationProfile> AppraisalCalibrations { get; }
        public IReadOnlyDictionary<AuthoredId, SocialEvidenceDefinition> SocialEvidence { get; }
        public IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> CommitmentAccountabilityPolicies { get; }
        public IReadOnlyDictionary<AuthoredId, SocialPressureDefinition> SocialPressures { get; }
        public IReadOnlyDictionary<AuthoredId, EmploymentDefinition> EmploymentDefinitions { get; }
        public DecisionImportancePolicyDefinition DecisionImportancePolicy { get; }

        private static IReadOnlyDictionary<AuthoredId, T> Snapshot<T>(IDictionary<AuthoredId, T> source) =>
            new ReadOnlyDictionary<AuthoredId, T>(new Dictionary<AuthoredId, T>(source));
    }
}
