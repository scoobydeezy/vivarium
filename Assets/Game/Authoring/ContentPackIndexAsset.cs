using UnityEngine;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Machine-maintained, build-included inventory for one content pack.</summary>
    public sealed class ContentPackIndexAsset : ScriptableObject
    {
        [SerializeField] private ContentPackManifestAsset manifest;
        [SerializeField] private TraitDefinitionAsset[] traits = new TraitDefinitionAsset[0];
        [SerializeField] private NeedDefinitionAsset[] needs = new NeedDefinitionAsset[0];
        [SerializeField] private ActivityDefinitionAsset[] activities = new ActivityDefinitionAsset[0];
        [SerializeField] private DecisionDefinitionAsset[] decisions = new DecisionDefinitionAsset[0];
        [SerializeField] private InterventionDefinitionAsset[] interventions = new InterventionDefinitionAsset[0];
        [SerializeField] private LocationKindDefinitionAsset[] locationKinds = new LocationKindDefinitionAsset[0];
        [SerializeField] private AppraisalCalibrationAsset[] appraisalCalibrations = new AppraisalCalibrationAsset[0];
        [SerializeField] private SocialEvidenceAsset[] socialEvidence = new SocialEvidenceAsset[0];
        [SerializeField] private DecisionImportancePolicyAsset decisionImportancePolicy;
        [SerializeField] private CommitmentAccountabilityPolicyAsset[] commitmentAccountabilityPolicies =
            new CommitmentAccountabilityPolicyAsset[0];
        [SerializeField] private EmploymentDefinitionAsset[] employments = new EmploymentDefinitionAsset[0];
        [SerializeField] private SocialPressureAsset[] socialPressures = new SocialPressureAsset[0];
        [SerializeField, HideInInspector] private string bakeFingerprint = string.Empty;

        public ContentPackManifestAsset Manifest => manifest;
        public TraitDefinitionAsset[] Traits => traits;
        public NeedDefinitionAsset[] Needs => needs;
        public ActivityDefinitionAsset[] Activities => activities;
        public DecisionDefinitionAsset[] Decisions => decisions;
        public InterventionDefinitionAsset[] Interventions => interventions;
        public LocationKindDefinitionAsset[] LocationKinds => locationKinds;
        public AppraisalCalibrationAsset[] AppraisalCalibrations => appraisalCalibrations;
        public SocialEvidenceAsset[] SocialEvidence => socialEvidence;
        public DecisionImportancePolicyAsset DecisionImportancePolicy => decisionImportancePolicy;
        public CommitmentAccountabilityPolicyAsset[] CommitmentAccountabilityPolicies =>
            commitmentAccountabilityPolicies;
        public EmploymentDefinitionAsset[] Employments => employments;
        public SocialPressureAsset[] SocialPressures => socialPressures;
        public string BakeFingerprint => bakeFingerprint;
    }
}
