using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Commitment accountability policy.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Commitment Accountability Policy", fileName = "accountability_")]
    public sealed class CommitmentAccountabilityPolicyAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "accountability.";
        [SerializeField] private CommitmentConsequenceSetEntry defaultConsequences;
        [SerializeField] private CommitmentOutcomeConsequenceEntry[] byOutcome =
            new CommitmentOutcomeConsequenceEntry[0];
        [SerializeField] private CommitmentRoleConsequenceEntry[] byRole =
            new CommitmentRoleConsequenceEntry[0];
        [SerializeField] private CommitmentAccountabilityOverrideEntry[] specificOverrides =
            new CommitmentAccountabilityOverrideEntry[0];

        public string AuthoredId => authoredId;

        public CommitmentAccountabilityPolicy ToDefinition()
        {
            var outcomes = new SortedDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>();
            for (int i = 0; i < (byOutcome?.Length ?? 0); i++)
            {
                if (outcomes.ContainsKey(byOutcome[i].outcome))
                    throw new System.InvalidOperationException(
                        $"Accountability policy '{authoredId}' declares outcome '{byOutcome[i].outcome}' twice.");
                outcomes.Add(byOutcome[i].outcome, byOutcome[i].consequences.ToDefinition());
            }

            var roles = new SortedDictionary<StakeholderRole, CommitmentConsequenceSet>();
            for (int i = 0; i < (byRole?.Length ?? 0); i++)
            {
                if (roles.ContainsKey(byRole[i].role))
                    throw new System.InvalidOperationException(
                        $"Accountability policy '{authoredId}' declares role '{byRole[i].role}' twice.");
                roles.Add(byRole[i].role, byRole[i].consequences.ToDefinition());
            }

            var overrides = new CommitmentAccountabilityOverride[specificOverrides?.Length ?? 0];
            for (int i = 0; i < overrides.Length; i++) overrides[i] = specificOverrides[i].ToDefinition();
            return new CommitmentAccountabilityPolicy(
                defaultConsequences.ToDefinition(), outcomes, roles, overrides, new AuthoredId(authoredId));
        }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("accountability."))
                yield return $"{name}: policy ids should be namespaced 'accountability.<something>'.";

            var outcomes = new HashSet<CommitmentOutcomeKind>();
            for (int i = 0; i < (byOutcome?.Length ?? 0); i++)
                if (!outcomes.Add(byOutcome[i].outcome))
                    yield return $"{name}: outcome '{byOutcome[i].outcome}' is declared twice.";

            var roles = new HashSet<StakeholderRole>();
            for (int i = 0; i < (byRole?.Length ?? 0); i++)
                if (!roles.Add(byRole[i].role))
                    yield return $"{name}: role '{byRole[i].role}' is declared twice.";
        }
    }

    [System.Serializable]
    public struct CommitmentOutcomeConsequenceEntry
    {
        public CommitmentOutcomeKind outcome;
        public CommitmentConsequenceSetEntry consequences;
    }

    [System.Serializable]
    public struct CommitmentRoleConsequenceEntry
    {
        public StakeholderRole role;
        public CommitmentConsequenceSetEntry consequences;
    }

    [System.Serializable]
    public struct CommitmentAccountabilityOverrideEntry
    {
        public CommitmentOutcomeKind outcome;
        public StakeholderRole role;
        public bool matchPerceivedCause;
        public PerceivedCommitmentCause perceivedCause;
        public CommitmentConsequenceSetEntry consequences;

        public CommitmentAccountabilityOverride ToDefinition() => new CommitmentAccountabilityOverride(
            outcome,
            role,
            consequences.ToDefinition(),
            matchPerceivedCause ? (PerceivedCommitmentCause?)perceivedCause : null);
    }

    [System.Serializable]
    public struct CommitmentConsequenceSetEntry
    {
        public bool hasMemory;
        public string memoryKind;
        public string memoryExplanationId;
        public RetentionTier memoryRetentionTier;
        public string evidenceActionId;
        public AuthoredLongEntry[] channelDeltas;

        public CommitmentConsequenceSet ToDefinition()
        {
            var deltas = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < (channelDeltas?.Length ?? 0); i++)
            {
                var id = new AuthoredId(channelDeltas[i].authoredId);
                if (deltas.ContainsKey(id))
                    throw new System.InvalidOperationException($"Consequence channel '{id}' is declared twice.");
                deltas.Add(id, channelDeltas[i].value);
            }
            return new CommitmentConsequenceSet(
                hasMemory
                    ? new CommitmentMemoryConsequence(
                        new AuthoredId(memoryKind),
                        new AuthoredId(memoryExplanationId),
                        memoryRetentionTier)
                    : null,
                new AuthoredId(evidenceActionId),
                deltas);
        }
    }

    [System.Serializable]
    public struct AuthoredLongEntry
    {
        public string authoredId;
        public long value;
    }
}
