using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    /// <summary>Content-backed ordinary Social satisfaction through a bounded shared-context partner.</summary>
    public sealed class SocializingRoutineDefinition
    {
        public const int MaximumCandidateLimit = 16;

        public SocializingRoutineDefinition(
            AuthoredId activityDefinitionId,
            long activationThreshold,
            long satisfactionOffset,
            int maxCandidates = 4)
        {
            if (!activityDefinitionId.IsSet)
                throw new ArgumentException("A Socializing routine needs an Activity definition id.", nameof(activityDefinitionId));
            if (maxCandidates <= 0 || maxCandidates > MaximumCandidateLimit)
                throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            ActivityDefinitionId = activityDefinitionId;
            ActivationThreshold = activationThreshold;
            SatisfactionOffset = satisfactionOffset;
            MaxCandidates = maxCandidates;
        }

        public AuthoredId ActivityDefinitionId { get; }
        public long ActivationThreshold { get; }
        public long SatisfactionOffset { get; }
        public int MaxCandidates { get; }
    }
}
