using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    /// <summary>
    /// Content-backed ordinary routine for an increasing pressure Need such as Hunger. The configured
    /// Activity begins only at a location that explicitly affords it and applies its offset on completion.
    /// </summary>
    public sealed class NeedSatisfactionRoutineDefinition
    {
        public NeedSatisfactionRoutineDefinition(
            AuthoredId activityDefinitionId,
            long activationThreshold,
            long satisfactionOffset)
        {
            if (!activityDefinitionId.IsSet)
                throw new ArgumentException("A satisfaction routine needs an Activity definition.", nameof(activityDefinitionId));
            if (satisfactionOffset >= 0)
                throw new ArgumentOutOfRangeException(nameof(satisfactionOffset), "An increasing pressure Need needs a negative satisfaction offset.");

            ActivityDefinitionId = activityDefinitionId;
            ActivationThreshold = activationThreshold;
            SatisfactionOffset = satisfactionOffset;
        }

        public AuthoredId ActivityDefinitionId { get; }

        public long ActivationThreshold { get; }

        public long SatisfactionOffset { get; }
    }
}
