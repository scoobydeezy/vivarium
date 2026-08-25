using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    public readonly struct RecreationCandidateDefinition
    {
        public RecreationCandidateDefinition(
            AuthoredId optionId,
            AuthoredId activityDefinitionId,
            AuthoredId interestId)
        {
            if (!optionId.IsSet || !activityDefinitionId.IsSet || !interestId.IsSet)
                throw new ArgumentException("Recreation candidates need stable Option, Activity, and Interest ids.");
            OptionId = optionId;
            ActivityDefinitionId = activityDefinitionId;
            InterestId = interestId;
        }

        public AuthoredId OptionId { get; }
        public AuthoredId ActivityDefinitionId { get; }
        public AuthoredId InterestId { get; }
    }

    /// <summary>Content-backed discretionary routine with a truthful automatic fallback.</summary>
    public sealed class RecreationRoutineDefinition
    {
        private readonly RecreationCandidateDefinition[] _candidates;

        public RecreationRoutineDefinition(
            AuthoredId decisionDefinitionId,
            long activationThreshold,
            long satisfactionOffset,
            IReadOnlyList<RecreationCandidateDefinition> candidates)
        {
            if (!decisionDefinitionId.IsSet)
                throw new ArgumentException("A Recreation routine needs a Decision definition id.", nameof(decisionDefinitionId));
            if (candidates == null || candidates.Count < 2)
                throw new ArgumentException("A Recreation routine needs at least two authored candidates.", nameof(candidates));
            DecisionDefinitionId = decisionDefinitionId;
            ActivationThreshold = activationThreshold;
            SatisfactionOffset = satisfactionOffset;
            _candidates = new RecreationCandidateDefinition[candidates.Count];
            var optionIds = new HashSet<AuthoredId>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!optionIds.Add(candidates[i].OptionId))
                    throw new ArgumentException($"Recreation Option '{candidates[i].OptionId}' is duplicated.", nameof(candidates));
                _candidates[i] = candidates[i];
            }
            Array.Sort(_candidates, (a, b) => a.OptionId.CompareTo(b.OptionId));
        }

        public AuthoredId DecisionDefinitionId { get; }
        public long ActivationThreshold { get; }
        public long SatisfactionOffset { get; }
        public IReadOnlyList<RecreationCandidateDefinition> Candidates => _candidates;
    }
}
