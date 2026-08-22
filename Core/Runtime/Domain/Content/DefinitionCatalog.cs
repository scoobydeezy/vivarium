using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Spatial;

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
            IReadOnlyDictionary<AuthoredId, CommitmentTemplate> commitmentTemplates)
        {
            ContentVersion = contentVersion;
            Traits = traits;
            Needs = needs;
            Activities = activities;
            Decisions = decisions;
            Interventions = interventions;
            LocationKinds = locationKinds;
            CommitmentTemplates = commitmentTemplates;
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
                    _commitmentTemplates);
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

            return errors;
        }
    }
}
