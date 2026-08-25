using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Characters
{
    /// <summary>An authored discretionary Activity that may be interrupted by a social invitation.</summary>
    public readonly struct SocialInvitationPlanDefinition
    {
        public SocialInvitationPlanDefinition(AuthoredId activityDefinitionId, AuthoredId interestId)
        {
            if (!activityDefinitionId.IsSet || !interestId.IsSet)
                throw new ArgumentException("Invitation plans need stable Activity and Interest ids.");
            ActivityDefinitionId = activityDefinitionId;
            InterestId = interestId;
        }

        public AuthoredId ActivityDefinitionId { get; }
        public AuthoredId InterestId { get; }
    }

    /// <summary>
    /// Optional meaningful branch for an ordinary Social routine: a co-located character already
    /// pursuing one of these plans decides whether to join the inviter or continue it.
    /// </summary>
    public sealed class SocialInvitationRoutineDefinition
    {
        private readonly SocialInvitationPlanDefinition[] _plans;

        public SocialInvitationRoutineDefinition(
            AuthoredId decisionDefinitionId,
            AuthoredId acceptOptionId,
            IReadOnlyList<SocialInvitationPlanDefinition> plans)
        {
            if (!decisionDefinitionId.IsSet || !acceptOptionId.IsSet)
                throw new ArgumentException("A Social invitation needs stable Decision and accept-Option ids.");
            if (plans == null || plans.Count == 0)
                throw new ArgumentException("A Social invitation needs at least one interruptible plan.", nameof(plans));
            DecisionDefinitionId = decisionDefinitionId;
            AcceptOptionId = acceptOptionId;
            _plans = new SocialInvitationPlanDefinition[plans.Count];
            var activities = new HashSet<AuthoredId>();
            for (int i = 0; i < plans.Count; i++)
            {
                if (!activities.Add(plans[i].ActivityDefinitionId))
                    throw new ArgumentException(
                        $"Invitation Activity '{plans[i].ActivityDefinitionId}' is duplicated.", nameof(plans));
                _plans[i] = plans[i];
            }
            Array.Sort(_plans, (a, b) => a.ActivityDefinitionId.CompareTo(b.ActivityDefinitionId));
        }

        public AuthoredId DecisionDefinitionId { get; }
        public AuthoredId AcceptOptionId { get; }
        public IReadOnlyList<SocialInvitationPlanDefinition> Plans => _plans;

        public bool TryGetPlan(AuthoredId activityDefinitionId, out SocialInvitationPlanDefinition plan)
        {
            for (int i = 0; i < _plans.Length; i++)
            {
                if (_plans[i].ActivityDefinitionId == activityDefinitionId)
                {
                    plan = _plans[i];
                    return true;
                }
            }
            plan = default;
            return false;
        }
    }
}
