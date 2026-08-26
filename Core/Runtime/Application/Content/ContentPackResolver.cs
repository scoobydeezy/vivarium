using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;

namespace Vivarium.Application.Content
{
    public sealed class ContentOverrideDeclaration
    {
        public ContentOverrideDeclaration(
            ContentDefinitionFamily family,
            AuthoredId definitionId,
            string expectedSourcePackId)
        {
            Key = new ContentDefinitionKey(family, definitionId);
            ExpectedSourcePackId = expectedSourcePackId ?? throw new ArgumentNullException(nameof(expectedSourcePackId));
        }

        public ContentDefinitionKey Key { get; }
        public string ExpectedSourcePackId { get; }
    }

    public sealed class ContentPackContribution
    {
        public ContentPackContribution(
            string packId,
            string displayName,
            int packVersion,
            DefinitionSet definitions,
            IReadOnlyList<ContentOverrideDeclaration> overrides = null)
        {
            if (string.IsNullOrWhiteSpace(packId)) throw new ArgumentException("Pack id is required.", nameof(packId));
            if (packVersion <= 0) throw new ArgumentOutOfRangeException(nameof(packVersion));
            PackId = packId;
            DisplayName = displayName ?? string.Empty;
            PackVersion = packVersion;
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            var copy = new ContentOverrideDeclaration[overrides?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++) copy[i] = overrides[i];
            Overrides = copy;
        }

        public string PackId { get; }
        public string DisplayName { get; }
        public int PackVersion { get; }
        public DefinitionSet Definitions { get; }
        public IReadOnlyList<ContentOverrideDeclaration> Overrides { get; }
    }

    public sealed class ResolvedContentManifest
    {
        public ResolvedContentManifest(IReadOnlyList<ResolvedPackEntry> packsInLoadOrder)
        {
            var copy = new ResolvedPackEntry[packsInLoadOrder.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = packsInLoadOrder[i];
            PacksInLoadOrder = copy;
        }

        public IReadOnlyList<ResolvedPackEntry> PacksInLoadOrder { get; }
    }

    public readonly struct ResolvedPackEntry
    {
        public ResolvedPackEntry(string packId, int packVersion)
        {
            PackId = packId;
            PackVersion = packVersion;
        }

        public string PackId { get; }
        public int PackVersion { get; }
    }

    public sealed class ResolvedOverride
    {
        public ResolvedOverride(ContentDefinitionKey key, string replacedPackId, string winningPackId)
        {
            Key = key;
            ReplacedPackId = replacedPackId;
            WinningPackId = winningPackId;
        }

        public ContentDefinitionKey Key { get; }
        public string ReplacedPackId { get; }
        public string WinningPackId { get; }
    }

    public sealed class ResolvedContent
    {
        internal ResolvedContent(
            DefinitionCatalog catalog,
            ResolvedContentManifest manifest,
            IReadOnlyList<ResolvedOverride> overrides)
        {
            Catalog = catalog;
            Manifest = manifest;
            var copy = new ResolvedOverride[overrides.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = overrides[i];
            Overrides = copy;
        }

        public DefinitionCatalog Catalog { get; }
        public ResolvedContentManifest Manifest { get; }
        public IReadOnlyList<ResolvedOverride> Overrides { get; }
    }

    /// <summary>Resolves immutable pack contributions into the one validated runtime catalog.</summary>
    public static class ContentPackResolver
    {
        public static readonly AuthoredId DecisionImportancePolicyId =
            new AuthoredId("policy.decision_importance");

        public static ResolvedContent Resolve(IReadOnlyList<ContentPackContribution> packsInLoadOrder)
        {
            if (packsInLoadOrder == null || packsInLoadOrder.Count == 0)
                throw new InvalidOperationException("At least one content pack is required.");

            var states = CreateStates(packsInLoadOrder);
            var resolvedOverrides = new List<ResolvedOverride>();
            var traits = Overlay(states, ContentDefinitionFamily.Trait, s => s.Pack.Definitions.Traits, resolvedOverrides);
            var needs = Overlay(states, ContentDefinitionFamily.Need, s => s.Pack.Definitions.Needs, resolvedOverrides);
            var activities = Overlay(states, ContentDefinitionFamily.Activity, s => s.Pack.Definitions.Activities, resolvedOverrides);
            var decisions = Overlay(states, ContentDefinitionFamily.Decision, s => s.Pack.Definitions.Decisions, resolvedOverrides);
            var interventions = Overlay(states, ContentDefinitionFamily.Intervention, s => s.Pack.Definitions.Interventions, resolvedOverrides);
            var locationKinds = Overlay(states, ContentDefinitionFamily.LocationKind, s => s.Pack.Definitions.LocationKinds, resolvedOverrides);
            var commitmentTemplates = Overlay(states, ContentDefinitionFamily.CommitmentTemplate, s => s.Pack.Definitions.CommitmentTemplates, resolvedOverrides);
            var calibrations = Overlay(states, ContentDefinitionFamily.AppraisalCalibration, s => s.Pack.Definitions.AppraisalCalibrations, resolvedOverrides);
            var evidence = Overlay(states, ContentDefinitionFamily.SocialEvidence, s => s.Pack.Definitions.SocialEvidence, resolvedOverrides);
            var accountability = Overlay(states, ContentDefinitionFamily.CommitmentAccountabilityPolicy, s => s.Pack.Definitions.CommitmentAccountabilityPolicies, resolvedOverrides);
            var pressures = Overlay(states, ContentDefinitionFamily.SocialPressure, s => s.Pack.Definitions.SocialPressures, resolvedOverrides);
            var employments = Overlay(states, ContentDefinitionFamily.Employment, s => s.Pack.Definitions.EmploymentDefinitions, resolvedOverrides);
            DecisionImportancePolicyDefinition importance = OverlayDecisionImportance(states, resolvedOverrides);

            BindCommitmentTemplateAccountabilityPolicies(commitmentTemplates, accountability);
            BindEmploymentAccountabilityPolicies(employments, accountability);

            for (int i = 0; i < states.Count; i++)
            {
                foreach (KeyValuePair<ContentDefinitionKey, ContentOverrideDeclaration> declaration in states[i].Declarations)
                {
                    if (!states[i].Consumed.Contains(declaration.Key))
                        throw new InvalidOperationException(
                            $"Pack '{states[i].Pack.PackId}' declares override '{declaration.Key}', but no matching earlier definition was replaced.");
                }
            }

            var builder = new DefinitionCatalog.Builder { ContentVersion = packsInLoadOrder[0].PackVersion };
            AddSorted(traits, builder.Add);
            AddSorted(needs, builder.Add);
            AddSorted(activities, builder.Add);
            AddSorted(decisions, builder.Add);
            AddSorted(interventions, builder.Add);
            AddSorted(locationKinds, builder.Add);
            AddSorted(commitmentTemplates, builder.Add);
            AddSorted(calibrations, builder.Add);
            AddSorted(evidence, builder.Add);
            AddSorted(accountability, builder.Add);
            AddSorted(pressures, builder.Add);
            AddSorted(employments, builder.Add);
            if (importance != null) builder.SetDecisionImportancePolicy(importance);

            DefinitionCatalog catalog = builder.Build();
            IReadOnlyList<string> errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0)
                throw new InvalidOperationException("Content validation failed after pack resolution: " + string.Join("; ", errors));

            var manifest = new ResolvedPackEntry[packsInLoadOrder.Count];
            for (int i = 0; i < manifest.Length; i++)
                manifest[i] = new ResolvedPackEntry(packsInLoadOrder[i].PackId, packsInLoadOrder[i].PackVersion);
            return new ResolvedContent(catalog, new ResolvedContentManifest(manifest), resolvedOverrides);
        }

        private static List<PackState> CreateStates(IReadOnlyList<ContentPackContribution> packs)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var states = new List<PackState>(packs.Count);
            for (int i = 0; i < packs.Count; i++)
            {
                ContentPackContribution pack = packs[i] ?? throw new InvalidOperationException($"Content pack slot {i} is empty.");
                if (!ids.Add(pack.PackId)) throw new InvalidOperationException($"Pack id '{pack.PackId}' is loaded more than once.");
                states.Add(new PackState(pack));
            }
            return states;
        }

        private static Dictionary<AuthoredId, Winner<T>> Overlay<T>(
            IReadOnlyList<PackState> states,
            ContentDefinitionFamily family,
            Func<PackState, IReadOnlyDictionary<AuthoredId, T>> select,
            List<ResolvedOverride> resolvedOverrides)
        {
            var result = new Dictionary<AuthoredId, Winner<T>>();
            for (int p = 0; p < states.Count; p++)
            {
                PackState state = states[p];
                var keys = new List<AuthoredId>(select(state).Keys);
                keys.Sort();
                for (int i = 0; i < keys.Count; i++)
                {
                    AuthoredId id = keys[i];
                    T value = select(state)[id];
                    if (!result.TryGetValue(id, out Winner<T> previous))
                    {
                        result.Add(id, new Winner<T>(value, state.Pack.PackId));
                        continue;
                    }

                    var key = new ContentDefinitionKey(family, id);
                    RequireOverride(state, key, previous.PackId);
                    result[id] = new Winner<T>(value, state.Pack.PackId);
                    resolvedOverrides.Add(new ResolvedOverride(key, previous.PackId, state.Pack.PackId));
                }
            }
            return result;
        }

        private static DecisionImportancePolicyDefinition OverlayDecisionImportance(
            IReadOnlyList<PackState> states,
            List<ResolvedOverride> resolvedOverrides)
        {
            DecisionImportancePolicyDefinition result = null;
            string source = null;
            var key = new ContentDefinitionKey(ContentDefinitionFamily.DecisionImportancePolicy, DecisionImportancePolicyId);
            for (int i = 0; i < states.Count; i++)
            {
                DecisionImportancePolicyDefinition current = states[i].Pack.Definitions.DecisionImportancePolicy;
                if (current == null) continue;
                if (result != null)
                {
                    RequireOverride(states[i], key, source);
                    resolvedOverrides.Add(new ResolvedOverride(key, source, states[i].Pack.PackId));
                }
                result = current;
                source = states[i].Pack.PackId;
            }
            return result;
        }

        /// <summary>
        /// Employment contributions retain accountability policy identity until every pack has been
        /// overlaid. Binding here makes references honor load-order replacement and allows a pack to
        /// reference a policy contributed by an earlier pack.
        /// </summary>
        private static void BindEmploymentAccountabilityPolicies(
            Dictionary<AuthoredId, Winner<EmploymentDefinition>> employments,
            Dictionary<AuthoredId, Winner<CommitmentAccountabilityPolicy>> accountability)
        {
            var ids = new List<AuthoredId>(employments.Keys);
            ids.Sort();
            for (int e = 0; e < ids.Count; e++)
            {
                Winner<EmploymentDefinition> winner = employments[ids[e]];
                EmploymentDefinition definition = winner.Value;
                var patterns = new EmploymentObligationPattern[definition.ObligationPatterns.Count];
                for (int p = 0; p < patterns.Length; p++)
                {
                    EmploymentObligationPattern pattern = definition.ObligationPatterns[p];
                    CommitmentAccountabilityPolicy policy = pattern.AccountabilityPolicy;
                    if (policy.Id.IsSet && accountability.TryGetValue(policy.Id, out Winner<CommitmentAccountabilityPolicy> resolved))
                        policy = resolved.Value;
                    patterns[p] = new EmploymentObligationPattern(
                        pattern.Id,
                        pattern.CommitmentKind,
                        pattern.CycleLengthDays,
                        pattern.ActiveDaysMask,
                        pattern.StartMinuteOfDay,
                        pattern.Duration,
                        pattern.Priority,
                        pattern.ActivityDefinitionId,
                        pattern.StartWindow,
                        policy);
                }
                employments[ids[e]] = new Winner<EmploymentDefinition>(
                    new EmploymentDefinition(definition.Id, definition.RoleId, patterns),
                    winner.PackId);
            }
        }

        private static void BindCommitmentTemplateAccountabilityPolicies(
            Dictionary<AuthoredId, Winner<CommitmentTemplate>> templates,
            Dictionary<AuthoredId, Winner<CommitmentAccountabilityPolicy>> accountability)
        {
            var ids = new List<AuthoredId>(templates.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                Winner<CommitmentTemplate> winner = templates[ids[i]];
                CommitmentTemplate template = winner.Value;
                CommitmentAccountabilityPolicy policy = template.AccountabilityPolicy;
                if (policy.Id.IsSet && accountability.TryGetValue(
                    policy.Id, out Winner<CommitmentAccountabilityPolicy> resolved))
                    policy = resolved.Value;
                templates[ids[i]] = new Winner<CommitmentTemplate>(
                    new CommitmentTemplate(
                        template.Id,
                        template.CommitmentKind,
                        template.CycleLengthDays,
                        template.ActiveDaysMask,
                        template.StartMinuteOfDay,
                        template.Duration,
                        template.LocationId,
                        template.Priority,
                        template.ActivityDefinitionId,
                        template.StartWindow,
                        template.Source,
                        policy,
                        template.Stakeholders),
                    winner.PackId);
            }
        }

        private static void RequireOverride(PackState state, ContentDefinitionKey key, string actualSource)
        {
            if (!state.Declarations.TryGetValue(key, out ContentOverrideDeclaration declaration))
                throw new InvalidOperationException(
                    $"Pack '{state.Pack.PackId}' collides with '{actualSource}' at '{key}' without declaring an override.");
            if (!string.Equals(declaration.ExpectedSourcePackId, actualSource, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Pack '{state.Pack.PackId}' expected '{key}' from '{declaration.ExpectedSourcePackId}', but the current winner is '{actualSource}'.");
            state.Consumed.Add(key);
        }

        private static void AddSorted<T>(Dictionary<AuthoredId, Winner<T>> values, Func<T, DefinitionCatalog.Builder> add)
        {
            var keys = new List<AuthoredId>(values.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++) add(values[keys[i]].Value);
        }

        private readonly struct Winner<T>
        {
            public Winner(T value, string packId) { Value = value; PackId = packId; }
            public T Value { get; }
            public string PackId { get; }
        }

        private sealed class PackState
        {
            public PackState(ContentPackContribution pack)
            {
                Pack = pack;
                Declarations = new Dictionary<ContentDefinitionKey, ContentOverrideDeclaration>();
                Consumed = new HashSet<ContentDefinitionKey>();
                for (int i = 0; i < pack.Overrides.Count; i++)
                {
                    ContentOverrideDeclaration declaration = pack.Overrides[i] ??
                        throw new InvalidOperationException($"Pack '{pack.PackId}' contains an empty override declaration.");
                    if (!Declarations.TryAdd(declaration.Key, declaration))
                        throw new InvalidOperationException($"Pack '{pack.PackId}' declares override '{declaration.Key}' more than once.");
                }
            }

            public ContentPackContribution Pack { get; }
            public Dictionary<ContentDefinitionKey, ContentOverrideDeclaration> Declarations { get; }
            public HashSet<ContentDefinitionKey> Consumed { get; }
        }
    }
}
