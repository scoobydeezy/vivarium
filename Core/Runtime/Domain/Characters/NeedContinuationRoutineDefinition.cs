using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    /// <summary>An authored ongoing Activity whose appeal may compete with a reserve Need.</summary>
    public readonly struct NeedContinuationCandidateDefinition
    {
        public NeedContinuationCandidateDefinition(AuthoredId activityDefinitionId, AuthoredId interestId)
        {
            if (!activityDefinitionId.IsSet || !interestId.IsSet)
                throw new ArgumentException("Continuation candidates need stable Activity and Interest ids.");
            ActivityDefinitionId = activityDefinitionId;
            InterestId = interestId;
        }

        public AuthoredId ActivityDefinitionId { get; }
        public AuthoredId InterestId { get; }
    }

    /// <summary>
    /// A fallback-capable Rest/Continue branch. Continue moves the watched reserve threshold by one
    /// authored step; it never installs a timer or polls the Need.
    /// </summary>
    public sealed class NeedContinuationRoutineDefinition
    {
        private readonly NeedContinuationCandidateDefinition[] _candidates;

        public NeedContinuationRoutineDefinition(
            AuthoredId decisionDefinitionId,
            AuthoredId restOptionId,
            AuthoredId continueOptionId,
            long activationThreshold,
            long continuationThresholdStep,
            IReadOnlyList<NeedContinuationCandidateDefinition> candidates)
        {
            if (!decisionDefinitionId.IsSet || !restOptionId.IsSet || !continueOptionId.IsSet)
                throw new ArgumentException("A continuation routine needs stable Decision and Option ids.");
            if (restOptionId == continueOptionId)
                throw new ArgumentException("Rest and Continue must be distinct Options.");
            if (continuationThresholdStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(continuationThresholdStep));
            if (candidates == null || candidates.Count == 0)
                throw new ArgumentException("A continuation routine needs at least one Activity candidate.", nameof(candidates));

            DecisionDefinitionId = decisionDefinitionId;
            RestOptionId = restOptionId;
            ContinueOptionId = continueOptionId;
            ActivationThreshold = activationThreshold;
            ContinuationThresholdStep = continuationThresholdStep;
            _candidates = new NeedContinuationCandidateDefinition[candidates.Count];
            var activities = new HashSet<AuthoredId>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!activities.Add(candidates[i].ActivityDefinitionId))
                    throw new ArgumentException(
                        $"Continuation Activity '{candidates[i].ActivityDefinitionId}' is duplicated.", nameof(candidates));
                _candidates[i] = candidates[i];
            }
            Array.Sort(_candidates, (a, b) => a.ActivityDefinitionId.CompareTo(b.ActivityDefinitionId));
        }

        public AuthoredId DecisionDefinitionId { get; }
        public AuthoredId RestOptionId { get; }
        public AuthoredId ContinueOptionId { get; }
        public long ActivationThreshold { get; }
        public long ContinuationThresholdStep { get; }
        public IReadOnlyList<NeedContinuationCandidateDefinition> Candidates => _candidates;

        public bool TryGetCandidate(AuthoredId activityDefinitionId, out NeedContinuationCandidateDefinition candidate)
        {
            for (int i = 0; i < _candidates.Length; i++)
            {
                if (_candidates[i].ActivityDefinitionId == activityDefinitionId)
                {
                    candidate = _candidates[i];
                    return true;
                }
            }
            candidate = default;
            return false;
        }
    }
}
