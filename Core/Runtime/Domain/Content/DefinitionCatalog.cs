using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Content
{
    /// <summary>
    /// The immutable definition catalog the simulation consumes (§41).
    /// <para>
    /// Unity ScriptableObjects are authoring tools, not authoritative Domain types. The pipeline is
    /// authoring assets → validation/conversion → this catalog → simulation. Nothing downstream of here
    /// knows Unity exists.
    /// </para>
    /// <para>
    /// Immutable once built, so hot reload means <i>building a new catalog</i> (§42). Runtime entities
    /// already in flight keep the values they snapshotted (§42.1), so swapping the catalog changes the
    /// future without rewriting the present.
    /// </para>
    /// </summary>
    public sealed class DefinitionCatalog
    {
        private DefinitionCatalog(
            int contentVersion,
            IReadOnlyDictionary<AuthoredId, TraitDefinition> traits,
            IReadOnlyDictionary<AuthoredId, NeedDefinition> needs,
            IReadOnlyDictionary<AuthoredId, ActivityDefinition> activities,
            IReadOnlyDictionary<AuthoredId, DecisionDefinition> decisions,
            IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions,
            IReadOnlyDictionary<AuthoredId, LocationKindDefinition> locationKinds,
            IReadOnlyDictionary<AuthoredId, CommitmentTemplate> commitmentTemplates,
            IReadOnlyDictionary<AuthoredId, AppraisalCalibrationProfile> appraisalCalibrations,
            IReadOnlyDictionary<AuthoredId, SocialEvidenceDefinition> socialEvidence,
            IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> commitmentAccountabilityPolicies,
            IReadOnlyDictionary<AuthoredId, SocialPressureDefinition> socialPressures,
            IReadOnlyDictionary<AuthoredId, EmploymentDefinition> employmentDefinitions,
            DecisionImportancePolicyDefinition decisionImportancePolicy)
        {
            ContentVersion = contentVersion;
            Traits = traits;
            Needs = needs;
            Activities = activities;
            Decisions = decisions;
            Interventions = interventions;
            LocationKinds = locationKinds;
            CommitmentTemplates = commitmentTemplates;
            AppraisalCalibrations = appraisalCalibrations;
            SocialEvidence = socialEvidence;
            CommitmentAccountabilityPolicies = commitmentAccountabilityPolicies;
            SocialPressures = socialPressures;
            EmploymentDefinitions = employmentDefinitions;
            DecisionImportancePolicy = decisionImportancePolicy;
        }

        /// <summary>Recorded in saves and traces so version-scoped reproduction is diagnosable (§39.1, §53).</summary>
        public int ContentVersion { get; }

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

        /// <summary>Mutable builder. Validate before building — see <see cref="ContentValidator"/>.</summary>
        public sealed class Builder
        {
            private readonly Dictionary<AuthoredId, TraitDefinition> _traits = new Dictionary<AuthoredId, TraitDefinition>();
            private readonly Dictionary<AuthoredId, NeedDefinition> _needs = new Dictionary<AuthoredId, NeedDefinition>();
            private readonly Dictionary<AuthoredId, ActivityDefinition> _activities = new Dictionary<AuthoredId, ActivityDefinition>();
            private readonly Dictionary<AuthoredId, DecisionDefinition> _decisions = new Dictionary<AuthoredId, DecisionDefinition>();
            private readonly Dictionary<AuthoredId, InterventionDefinition> _interventions = new Dictionary<AuthoredId, InterventionDefinition>();
            private readonly Dictionary<AuthoredId, LocationKindDefinition> _locationKinds = new Dictionary<AuthoredId, LocationKindDefinition>();
            private readonly Dictionary<AuthoredId, CommitmentTemplate> _commitmentTemplates = new Dictionary<AuthoredId, CommitmentTemplate>();
            private readonly Dictionary<AuthoredId, AppraisalCalibrationProfile> _appraisalCalibrations = new Dictionary<AuthoredId, AppraisalCalibrationProfile>();
            private readonly Dictionary<AuthoredId, SocialEvidenceDefinition> _socialEvidence = new Dictionary<AuthoredId, SocialEvidenceDefinition>();
            private readonly Dictionary<AuthoredId, CommitmentAccountabilityPolicy> _commitmentAccountabilityPolicies = new Dictionary<AuthoredId, CommitmentAccountabilityPolicy>();
            private readonly Dictionary<AuthoredId, SocialPressureDefinition> _socialPressures = new Dictionary<AuthoredId, SocialPressureDefinition>();
            private readonly Dictionary<AuthoredId, EmploymentDefinition> _employmentDefinitions = new Dictionary<AuthoredId, EmploymentDefinition>();
            private readonly List<string> _errors = new List<string>();
            private DecisionImportancePolicyDefinition _decisionImportancePolicy;

            public int ContentVersion { get; set; } = 1;

            /// <summary>Duplicate-id and missing-reference errors found while adding definitions (§42).</summary>
            public IReadOnlyList<string> Errors => _errors;

            public Builder Add(TraitDefinition definition) => AddTo(_traits, definition.Id, definition, "trait");

            public Builder Add(NeedDefinition definition) => AddTo(_needs, definition.Id, definition, "need");

            public Builder Add(ActivityDefinition definition) => AddTo(_activities, definition.Id, definition, "activity");

            public Builder Add(DecisionDefinition definition) => AddTo(_decisions, definition.Id, definition, "decision");

            public Builder Add(InterventionDefinition definition) => AddTo(_interventions, definition.Id, definition, "intervention");

            public Builder Add(LocationKindDefinition definition) => AddTo(_locationKinds, definition.Id, definition, "location kind");

            public Builder Add(CommitmentTemplate definition) => AddTo(_commitmentTemplates, definition.Id, definition, "commitment template");

            public Builder Add(AppraisalCalibrationProfile definition) => AddTo(_appraisalCalibrations, definition.Id, definition, "appraisal calibration");

            public Builder Add(SocialEvidenceDefinition definition) => AddTo(_socialEvidence, definition.ActionDefinitionId, definition, "social evidence");

            public Builder Add(CommitmentAccountabilityPolicy definition) =>
                AddTo(_commitmentAccountabilityPolicies, definition.Id, definition, "commitment accountability policy");

            public Builder Add(SocialPressureDefinition definition) => AddTo(_socialPressures, definition.Id, definition, "social pressure");

            public Builder Add(EmploymentDefinition definition) => AddTo(_employmentDefinitions, definition.Id, definition, "employment");

            public Builder SetDecisionImportancePolicy(DecisionImportancePolicyDefinition definition)
            {
                if (_decisionImportancePolicy != null)
                    _errors.Add("decision importance policy is declared more than once");
                else
                    _decisionImportancePolicy = definition ?? throw new ArgumentNullException(nameof(definition));
                return this;
            }

            public DefinitionCatalog Build()
            {
                if (_errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Content validation failed before entering gameplay (§42): " + string.Join("; ", _errors.ToArray()));
                }

                return new DefinitionCatalog(
                    ContentVersion,
                    _traits,
                    _needs,
                    _activities,
                    _decisions,
                    _interventions,
                    _locationKinds,
                    _commitmentTemplates,
                    _appraisalCalibrations,
                    _socialEvidence,
                    _commitmentAccountabilityPolicies,
                    _socialPressures,
                    _employmentDefinitions,
                    _decisionImportancePolicy);
            }

            private Builder AddTo<TDefinition>(Dictionary<AuthoredId, TDefinition> target, AuthoredId id, TDefinition definition, string kind)
            {
                if (target.ContainsKey(id))
                {
                    _errors.Add($"duplicate {kind} id '{id}'");
                    return this;
                }

                target.Add(id, definition);
                return this;
            }
        }
    }

    /// <summary>
    /// Catalog-wide validation (§42).
    /// <para>
    /// Duplicate ids, missing references, invalid ranges, and dependency errors must be caught
    /// <b>before entering gameplay</b>, not discovered as a null reference three simulated years in.
    /// </para>
    /// </summary>
    public static class ContentValidator
    {
        public static IReadOnlyList<string> Validate(DefinitionCatalog catalog)
        {
            var errors = new List<string>();
            var recreationDecisions = new HashSet<AuthoredId>();

            foreach (KeyValuePair<AuthoredId, NeedDefinition> pair in catalog.Needs)
            {
                NeedDefinition need = pair.Value;
                if (need.MinValue >= need.MaxValue)
                {
                    errors.Add($"need '{need.Id}' has an empty range [{need.MinValue}..{need.MaxValue}]");
                }

                if (need.DefaultRateDenominator <= 0)
                {
                    errors.Add($"need '{need.Id}' has a non-positive rate denominator");
                }

                for (int i = 0; i < need.BehaviouralThresholds.Count; i++)
                {
                    long threshold = need.BehaviouralThresholds[i];
                    if (threshold < need.MinValue || threshold > need.MaxValue)
                    {
                        errors.Add($"need '{need.Id}' threshold {threshold} falls outside its range");
                    }
                }

                NeedRestRoutineDefinition rest = need.RestRoutine;
                if (rest != null)
                {
                    if (!catalog.Activities.ContainsKey(rest.ActivityDefinitionId))
                        errors.Add($"need '{need.Id}' rest routine references unknown activity '{rest.ActivityDefinitionId}'");
                    else if (rest.RecoveryRateNumerator > 0)
                    {
                        long recoveryDelta = rest.RecoveredThreshold - need.MinValue;
                        long minimumMinutes = recoveryDelta <= 0
                            ? 0
                            : (recoveryDelta * rest.RecoveryRateDenominator + rest.RecoveryRateNumerator - 1) /
                                rest.RecoveryRateNumerator;
                        if (catalog.Activities[rest.ActivityDefinitionId].DefaultDuration.TotalMinutes < minimumMinutes)
                            errors.Add($"need '{need.Id}' recovery activity is too short to recover from its minimum value");
                    }
                    if (rest.ActivationThreshold < need.MinValue || rest.ActivationThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' rest activation threshold falls outside its range");
                    if (rest.RecoveredThreshold < need.MinValue || rest.RecoveredThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' recovered threshold falls outside its range");
                    if (need.DefaultRateNumerator >= 0)
                        errors.Add($"need '{need.Id}' rest routine requires a negative ordinary rate");
                    if (rest.RecoveryRateNumerator <= 0)
                        errors.Add($"need '{need.Id}' rest routine requires a positive recovery rate");
                    if (rest.ActivationThreshold >= rest.RecoveredThreshold)
                        errors.Add($"need '{need.Id}' rest activation threshold must be below its recovered threshold");
                    if (!ContainsThreshold(need.BehaviouralThresholds, rest.ActivationThreshold) ||
                        !ContainsThreshold(need.BehaviouralThresholds, rest.RecoveredThreshold))
                        errors.Add($"need '{need.Id}' rest thresholds must both be declared behavioural thresholds");
                }

                NeedSatisfactionRoutineDefinition satisfaction = need.SatisfactionRoutine;
                if (satisfaction != null)
                {
                    if (!catalog.Activities.ContainsKey(satisfaction.ActivityDefinitionId))
                        errors.Add($"need '{need.Id}' satisfaction routine references unknown activity '{satisfaction.ActivityDefinitionId}'");
                    if (need.DefaultRateNumerator <= 0)
                        errors.Add($"need '{need.Id}' satisfaction routine requires a positive ordinary rate");
                    if (satisfaction.ActivationThreshold < need.MinValue || satisfaction.ActivationThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' satisfaction activation threshold falls outside its range");
                    if (!ContainsThreshold(need.BehaviouralThresholds, satisfaction.ActivationThreshold))
                        errors.Add($"need '{need.Id}' satisfaction activation threshold must be a declared behavioural threshold");
                    if (need.MaxValue + satisfaction.SatisfactionOffset >= satisfaction.ActivationThreshold)
                        errors.Add($"need '{need.Id}' satisfaction offset must rearm the routine below its activation threshold even from maximum");
                }

                RecreationRoutineDefinition recreation = need.RecreationRoutine;
                if (recreation != null)
                {
                    if (!recreationDecisions.Add(recreation.DecisionDefinitionId))
                        errors.Add($"Recreation decision '{recreation.DecisionDefinitionId}' is assigned to more than one Need");
                    if (catalog.DecisionImportancePolicy == null)
                        errors.Add($"need '{need.Id}' Recreation routine requires a Decision Importance policy");
                    if (need.DefaultRateNumerator <= 0)
                        errors.Add($"need '{need.Id}' Recreation routine requires a positive ordinary rate");
                    if (recreation.ActivationThreshold < need.MinValue || recreation.ActivationThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' Recreation activation threshold falls outside its range");
                    if (!ContainsThreshold(need.BehaviouralThresholds, recreation.ActivationThreshold))
                        errors.Add($"need '{need.Id}' Recreation activation threshold must be a declared behavioural threshold");
                    if (need.MaxValue + recreation.SatisfactionOffset >= recreation.ActivationThreshold)
                        errors.Add($"need '{need.Id}' Recreation satisfaction offset must rearm below its activation threshold");
                    if (!catalog.Decisions.TryGetValue(recreation.DecisionDefinitionId, out DecisionDefinition recreationDecision))
                    {
                        errors.Add($"need '{need.Id}' Recreation routine references unknown decision '{recreation.DecisionDefinitionId}'");
                    }
                    else
                    {
                        if (recreationDecision.ReasoningProgram == null)
                            errors.Add($"need '{need.Id}' Recreation decision requires compiled reasoning");
                        for (int c = 0; c < recreation.Candidates.Count; c++)
                        {
                            RecreationCandidateDefinition candidate = recreation.Candidates[c];
                            if (!catalog.Activities.ContainsKey(candidate.ActivityDefinitionId))
                                errors.Add($"need '{need.Id}' Recreation candidate references unknown activity '{candidate.ActivityDefinitionId}'");
                            bool optionExists = false;
                            for (int o = 0; o < recreationDecision.Options.Count; o++)
                                if (recreationDecision.Options[o].Id == candidate.OptionId) optionExists = true;
                            if (!optionExists)
                                errors.Add($"need '{need.Id}' Recreation candidate references unknown Option '{candidate.OptionId}'");
                        }
                    }
                }

                SocializingRoutineDefinition socializing = need.SocializingRoutine;
                if (socializing != null)
                {
                    if (!catalog.Activities.ContainsKey(socializing.ActivityDefinitionId))
                        errors.Add($"need '{need.Id}' Socializing routine references unknown activity '{socializing.ActivityDefinitionId}'");
                    if (need.DefaultRateNumerator <= 0)
                        errors.Add($"need '{need.Id}' Socializing routine requires a positive ordinary rate");
                    if (socializing.ActivationThreshold < need.MinValue || socializing.ActivationThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' Socializing activation threshold falls outside its range");
                    if (!ContainsThreshold(need.BehaviouralThresholds, socializing.ActivationThreshold))
                        errors.Add($"need '{need.Id}' Socializing activation threshold must be a declared behavioural threshold");
                    if (need.MaxValue + socializing.SatisfactionOffset >= socializing.ActivationThreshold)
                        errors.Add($"need '{need.Id}' Socializing satisfaction offset must rearm below its activation threshold");
                    SocialInvitationRoutineDefinition invitation = socializing.Invitation;
                    if (invitation != null)
                    {
                        if (!catalog.Decisions.TryGetValue(
                                invitation.DecisionDefinitionId,
                                out DecisionDefinition invitationDecision))
                        {
                            errors.Add(
                                $"need '{need.Id}' Social invitation references unknown decision '{invitation.DecisionDefinitionId}'");
                        }
                        else
                        {
                            bool accepts = false;
                            for (int o = 0; o < invitationDecision.Options.Count; o++)
                                if (invitationDecision.Options[o].Id == invitation.AcceptOptionId) accepts = true;
                            if (!accepts)
                                errors.Add(
                                    $"need '{need.Id}' Social invitation references unknown accept Option '{invitation.AcceptOptionId}'");
                            if (invitationDecision.Options.Count != 2)
                                errors.Add(
                                    $"need '{need.Id}' Social invitation v0 requires exactly two Decision Options");
                            if (invitationDecision.ReasoningProgram == null)
                                errors.Add(
                                    $"need '{need.Id}' Social invitation decision requires compiled reasoning");
                        }
                        for (int p = 0; p < invitation.Plans.Count; p++)
                        {
                            if (!catalog.Activities.ContainsKey(invitation.Plans[p].ActivityDefinitionId))
                                errors.Add(
                                    $"need '{need.Id}' Social invitation plan references unknown Activity '{invitation.Plans[p].ActivityDefinitionId}'");
                        }
                    }
                }

                NeedContinuationRoutineDefinition continuation = need.ContinuationRoutine;
                if (continuation != null)
                {
                    if (catalog.DecisionImportancePolicy == null)
                        errors.Add($"need '{need.Id}' continuation routine requires a Decision Importance policy");
                    if (need.RestRoutine == null)
                        errors.Add($"need '{need.Id}' continuation routine requires a rest routine");
                    if (need.DefaultRateNumerator >= 0)
                        errors.Add($"need '{need.Id}' continuation routine requires a decreasing Need");
                    if (continuation.ActivationThreshold < need.MinValue ||
                        continuation.ActivationThreshold > need.MaxValue)
                        errors.Add($"need '{need.Id}' continuation activation threshold falls outside its range");
                    if (!ContainsThreshold(need.BehaviouralThresholds, continuation.ActivationThreshold))
                        errors.Add($"need '{need.Id}' continuation activation threshold must be declared behavioural");
                    if (need.RestRoutine != null &&
                        continuation.ActivationThreshold != need.RestRoutine.ActivationThreshold)
                        errors.Add($"need '{need.Id}' continuation and rest activation thresholds must match");
                    long firstRearm = continuation.ActivationThreshold - continuation.ContinuationThresholdStep;
                    if (firstRearm < need.MinValue || !ContainsThreshold(need.BehaviouralThresholds, firstRearm))
                        errors.Add($"need '{need.Id}' continuation step must reach a declared lower threshold");

                    if (!catalog.Decisions.TryGetValue(
                            continuation.DecisionDefinitionId,
                            out DecisionDefinition continuationDecision))
                    {
                        errors.Add(
                            $"need '{need.Id}' continuation routine references unknown decision '{continuation.DecisionDefinitionId}'");
                    }
                    else
                    {
                        bool hasRest = false;
                        bool hasContinue = false;
                        for (int o = 0; o < continuationDecision.Options.Count; o++)
                        {
                            hasRest |= continuationDecision.Options[o].Id == continuation.RestOptionId;
                            hasContinue |= continuationDecision.Options[o].Id == continuation.ContinueOptionId;
                        }
                        if (!hasRest || !hasContinue)
                            errors.Add($"need '{need.Id}' continuation routine references unknown Rest/Continue Options");
                        if (continuationDecision.Options.Count != 2)
                            errors.Add($"need '{need.Id}' continuation v0 requires exactly two Decision Options");
                        if (continuationDecision.ReasoningProgram == null)
                            errors.Add($"need '{need.Id}' continuation decision requires compiled reasoning");
                        if (continuationDecision.HoldEligible)
                            errors.Add($"need '{need.Id}' continuation decision cannot be Hold-eligible without a context deadline");
                        if (continuationDecision.ActivityOutcomes.Count > 0)
                            errors.Add($"need '{need.Id}' continuation consequences are owned by the routine service");
                    }
                    for (int c = 0; c < continuation.Candidates.Count; c++)
                    {
                        if (!catalog.Activities.ContainsKey(continuation.Candidates[c].ActivityDefinitionId))
                            errors.Add(
                                $"need '{need.Id}' continuation candidate references unknown Activity '{continuation.Candidates[c].ActivityDefinitionId}'");
                    }
                }
            }

            foreach (KeyValuePair<AuthoredId, DecisionDefinition> pair in catalog.Decisions)
            {
                DecisionDefinition decision = pair.Value;
                var seenOptions = new HashSet<string>();
                for (int i = 0; i < decision.Options.Count; i++)
                {
                    string optionId = decision.Options[i].Id.Value;
                    if (!seenOptions.Add(optionId))
                    {
                        errors.Add($"decision '{decision.Id}' declares option '{optionId}' twice");
                    }
                }

                if (decision.TimeToResolve.IsNegative)
                {
                    errors.Add($"decision '{decision.Id}' has a negative resolve delay");
                }

                if (decision.Trigger != null && !catalog.Needs.ContainsKey(decision.Trigger.NeedId))
                {
                    errors.Add($"decision '{decision.Id}' trigger references unknown need '{decision.Trigger.NeedId}'");
                }
                if (decision.Trigger != null && decision.Trigger.RequiredActivityDefinitionId.IsSet &&
                    !catalog.Activities.ContainsKey(decision.Trigger.RequiredActivityDefinitionId))
                    errors.Add($"decision '{decision.Id}' trigger references unknown required activity '{decision.Trigger.RequiredActivityDefinitionId}'");

                for (int i = 0; i < decision.InfluenceTemplates.Count; i++)
                {
                    if (!seenOptions.Contains(decision.InfluenceTemplates[i].OptionId.Value))
                    {
                        errors.Add($"decision '{decision.Id}' influence references unknown option '{decision.InfluenceTemplates[i].OptionId}'");
                    }
                }

                for (int i = 0; i < decision.ActivityOutcomes.Count; i++)
                {
                    DecisionActivityOutcome outcome = decision.ActivityOutcomes[i];
                    if (!seenOptions.Contains(outcome.OptionId.Value))
                    {
                        errors.Add($"decision '{decision.Id}' outcome references unknown option '{outcome.OptionId}'");
                    }
                    if (!catalog.Activities.ContainsKey(outcome.ActivityDefinitionId))
                    {
                        errors.Add($"decision '{decision.Id}' outcome references unknown activity '{outcome.ActivityDefinitionId}'");
                    }
                }

                if (decision.SocialTrigger != null)
                {
                    if (!catalog.SocialPressures.ContainsKey(decision.SocialTrigger.PressureDefinitionId))
                    {
                        errors.Add($"decision '{decision.Id}' social trigger references unknown pressure '{decision.SocialTrigger.PressureDefinitionId}'");
                    }
                    if (!seenOptions.Contains(decision.SocialTrigger.InfluenceSpec.PositiveOptionId.Value) ||
                        !seenOptions.Contains(decision.SocialTrigger.InfluenceSpec.NegativeOptionId.Value))
                    {
                        errors.Add($"decision '{decision.Id}' social influence references an unknown positive/negative option");
                    }
                }

                if (decision.SocialTrigger != null && decision.ReasoningProgram != null)
                {
                    errors.Add($"decision '{decision.Id}' cannot use both legacy SocialTrigger and compiled reasoning");
                }
                int triggerCount = (decision.Trigger == null ? 0 : 1) +
                    (decision.SocialTrigger == null ? 0 : 1) +
                    (decision.CommitmentConflictTrigger == null ? 0 : 1);
                if (triggerCount > 1)
                {
                    errors.Add($"decision '{decision.Id}' declares more than one generation trigger");
                }
                if (decision.CommitmentConflictTrigger != null)
                {
                    if (decision.Options.Count != 2)
                        errors.Add($"decision '{decision.Id}' commitment-conflict v0 requires exactly two Option templates");
                    if (decision.ReasoningProgram == null)
                        errors.Add($"decision '{decision.Id}' commitment-conflict requires compiled reasoning");
                    if (decision.ActivityOutcomes.Count > 0)
                        errors.Add($"decision '{decision.Id}' commitment-conflict consequences must mutate Commitment intent, not authored Activities");
                }
                IReadOnlyList<string> reasoningErrors = DecisionReasoningProgramValidator.Validate(
                    decision.ReasoningProgram, decision.Options, DecisionSignalProviderIds.BuiltIns);
                for (int i = 0; i < reasoningErrors.Count; i++)
                {
                    errors.Add($"decision '{decision.Id}': {reasoningErrors[i]}");
                }

                for (int i = 0; i < decision.RelationshipOutcomes.Count; i++)
                {
                    if (!seenOptions.Contains(decision.RelationshipOutcomes[i].OptionId.Value))
                    {
                        errors.Add($"decision '{decision.Id}' relationship outcome references unknown option '{decision.RelationshipOutcomes[i].OptionId}'");
                    }
                }
            }

            foreach (KeyValuePair<AuthoredId, CommitmentTemplate> pair in catalog.CommitmentTemplates)
            {
                CommitmentTemplate template = pair.Value;
                if (template.ActiveDaysMask == 0)
                {
                    errors.Add($"commitment template '{template.Id}' never occurs (empty day mask)");
                }

                if (template.ActivityDefinitionId.IsSet && !catalog.Activities.ContainsKey(template.ActivityDefinitionId))
                {
                    errors.Add($"commitment template '{template.Id}' references unknown activity '{template.ActivityDefinitionId}'");
                }
            }

            foreach (KeyValuePair<AuthoredId, EmploymentDefinition> pair in catalog.EmploymentDefinitions)
            {
                EmploymentDefinition definition = pair.Value;
                for (int i = 0; i < definition.ObligationPatterns.Count; i++)
                {
                    EmploymentObligationPattern pattern = definition.ObligationPatterns[i];
                    if (pattern.ActiveDaysMask == 0)
                        errors.Add($"employment '{definition.Id}' obligation '{pattern.Id}' never occurs (empty day mask)");
                    if (!catalog.Activities.ContainsKey(pattern.ActivityDefinitionId))
                        errors.Add($"employment '{definition.Id}' obligation '{pattern.Id}' references unknown activity '{pattern.ActivityDefinitionId}'");
                    if (pattern.AccountabilityPolicy.Id.IsSet &&
                        !catalog.CommitmentAccountabilityPolicies.ContainsKey(pattern.AccountabilityPolicy.Id))
                        errors.Add($"employment '{definition.Id}' obligation '{pattern.Id}' references unknown accountability policy '{pattern.AccountabilityPolicy.Id}'");
                }
            }

            foreach (KeyValuePair<AuthoredId, AppraisalCalibrationProfile> pair in catalog.AppraisalCalibrations)
            {
                AppraisalCalibrationProfile profile = pair.Value;
                long previous = -1;
                for (int i = 0; i < profile.Thresholds.Count; i++)
                {
                    if (profile.Thresholds[i].MinimumMagnitude <= previous)
                    {
                        errors.Add($"appraisal calibration '{profile.Id}' thresholds must be strictly increasing");
                        break;
                    }
                    previous = profile.Thresholds[i].MinimumMagnitude;
                }
            }

            foreach (KeyValuePair<AuthoredId, SocialEvidenceDefinition> pair in catalog.SocialEvidence)
            {
                SocialEvidenceDefinition evidence = pair.Value;
                if (evidence.Measurements.Count == 0)
                {
                    errors.Add($"social evidence '{evidence.ActionDefinitionId}' has no measurements");
                }
            }

            foreach (KeyValuePair<AuthoredId, CommitmentAccountabilityPolicy> pair in catalog.CommitmentAccountabilityPolicies)
            {
                ValidateConsequences(pair.Key, pair.Value.Default, catalog, errors);
                foreach (KeyValuePair<CommitmentOutcomeKind, CommitmentConsequenceSet> rule in pair.Value.ByOutcome)
                    ValidateConsequences(pair.Key, rule.Value, catalog, errors);
                foreach (KeyValuePair<StakeholderRole, CommitmentConsequenceSet> rule in pair.Value.ByRole)
                    ValidateConsequences(pair.Key, rule.Value, catalog, errors);
                for (int i = 0; i < pair.Value.SpecificOverrides.Count; i++)
                    ValidateConsequences(pair.Key, pair.Value.SpecificOverrides[i].Consequences, catalog, errors);
            }

            return errors;
        }

        private static bool ContainsThreshold(IReadOnlyList<long> thresholds, long expected)
        {
            for (int i = 0; i < thresholds.Count; i++)
                if (thresholds[i] == expected) return true;
            return false;
        }

        private static void ValidateConsequences(
            AuthoredId policyId,
            CommitmentConsequenceSet consequences,
            DefinitionCatalog catalog,
            List<string> errors)
        {
            if (consequences.EvidenceActionId.IsSet && !catalog.SocialEvidence.ContainsKey(consequences.EvidenceActionId))
                errors.Add($"commitment accountability policy '{policyId}' references unknown social evidence '{consequences.EvidenceActionId}'");
        }
    }
}
