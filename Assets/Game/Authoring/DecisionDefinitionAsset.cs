using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one compiled Decision definition.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Decision Definition", fileName = "decision_")]
    public sealed class DecisionDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "decision.";
        [SerializeField] private DecisionOptionEntry[] options = new DecisionOptionEntry[0];
        [SerializeField] private int timeToResolveMinutes;
        [SerializeField] private string conflictScopeKindId;
        [SerializeField] private bool holdEligible;
        [SerializeField] private DecisionDependencyEntry[] dependencies = new DecisionDependencyEntry[0];
        [SerializeField] private NeedThresholdTriggerEntry trigger;
        [SerializeField] private DecisionInfluenceEntry[] influences = new DecisionInfluenceEntry[0];
        [SerializeField] private DecisionActivityOutcomeEntry[] activityOutcomes = new DecisionActivityOutcomeEntry[0];
        [SerializeField] private SocialDecisionTriggerEntry socialTrigger;
        [SerializeField] private bool commitmentConflictTrigger;
        [SerializeField] private DecisionRelationshipOutcomeEntry[] relationshipOutcomes =
            new DecisionRelationshipOutcomeEntry[0];
        [SerializeField] private DecisionReasoningProgramEntry reasoningProgram;

        public string AuthoredId => authoredId;

        public DecisionDefinition ToDefinition()
        {
            var domainOptions = new DecisionOption[options?.Length ?? 0];
            for (int i = 0; i < domainOptions.Length; i++) domainOptions[i] = options[i].ToDefinition(i);

            var domainInfluences = new DecisionInfluenceTemplate[influences?.Length ?? 0];
            for (int i = 0; i < domainInfluences.Length; i++)
                domainInfluences[i] = influences[i].ToDefinition();

            var domainOutcomes = new DecisionActivityOutcome[activityOutcomes?.Length ?? 0];
            for (int i = 0; i < domainOutcomes.Length; i++)
                domainOutcomes[i] = activityOutcomes[i].ToDefinition();

            var domainRelationshipOutcomes = new DecisionRelationshipOutcome[relationshipOutcomes?.Length ?? 0];
            for (int i = 0; i < domainRelationshipOutcomes.Length; i++)
                domainRelationshipOutcomes[i] = relationshipOutcomes[i].ToDefinition();

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
                commitmentConflictTrigger: commitmentConflictTrigger
                    ? new CommitmentConflictDecisionTrigger()
                    : null);
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                problems.Add($"{name}: authored id '{authoredId}' is incomplete.");
            if (!authoredId.StartsWith("decision."))
                problems.Add($"{name}: Decision ids should be namespaced 'decision.<something>'.");

            try
            {
                DecisionDefinition definition = ToDefinition();
                IReadOnlyList<string> reasoningProblems = DecisionReasoningProgramValidator.Validate(
                    definition.ReasoningProgram,
                    definition.Options,
                    DecisionSignalProviderIds.BuiltIns);
                for (int i = 0; i < reasoningProblems.Count; i++)
                    problems.Add($"{name}: {reasoningProblems[i]}");
            }
            catch (System.Exception error)
            {
                problems.Add($"{name}: {error.Message}");
            }
            return problems;
        }

        private DecisionDependencyKey[] ToDependencies()
        {
            var result = new DecisionDependencyKey[dependencies?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = dependencies[i].ToDefinition();
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
                    throw new System.InvalidOperationException(
                        $"option '{authoredId}' declares context '{key}' twice");
                values.Add(key, context[i].ToValue());
            }
            return new DecisionOption(
                new AuthoredId(authoredId),
                new AuthoredId(labelId),
                orderIndex < 0 ? fallbackOrder : orderIndex,
                values);
        }
    }

    [System.Serializable]
    public struct NeedThresholdTriggerEntry
    {
        public string needId;
        public long threshold;
        public string requiredActivityDefinitionId;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(needId);
        public NeedThresholdDecisionTrigger ToDefinition() => new NeedThresholdDecisionTrigger(
            new AuthoredId(needId), threshold, new AuthoredId(requiredActivityDefinitionId));
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
        public int minimumRepeatIntervalMinutes;
        public int minimumRelationshipAgeMinutes;
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
            minimumStrength,
            SimDuration.FromMinutes(minimumRepeatIntervalMinutes),
            SimDuration.FromMinutes(minimumRelationshipAgeMinutes));
    }

    [System.Serializable]
    public struct DecisionRelationshipOutcomeEntry
    {
        public string optionId;
        public string channelId;
        public long delta;
        public DecisionRelationshipOutcome ToDefinition() => new DecisionRelationshipOutcome(
            new AuthoredId(optionId), new AuthoredId(channelId), delta);
    }
}
