using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    /// <summary>
    /// Content-backed routine reaction for a reserve-style Need such as Energy.
    /// The Need falls under its ordinary rate, starts the configured recovery Activity at a group's
    /// primary location, then rises under the recovery rate until the recovered threshold is reached.
    /// </summary>
    public sealed class NeedRestRoutineDefinition
    {
        public NeedRestRoutineDefinition(
            AuthoredId activityDefinitionId,
            AuthoredId locationGroupKindId,
            long activationThreshold,
            long recoveredThreshold,
            long recoveryRateNumerator,
            long recoveryRateDenominator = 1)
        {
            if (!activityDefinitionId.IsSet)
                throw new ArgumentException("A rest routine needs an Activity definition.", nameof(activityDefinitionId));
            if (!locationGroupKindId.IsSet)
                throw new ArgumentException("A rest routine needs a location-bearing group kind.", nameof(locationGroupKindId));
            if (recoveryRateDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryRateDenominator));

            ActivityDefinitionId = activityDefinitionId;
            LocationGroupKindId = locationGroupKindId;
            ActivationThreshold = activationThreshold;
            RecoveredThreshold = recoveredThreshold;
            RecoveryRateNumerator = recoveryRateNumerator;
            RecoveryRateDenominator = recoveryRateDenominator;
        }

        public AuthoredId ActivityDefinitionId { get; }

        public AuthoredId LocationGroupKindId { get; }

        public long ActivationThreshold { get; }

        public long RecoveredThreshold { get; }

        public long RecoveryRateNumerator { get; }

        public long RecoveryRateDenominator { get; }
    }
}
