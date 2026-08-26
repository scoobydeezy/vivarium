using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Need definition and its optional routines.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Need Definition", fileName = "need_")]
    public sealed class NeedDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "need.";
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private long minValue;
        [SerializeField] private long maxValue = 10000;
        [SerializeField] private long ratePerMinuteNumerator;
        [SerializeField] private long ratePerMinuteDenominator = 1;
        [SerializeField] private int[] behaviouralThresholds = new int[0];
        [SerializeField] private RestRoutineEntry restRoutine;
        [SerializeField] private SatisfactionRoutineEntry satisfactionRoutine;
        [SerializeField] private RecreationRoutineEntry recreationRoutine;
        [SerializeField] private SocializingRoutineEntry socializingRoutine;
        [SerializeField] private NeedContinuationRoutineEntry continuationRoutine;

        public string AuthoredId => authoredId;

        public NeedDefinition ToDefinition() => new NeedDefinition(
            new AuthoredId(authoredId),
            displayName,
            minValue,
            maxValue,
            ratePerMinuteNumerator,
            ratePerMinuteDenominator <= 0 ? 1 : ratePerMinuteDenominator,
            ToThresholds(),
            restRoutine.IsConfigured ? restRoutine.ToDefinition() : null,
            satisfactionRoutine.IsConfigured ? satisfactionRoutine.ToDefinition() : null,
            recreationRoutine.IsConfigured ? recreationRoutine.ToDefinition() : null,
            socializingRoutine.IsConfigured ? socializingRoutine.ToDefinition() : null,
            continuationRoutine.IsConfigured ? continuationRoutine.ToDefinition() : null);

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("need."))
                yield return $"{name}: Need ids should be namespaced 'need.<something>'.";
            if (string.IsNullOrWhiteSpace(displayName))
                yield return $"{name}: display name is required.";
            if (minValue >= maxValue)
                yield return $"{name}: minimum must be lower than maximum.";
            if (ratePerMinuteDenominator <= 0)
                yield return $"{name}: rate denominator must be positive.";
        }

        private long[] ToThresholds()
        {
            var result = new long[behaviouralThresholds?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = behaviouralThresholds[i];
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
            new AuthoredId(activityDefinitionId), activationThreshold, satisfactionOffset);
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
            var result = new RecreationCandidateDefinition[candidates?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = candidates[i].ToDefinition();
            return new RecreationRoutineDefinition(
                new AuthoredId(decisionDefinitionId), activationThreshold, satisfactionOffset, result);
        }
    }

    [System.Serializable]
    public struct RecreationCandidateEntry
    {
        public string optionId;
        public string activityDefinitionId;
        public string interestId;

        public RecreationCandidateDefinition ToDefinition() => new RecreationCandidateDefinition(
            new AuthoredId(optionId), new AuthoredId(activityDefinitionId), new AuthoredId(interestId));
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
            var result = new SocialInvitationPlanDefinition[plans?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = plans[i].ToDefinition();
            return new SocialInvitationRoutineDefinition(
                new AuthoredId(decisionDefinitionId), new AuthoredId(acceptOptionId), result);
        }
    }

    [System.Serializable]
    public struct SocialInvitationPlanEntry
    {
        public string activityDefinitionId;
        public string interestId;

        public SocialInvitationPlanDefinition ToDefinition() => new SocialInvitationPlanDefinition(
            new AuthoredId(activityDefinitionId), new AuthoredId(interestId));
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
            var result = new NeedContinuationCandidateDefinition[candidates?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = candidates[i].ToDefinition();
            return new NeedContinuationRoutineDefinition(
                new AuthoredId(decisionDefinitionId),
                new AuthoredId(restOptionId),
                new AuthoredId(continueOptionId),
                activationThreshold,
                continuationThresholdStep,
                result);
        }
    }

    [System.Serializable]
    public struct NeedContinuationCandidateEntry
    {
        public string activityDefinitionId;
        public string interestId;

        public NeedContinuationCandidateDefinition ToDefinition() =>
            new NeedContinuationCandidateDefinition(
                new AuthoredId(activityDefinitionId), new AuthoredId(interestId));
    }
}
