using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Employment definition.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Employment Definition", fileName = "employment_")]
    public sealed class EmploymentDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "employment.";
        [SerializeField] private string roleId = "employment_role.";
        [SerializeField] private EmploymentObligationPatternEntry[] obligationPatterns =
            new EmploymentObligationPatternEntry[0];

        public string AuthoredId => authoredId;

        public EmploymentDefinition ToDefinition(
            IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> availablePolicies = null)
        {
            var patterns = new EmploymentObligationPattern[obligationPatterns?.Length ?? 0];
            for (int i = 0; i < patterns.Length; i++)
                patterns[i] = obligationPatterns[i].ToDefinition(availablePolicies);
            return new EmploymentDefinition(new AuthoredId(authoredId), new AuthoredId(roleId), patterns);
        }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("employment."))
                yield return $"{name}: Employment ids should be namespaced 'employment.<something>'.";
            if (string.IsNullOrEmpty(roleId) || roleId.EndsWith("."))
                yield return $"{name}: role id '{roleId}' is incomplete.";

            var patternIds = new HashSet<string>();
            for (int i = 0; i < (obligationPatterns?.Length ?? 0); i++)
            {
                EmploymentObligationPatternEntry pattern = obligationPatterns[i];
                if (string.IsNullOrWhiteSpace(pattern.authoredId))
                    yield return $"{name}: obligation pattern {i} needs an authored id.";
                else if (!patternIds.Add(pattern.authoredId))
                    yield return $"{name}: obligation pattern '{pattern.authoredId}' is duplicated.";
                if (pattern.cycleLengthDays < 1 || pattern.cycleLengthDays > 31)
                    yield return $"{name}: obligation '{pattern.authoredId}' cycle length must be 1..31 days.";
                if (pattern.durationMinutes <= 0)
                    yield return $"{name}: obligation '{pattern.authoredId}' duration must be positive.";
            }
        }
    }

    [System.Serializable]
    public struct EmploymentObligationPatternEntry
    {
        public string authoredId;
        public string commitmentKindId;
        public int cycleLengthDays;
        public int activeDaysMask;
        public int startMinuteOfDay;
        public int durationMinutes;
        public int priority;
        public string activityDefinitionId;
        public int startWindowMinutes;
        public string accountabilityPolicyId;

        public EmploymentObligationPattern ToDefinition(
            IReadOnlyDictionary<AuthoredId, CommitmentAccountabilityPolicy> availablePolicies = null)
        {
            CommitmentAccountabilityPolicy policy = CommitmentAccountabilityPolicy.None;
            if (!string.IsNullOrWhiteSpace(accountabilityPolicyId))
            {
                var policyId = new AuthoredId(accountabilityPolicyId);
                if (availablePolicies == null || !availablePolicies.TryGetValue(policyId, out policy))
                    policy = new CommitmentAccountabilityPolicy(id: policyId);
            }

            return new EmploymentObligationPattern(
                new AuthoredId(authoredId),
                new AuthoredId(commitmentKindId),
                cycleLengthDays,
                activeDaysMask,
                startMinuteOfDay,
                SimDuration.FromMinutes(durationMinutes),
                priority,
                new AuthoredId(activityDefinitionId),
                SimDuration.FromMinutes(startWindowMinutes),
                policy);
        }
    }
}
