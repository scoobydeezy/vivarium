using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
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
            IReadOnlyDictionary<AuthoredId, SocialPressureDefinition> socialPressures)
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
            SocialPressures = socialPressures;
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

        public IReadOnlyDictionary<AuthoredId, SocialPressureDefinition> SocialPressures { get; }

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
            private readonly Dictionary<AuthoredId, SocialPressureDefinition> _socialPressures = new Dictionary<AuthoredId, SocialPressureDefinition>();
            private readonly List<string> _errors = new List<string>();

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

            public Builder Add(SocialPressureDefinition definition) => AddTo(_socialPressures, definition.Id, definition, "social pressure");

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
                    _socialPressures);
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

            return errors;
        }
    }
}
