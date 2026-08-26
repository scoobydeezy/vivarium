using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Domain.Social;
using Vivarium.Domain.History;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Unity.Authoring
{
    /// <summary>
    /// Collects authoring assets and converts them into an immutable
    /// <see cref="DefinitionCatalog"/> (§41).
    /// <para>
    /// This is the conversion step in the content pipeline: authoring assets → validation and conversion
    /// → catalog → simulation. Everything downstream of <see cref="Build"/> is Unity-free.
    /// </para>
    /// <para>
    /// Rebuilding the catalog is how hot reload works (§42): a new catalog changes future runtime
    /// entities, while ones already in flight keep the values they snapshotted (§42.1).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Vivarium/Content Pack", fileName = "ContentPack")]
    public sealed class ContentPackAsset : ScriptableObject
    {
        [Tooltip("Bumped whenever content changes. Recorded in saves and traces (§38, §53).")]
        [SerializeField] private int contentVersion = 1;

        [Header("Decision importance")]
        [SerializeField] private int decisionAdmissionFloor = 6500;
        [SerializeField] private int decisionPrioritizedFeedFloor = 6500;
        [SerializeField] private int decisionNormalFeedFloor = 7000;
        [SerializeField] private int decisionAutoHoldFloor = 7000;

        [SerializeField] private TraitDefinitionAsset[] traits = new TraitDefinitionAsset[0];

        [Header("Placeholder inline content")]
        [Tooltip("Needs authored inline until they get their own asset type.")]
        [SerializeField] private NeedEntry[] needs = new NeedEntry[0];

        [SerializeField] private ActivityEntry[] activities = new ActivityEntry[0];

        [SerializeField] private string[] locationKindIds = new string[0];

        [SerializeField] private DecisionEntry[] decisions = new DecisionEntry[0];

        [SerializeField] private InterventionEntry[] interventions = new InterventionEntry[0];

        [Header("Social model")]
        [SerializeField] private AppraisalCalibrationEntry[] appraisalCalibrations = new AppraisalCalibrationEntry[0];
        [SerializeField] private SocialEvidenceEntry[] socialEvidence = new SocialEvidenceEntry[0];
        [SerializeField] private CommitmentAccountabilityPolicyEntry[] commitmentAccountabilityPolicies = new CommitmentAccountabilityPolicyEntry[0];
        [SerializeField] private EmploymentDefinitionEntry[] employmentDefinitions = new EmploymentDefinitionEntry[0];
        [SerializeField] private SocialPressureEntry[] socialPressures = new SocialPressureEntry[0];

        public int ContentVersion => contentVersion;

        /// <summary>
        /// Builds the catalog. Throws if validation fails, so a broken content pack cannot reach
        /// gameplay (§42).
        /// </summary>
        public DefinitionCatalog Build()
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };
            builder.SetDecisionImportancePolicy(
                new DecisionImportancePolicyDefinition(
                    decisionAdmissionFloor,
                    decisionPrioritizedFeedFloor,
                    decisionNormalFeedFloor,
                    decisionAutoHoldFloor));

            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i] != null)
                {
                    builder.Add(traits[i].ToDefinition());
                }
            }

            for (int i = 0; i < needs.Length; i++)
            {
                builder.Add(needs[i].ToDefinition());
            }

            for (int i = 0; i < activities.Length; i++)
            {
                builder.Add(activities[i].ToDefinition());
            }

            for (int i = 0; i < locationKindIds.Length; i++)
            {
                builder.Add(new LocationKindDefinition(new AuthoredId(locationKindIds[i]), locationKindIds[i]));
            }

            for (int i = 0; i < decisions.Length; i++)
            {
                builder.Add(decisions[i].ToDefinition());
            }

            for (int i = 0; i < interventions.Length; i++)
            {
                builder.Add(interventions[i].ToDefinition());
            }

            for (int i = 0; i < appraisalCalibrations.Length; i++)
            {
                builder.Add(appraisalCalibrations[i].ToDefinition());
            }
            for (int i = 0; i < socialEvidence.Length; i++)
            {
                builder.Add(socialEvidence[i].ToDefinition());
            }
            var accountabilityPolicies = new Dictionary<AuthoredId, CommitmentAccountabilityPolicy>();
            for (int i = 0; i < commitmentAccountabilityPolicies.Length; i++)
            {
                CommitmentAccountabilityPolicy policy = commitmentAccountabilityPolicies[i].ToDefinition();
                accountabilityPolicies.Add(policy.Id, policy);
                builder.Add(policy);
            }
            for (int i = 0; i < employmentDefinitions.Length; i++)
            {
                builder.Add(employmentDefinitions[i].ToDefinition(accountabilityPolicies));
            }
            for (int i = 0; i < socialPressures.Length; i++)
            {
                builder.Add(socialPressures[i].ToDefinition());
            }

            // The system-provided activities the architecture assumes exist (§29.2, invariant 39).
            if (!ContainsActivity(WellKnownActivities.Waiting))
            {
                builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            }

            if (!ContainsActivity(WellKnownActivities.Traveling))
            {
                builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            }

            if (!ContainsActivity(WellKnownActivities.Sleeping))
            {
                builder.Add(new ActivityDefinition(WellKnownActivities.Sleeping, "Sleeping", SimDuration.FromHours(8), false));
            }

            if (!ContainsNeed(WellKnownNeeds.Energy))
            {
                builder.Add(new NeedDefinition(
                    WellKnownNeeds.Energy,
                    "Energy",
                    0,
                    10000,
                    -10,
                    1,
                    new long[] { 2000, 8000 },
                    new NeedRestRoutineDefinition(
                        WellKnownActivities.Sleeping,
                        GroupKinds.Household,
                        2000,
                        8000,
                        20)));
            }

            DefinitionCatalog catalog = builder.Build();

            IReadOnlyList<string> errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "Content validation failed: " + string.Join("; ", errors));
            }

            return catalog;
        }

        /// <summary>Collects every authoring-time problem without throwing. Used by the editor menu.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seenIds = new HashSet<string>();

            if (decisionAdmissionFloor < 0 || decisionAdmissionFloor > SignalNumeric.Scale)
            {
                problems.Add($"Decision admission floor must be between 0 and {SignalNumeric.Scale}");
            }
            if (decisionPrioritizedFeedFloor < decisionAdmissionFloor ||
                decisionNormalFeedFloor < decisionPrioritizedFeedFloor ||
                decisionAutoHoldFloor < decisionNormalFeedFloor ||
                decisionAutoHoldFloor > SignalNumeric.Scale)
            {
                problems.Add("Decision importance floors must satisfy Admission <= PrioritizedFeed <= NormalFeed <= AutoHold <= 10000.");
            }

            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i] == null)
                {
                    problems.Add($"trait slot {i} is empty");
                    continue;
                }

                foreach (string problem in traits[i].Validate())
                {
                    problems.Add(problem);
                }

                if (!seenIds.Add(traits[i].AuthoredId))
                {
                    problems.Add($"duplicate trait id '{traits[i].AuthoredId}'");
                }
            }

            for (int i = 0; i < decisions.Length; i++)
            {
                try
                {
                    DecisionDefinition definition = decisions[i].ToDefinition();
                    IReadOnlyList<string> reasoningProblems = DecisionReasoningProgramValidator.Validate(
                        definition.ReasoningProgram, definition.Options, DecisionSignalProviderIds.BuiltIns);
                    for (int p = 0; p < reasoningProblems.Count; p++)
                    {
                        problems.Add($"decision '{definition.Id}': {reasoningProblems[p]}");
                    }
                }
                catch (System.Exception error)
                {
                    problems.Add($"decision slot {i}: {error.Message}");
                }
            }

            return problems;
        }

        private bool ContainsActivity(AuthoredId id)
        {
            for (int i = 0; i < activities.Length; i++)
            {
                if (activities[i].authoredId == id.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsNeed(AuthoredId id)
        {
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].authoredId == id.Value)
                {
                    return true;
                }
            }

            return false;
        }

        [System.Serializable]
        public struct NeedEntry
        {
            public string authoredId;
            public string displayName;
            public long minValue;
            public long maxValue;
            public long ratePerMinuteNumerator;
            public long ratePerMinuteDenominator;
            public long[] behaviouralThresholds;
            public int[] behaviouralThresholdValues;
            public RestRoutineEntry restRoutine;
            public SatisfactionRoutineEntry satisfactionRoutine;
            public RecreationRoutineEntry recreationRoutine;
            public SocializingRoutineEntry socializingRoutine;
            public NeedContinuationRoutineEntry continuationRoutine;

            public Domain.Characters.NeedDefinition ToDefinition() => new Domain.Characters.NeedDefinition(
                new AuthoredId(authoredId),
                displayName,
                minValue,
                maxValue,
                ratePerMinuteNumerator,
                ratePerMinuteDenominator <= 0 ? 1 : ratePerMinuteDenominator,
                ToBehaviouralThresholds(),
                restRoutine.IsConfigured ? restRoutine.ToDefinition() : null,
                satisfactionRoutine.IsConfigured ? satisfactionRoutine.ToDefinition() : null,
                recreationRoutine.IsConfigured ? recreationRoutine.ToDefinition() : null,
                socializingRoutine.IsConfigured ? socializingRoutine.ToDefinition() : null,
                continuationRoutine.IsConfigured ? continuationRoutine.ToDefinition() : null);

            private long[] ToBehaviouralThresholds()
            {
                if (behaviouralThresholdValues == null || behaviouralThresholdValues.Length == 0)
                    return behaviouralThresholds ?? new long[0];

                var result = new long[behaviouralThresholdValues.Length];
                for (var index = 0; index < behaviouralThresholdValues.Length; index++)
                    result[index] = behaviouralThresholdValues[index];
                return result;
            }
        }

        [System.Serializable]
        public struct RestRoutineEntry
        {
            public string activityDefinitionId;
            public string locationGroupKindId;
            public long activationThreshold;
            public long recoveredThreshold;
            public long recoveryRateNumerator;
            public long recoveryRateDenominator;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(activityDefinitionId);

            public NeedRestRoutineDefinition ToDefinition() => new NeedRestRoutineDefinition(
                new AuthoredId(activityDefinitionId),
                new AuthoredId(locationGroupKindId),
                activationThreshold,
                recoveredThreshold,
                recoveryRateNumerator,
                recoveryRateDenominator <= 0 ? 1 : recoveryRateDenominator);
        }

        [System.Serializable]
        public struct SatisfactionRoutineEntry
        {
            public string activityDefinitionId;
            public long activationThreshold;
            public long satisfactionOffset;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(activityDefinitionId);

            public NeedSatisfactionRoutineDefinition ToDefinition() => new NeedSatisfactionRoutineDefinition(
                new AuthoredId(activityDefinitionId),
                activationThreshold,
                satisfactionOffset);
        }

        [System.Serializable]
        public struct RecreationRoutineEntry
        {
            public string decisionDefinitionId;
            public long activationThreshold;
            public long satisfactionOffset;
            public RecreationCandidateEntry[] candidates;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(decisionDefinitionId);

            public RecreationRoutineDefinition ToDefinition()
            {
                var definitions = new RecreationCandidateDefinition[candidates?.Length ?? 0];
                for (int i = 0; i < definitions.Length; i++) definitions[i] = candidates[i].ToDefinition();
                return new RecreationRoutineDefinition(
                    new AuthoredId(decisionDefinitionId),
                    activationThreshold,
                    satisfactionOffset,
                    definitions);
            }
        }

        [System.Serializable]
        public struct RecreationCandidateEntry
        {
            public string optionId;
            public string activityDefinitionId;
            public string interestId;

            public RecreationCandidateDefinition ToDefinition() => new RecreationCandidateDefinition(
                new AuthoredId(optionId),
                new AuthoredId(activityDefinitionId),
                new AuthoredId(interestId));
        }

        [System.Serializable]
        public struct SocializingRoutineEntry
        {
            public string activityDefinitionId;
            public long activationThreshold;
            public long satisfactionOffset;
            public int maxCandidates;
            public SocialInvitationRoutineEntry invitation;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(activityDefinitionId);

            public SocializingRoutineDefinition ToDefinition() => new SocializingRoutineDefinition(
                new AuthoredId(activityDefinitionId),
                activationThreshold,
                satisfactionOffset,
                maxCandidates <= 0 ? 4 : maxCandidates,
                invitation.IsConfigured ? invitation.ToDefinition() : null);
        }

        [System.Serializable]
        public struct SocialInvitationRoutineEntry
        {
            public string decisionDefinitionId;
            public string acceptOptionId;
            public SocialInvitationPlanEntry[] plans;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(decisionDefinitionId);

            public SocialInvitationRoutineDefinition ToDefinition()
            {
                var definitions = new SocialInvitationPlanDefinition[plans?.Length ?? 0];
                for (int i = 0; i < definitions.Length; i++) definitions[i] = plans[i].ToDefinition();
                return new SocialInvitationRoutineDefinition(
                    new AuthoredId(decisionDefinitionId),
                    new AuthoredId(acceptOptionId),
                    definitions);
            }
        }

        [System.Serializable]
        public struct SocialInvitationPlanEntry
        {
            public string activityDefinitionId;
            public string interestId;

            public SocialInvitationPlanDefinition ToDefinition() => new SocialInvitationPlanDefinition(
                new AuthoredId(activityDefinitionId),
                new AuthoredId(interestId));
        }

        [System.Serializable]
        public struct NeedContinuationRoutineEntry
        {
            public string decisionDefinitionId;
            public string restOptionId;
            public string continueOptionId;
            public long activationThreshold;
            public long continuationThresholdStep;
            public NeedContinuationCandidateEntry[] candidates;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(decisionDefinitionId);

            public NeedContinuationRoutineDefinition ToDefinition()
            {
                var definitions = new NeedContinuationCandidateDefinition[candidates?.Length ?? 0];
                for (int i = 0; i < definitions.Length; i++) definitions[i] = candidates[i].ToDefinition();
                return new NeedContinuationRoutineDefinition(
                    new AuthoredId(decisionDefinitionId),
                    new AuthoredId(restOptionId),
                    new AuthoredId(continueOptionId),
                    activationThreshold,
                    continuationThresholdStep,
                    definitions);
            }
        }

        [System.Serializable]
        public struct NeedContinuationCandidateEntry
        {
            public string activityDefinitionId;
            public string interestId;

            public NeedContinuationCandidateDefinition ToDefinition() =>
                new NeedContinuationCandidateDefinition(
                    new AuthoredId(activityDefinitionId),
                    new AuthoredId(interestId));
        }

        [System.Serializable]
        public struct ActivityEntry
        {
            public string authoredId;
            public string displayName;
            public int defaultDurationMinutes;
            public bool producesOutcome;
            public bool supportsInteractiveResolution;
            public bool isTravel;

            public ActivityDefinition ToDefinition() => new ActivityDefinition(
                new AuthoredId(authoredId),
                displayName,
                SimDuration.FromMinutes(defaultDurationMinutes),
                producesOutcome,
                supportsInteractiveResolution,
                isTravel);
        }

        [System.Serializable]
        public struct DecisionEntry
        {
            public string authoredId;
            public DecisionOptionEntry[] options;
            public int timeToResolveMinutes;
            public string conflictScopeKindId;
            public bool holdEligible;
            public DecisionDependencyEntry[] dependencies;
            public NeedThresholdTriggerEntry trigger;
            public DecisionInfluenceEntry[] influences;
            public DecisionActivityOutcomeEntry[] activityOutcomes;
            public SocialDecisionTriggerEntry socialTrigger;
            public bool commitmentConflictTrigger;
            public DecisionRelationshipOutcomeEntry[] relationshipOutcomes;
            public DecisionReasoningProgramEntry reasoningProgram;

            public DecisionDefinition ToDefinition()
            {
                var domainOptions = new DecisionOption[options?.Length ?? 0];
                for (int i = 0; i < domainOptions.Length; i++)
                {
                    domainOptions[i] = options[i].ToDefinition(i);
                }

                var domainInfluences = new DecisionInfluenceTemplate[influences?.Length ?? 0];
                for (int i = 0; i < domainInfluences.Length; i++)
                {
                    domainInfluences[i] = influences[i].ToDefinition();
                }

                var domainOutcomes = new DecisionActivityOutcome[activityOutcomes?.Length ?? 0];
                for (int i = 0; i < domainOutcomes.Length; i++)
                {
                    domainOutcomes[i] = activityOutcomes[i].ToDefinition();
                }

                var domainRelationshipOutcomes = new DecisionRelationshipOutcome[relationshipOutcomes?.Length ?? 0];
                for (int i = 0; i < domainRelationshipOutcomes.Length; i++)
                {
                    domainRelationshipOutcomes[i] = relationshipOutcomes[i].ToDefinition();
                }

                return new DecisionDefinition(
                    new AuthoredId(authoredId),
                    domainOptions,
                    SimDuration.FromMinutes(timeToResolveMinutes),
                    new AuthoredId(conflictScopeKindId),
                    holdEligible,
                    dependencyTemplates: ToDependencies(),
                    trigger: trigger.IsConfigured ? trigger.ToDefinition() : null,
                    influenceTemplates: domainInfluences,
                    activityOutcomes: domainOutcomes,
                    socialTrigger: socialTrigger.IsConfigured ? socialTrigger.ToDefinition() : null,
                    relationshipOutcomes: domainRelationshipOutcomes,
                    reasoningProgram: reasoningProgram.IsConfigured ? reasoningProgram.ToDefinition() : null,
                    commitmentConflictTrigger: commitmentConflictTrigger ? new CommitmentConflictDecisionTrigger() : null);
            }

            private DecisionDependencyKey[] ToDependencies()
            {
                var result = new DecisionDependencyKey[dependencies?.Length ?? 0];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = dependencies[i].ToDefinition();
                }

                return result;
            }
        }

        [System.Serializable]
        public struct DecisionDependencyEntry
        {
            public string contextKindId;

            public DecisionDependencyKey ToDefinition() =>
                new DecisionDependencyKey(new AuthoredId(contextKindId));
        }

        [System.Serializable]
        public struct DecisionOptionEntry
        {
            public string authoredId;
            public string labelId;
            public int orderIndex;
            public DecisionParameterValueEntry[] context;

            public DecisionOption ToDefinition(int fallbackOrder)
            {
                var values = new SortedDictionary<AuthoredId, DecisionParameterValue>();
                for (int i = 0; i < (context?.Length ?? 0); i++)
                {
                    var key = new AuthoredId(context[i].key);
                    if (values.ContainsKey(key))
                        throw new System.InvalidOperationException($"option '{authoredId}' declares context '{key}' twice");
                    values.Add(key, context[i].ToValue());
                }
                return new DecisionOption(
                    new AuthoredId(authoredId), new AuthoredId(labelId),
                    orderIndex < 0 ? fallbackOrder : orderIndex, values);
            }
        }

        [System.Serializable]
        public struct NeedThresholdTriggerEntry
        {
            public string needId;
            public long threshold;
            public string requiredActivityDefinitionId;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(needId);

            public NeedThresholdDecisionTrigger ToDefinition() =>
                new NeedThresholdDecisionTrigger(
                    new AuthoredId(needId),
                    threshold,
                    new AuthoredId(requiredActivityDefinitionId));
        }

        [System.Serializable]
        public struct DecisionInfluenceEntry
        {
            public string optionId;
            public string categoryId;
            public string labelId;
            public int dieSides;
            public InfluenceVisibility visibility;
            public bool subjectIsCharacter;

            public DecisionInfluenceTemplate ToDefinition() => new DecisionInfluenceTemplate(
                new AuthoredId(optionId),
                new AuthoredId(categoryId),
                new AuthoredId(labelId),
                new Die(dieSides),
                visibility,
                subjectIsCharacter);
        }

        [System.Serializable]
        public struct DecisionActivityOutcomeEntry
        {
            public string optionId;
            public string activityDefinitionId;
            public int durationMinutes;

            public DecisionActivityOutcome ToDefinition() => new DecisionActivityOutcome(
                new AuthoredId(optionId),
                new AuthoredId(activityDefinitionId),
                SimDuration.FromMinutes(durationMinutes));
        }

        [System.Serializable]
        public struct SocialDecisionTriggerEntry
        {
            public string pressureDefinitionId;
            public string lensId;
            public string positiveOptionId;
            public string negativeOptionId;
            public string categoryId;
            public string positiveLabelId;
            public string negativeLabelId;
            public InfluenceVisibility visibility;
            public AppraisalStrength minimumStrength;

            public bool IsConfigured => !string.IsNullOrWhiteSpace(pressureDefinitionId);

            public SocialInteractionDecisionTrigger ToDefinition() => new SocialInteractionDecisionTrigger(
                new AuthoredId(pressureDefinitionId),
                new AuthoredId(lensId),
                new SocialDecisionInfluenceSpec(
                    new AuthoredId(positiveOptionId),
                    new AuthoredId(negativeOptionId),
                    new AuthoredId(categoryId),
                    new AuthoredId(positiveLabelId),
                    new AuthoredId(negativeLabelId),
                    visibility),
                minimumStrength);
        }

        [System.Serializable]
        public struct DecisionRelationshipOutcomeEntry
        {
            public string optionId;
            public string channelId;
            public long delta;

            public DecisionRelationshipOutcome ToDefinition() => new DecisionRelationshipOutcome(
                new AuthoredId(optionId),
                new AuthoredId(channelId),
                delta);
        }

        [System.Serializable]
        public struct InterventionEntry
        {
            public string authoredId;
            public InterventionKind kind;
            public int cost;
            public InterventionResourceKind resourceKind;
            public int replacementDieSides;
            public int fixedResult;
            public int initialBalance;
            public int availabilityCap;
            public int refreshAmount;
            public long refreshPeriodMinutes;

            public InterventionDefinition ToDefinition() => new InterventionDefinition(
                new AuthoredId(authoredId),
                kind,
                cost,
                replacementDie: new Die(replacementDieSides, fixedResult),
                resourceKind: resourceKind,
                resourcePolicy: availabilityCap > 0
                    ? new Vivarium.Domain.PlayerAgency.InterventionResourcePolicy(
                        initialBalance, availabilityCap, refreshAmount,
                        new Vivarium.Domain.Time.SimDuration(refreshPeriodMinutes))
                    : default);
        }

        [System.Serializable]
        public struct AppraisalCalibrationEntry
        {
            public string authoredId;
            public int version;
            public AppraisalStrengthThresholdEntry[] thresholds;

            public AppraisalCalibrationProfile ToDefinition()
            {
                var result = new AppraisalStrengthThreshold[thresholds?.Length ?? 0];
                for (int i = 0; i < result.Length; i++) result[i] = thresholds[i].ToDefinition();
                return new AppraisalCalibrationProfile(new AuthoredId(authoredId), result, version);
            }
        }

        [System.Serializable]
        public struct AppraisalStrengthThresholdEntry
        {
            public long minimumMagnitude;
            public AppraisalStrength strength;
            public AppraisalStrengthThreshold ToDefinition() => new AppraisalStrengthThreshold(minimumMagnitude, strength);
        }

        [System.Serializable]
        public struct SocialEvidenceEntry
        {
            public string actionDefinitionId;
            public string explanationId;
            public SocialEvidenceMeasurementEntry[] measurements;

            public SocialEvidenceDefinition ToDefinition()
            {
                var result = new SocialEvidenceMeasurement[measurements?.Length ?? 0];
                for (int i = 0; i < result.Length; i++) result[i] = measurements[i].ToDefinition();
                return new SocialEvidenceDefinition(new AuthoredId(actionDefinitionId), result, new AuthoredId(explanationId));
            }
        }

        [System.Serializable]
        public struct SocialEvidenceMeasurementEntry
        {
            public string authoredId;
            public SocialLinearEntry[] projection;
            public long observedValue;
            public long noiseVariance;

            public SocialEvidenceMeasurement ToDefinition()
            {
                var result = new SocialLinearTerm[projection?.Length ?? 0];
                for (int i = 0; i < result.Length; i++) result[i] = projection[i].ToDefinition();
                return new SocialEvidenceMeasurement(new AuthoredId(authoredId), result, observedValue, noiseVariance);
            }
        }

        [System.Serializable]
        public struct CommitmentAccountabilityPolicyEntry
        {
            public string authoredId;
            public CommitmentConsequenceSetEntry defaultConsequences;
            public CommitmentOutcomeConsequenceEntry[] byOutcome;
            public CommitmentRoleConsequenceEntry[] byRole;
            public CommitmentAccountabilityOverrideEntry[] specificOverrides;

            public CommitmentAccountabilityPolicy ToDefinition()
            {
                var outcomes = new SortedDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>();
                for (int i = 0; i < (byOutcome?.Length ?? 0); i++)
                    outcomes[byOutcome[i].outcome] = byOutcome[i].consequences.ToDefinition();
                var roles = new SortedDictionary<StakeholderRole, CommitmentConsequenceSet>();
                for (int i = 0; i < (byRole?.Length ?? 0); i++)
                    roles[byRole[i].role] = byRole[i].consequences.ToDefinition();
                var overrides = new CommitmentAccountabilityOverride[specificOverrides?.Length ?? 0];
                for (int i = 0; i < overrides.Length; i++) overrides[i] = specificOverrides[i].ToDefinition();
                return new CommitmentAccountabilityPolicy(
                    defaultConsequences.ToDefinition(), outcomes, roles, overrides, new AuthoredId(authoredId));
            }
        }

        [System.Serializable]
        public struct CommitmentOutcomeConsequenceEntry
        {
            public CommitmentOutcomeKind outcome;
            public CommitmentConsequenceSetEntry consequences;
        }

        [System.Serializable]
        public struct EmploymentDefinitionEntry
        {
            public string authoredId;
            public string roleId;
            public EmploymentObligationPatternEntry[] obligationPatterns;

            public EmploymentDefinition ToDefinition(
                IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> accountabilityPolicies)
            {
                var patterns = new EmploymentObligationPattern[obligationPatterns?.Length ?? 0];
                for (int i = 0; i < patterns.Length; i++)
                {
                    patterns[i] = obligationPatterns[i].ToDefinition(accountabilityPolicies);
                }

                return new EmploymentDefinition(new AuthoredId(authoredId), new AuthoredId(roleId), patterns);
            }
        }

        [System.Serializable]
        public struct EmploymentObligationPatternEntry
        {
            public string authoredId;
            public string commitmentKindId;
            public int cycleLengthDays;
            public int activeDaysMask;
            public int startMinuteOfDay;
            public int durationMinutes;
            public int priority;
            public string activityDefinitionId;
            public int startWindowMinutes;
            public string accountabilityPolicyId;

            public EmploymentObligationPattern ToDefinition(
                IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> accountabilityPolicies)
            {
                CommitmentAccountabilityPolicy policy = null;
                if (!string.IsNullOrWhiteSpace(accountabilityPolicyId))
                {
                    var policyId = new AuthoredId(accountabilityPolicyId);
                    if (!accountabilityPolicies.TryGetValue(policyId, out policy))
                    {
                        throw new System.InvalidOperationException(
                            $"Employment obligation '{authoredId}' references unknown accountability policy '{policyId}'.");
                    }
                }

                return new EmploymentObligationPattern(
                    new AuthoredId(authoredId),
                    new AuthoredId(commitmentKindId),
                    cycleLengthDays,
                    activeDaysMask,
                    startMinuteOfDay,
                    SimDuration.FromMinutes(durationMinutes),
                    priority,
                    new AuthoredId(activityDefinitionId),
                    SimDuration.FromMinutes(startWindowMinutes),
                    policy);
            }
        }

        [System.Serializable]
        public struct CommitmentRoleConsequenceEntry
        {
            public StakeholderRole role;
            public CommitmentConsequenceSetEntry consequences;
        }

        [System.Serializable]
        public struct CommitmentAccountabilityOverrideEntry
        {
            public CommitmentOutcomeKind outcome;
            public StakeholderRole role;
            public bool matchPerceivedCause;
            public PerceivedCommitmentCause perceivedCause;
            public CommitmentConsequenceSetEntry consequences;
            public CommitmentAccountabilityOverride ToDefinition() => new CommitmentAccountabilityOverride(
                outcome, role, consequences.ToDefinition(),
                matchPerceivedCause ? (PerceivedCommitmentCause?)perceivedCause : null);
        }

        [System.Serializable]
        public struct CommitmentConsequenceSetEntry
        {
            public bool hasMemory;
            public string memoryKind;
            public string memoryExplanationId;
            public RetentionTier memoryRetentionTier;
            public string evidenceActionId;
            public AuthoredLongEntry[] channelDeltas;

            public CommitmentConsequenceSet ToDefinition()
            {
                var deltas = new SortedDictionary<AuthoredId, long>();
                for (int i = 0; i < (channelDeltas?.Length ?? 0); i++)
                    deltas[new AuthoredId(channelDeltas[i].authoredId)] = channelDeltas[i].value;
                return new CommitmentConsequenceSet(
                    hasMemory
                        ? new CommitmentMemoryConsequence(
                            new AuthoredId(memoryKind),
                            new AuthoredId(memoryExplanationId),
                            memoryRetentionTier)
                        : null,
                    new AuthoredId(evidenceActionId),
                    deltas);
            }
        }

        [System.Serializable]
        public struct AuthoredLongEntry
        {
            public string authoredId;
            public long value;
        }

        [System.Serializable]
        public struct SocialPressureEntry
        {
            public string authoredId;
            public SocialFactorRuleEntry[] rules;

            public SocialPressureDefinition ToDefinition()
            {
                var result = new SocialFactorRule[rules?.Length ?? 0];
                for (int i = 0; i < result.Length; i++) result[i] = rules[i].ToDefinition();
                return new SocialPressureDefinition(new AuthoredId(authoredId), result);
            }
        }

        [System.Serializable]
        public struct SocialFactorRuleEntry
        {
            public string lensId;
            public SocialFactorSourceKind sourceKind;
            public string sourceId;
            public long coefficient;
            public string explanationId;
            public string requiredContextId;

            public SocialFactorRule ToDefinition() => new SocialFactorRule(
                new AuthoredId(lensId),
                sourceKind,
                new AuthoredId(sourceId),
                coefficient,
                new AuthoredId(explanationId),
                new AuthoredId(requiredContextId));
        }
    }
}
