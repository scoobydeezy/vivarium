using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;

namespace Vivarium.Application.Persistence
{
    /// <summary>Explicit save mapping for definition-derived accountability snapshots.</summary>
    internal static class CommitmentAccountabilityDataMapper
    {
        internal static List<CommitmentStakeholderData> WriteStakeholders(IReadOnlyList<StakeholderRef> stakeholders)
        {
            var result = new List<CommitmentStakeholderData>();
            if (stakeholders == null) return result;
            for (int i = 0; i < stakeholders.Count; i++)
            {
                StakeholderRef stakeholder = stakeholders[i];
                result.Add(new CommitmentStakeholderData
                {
                    EntityKind = (int)stakeholder.Entity.Kind,
                    RuntimeId = stakeholder.Entity.RuntimeId,
                    Role = (int)stakeholder.Role,
                });
            }
            return result;
        }

        internal static IReadOnlyList<StakeholderRef> ReadStakeholders(
            List<CommitmentStakeholderData> data,
            bool hasSnapshot)
        {
            if (!hasSnapshot) return null;
            if (data == null || data.Count == 0) return new StakeholderRef[0];
            var result = new StakeholderRef[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                CommitmentStakeholderData item = data[i];
                result[i] = new StakeholderRef(
                    new EntityRef((EntityKind)item.EntityKind, item.RuntimeId),
                    (StakeholderRole)item.Role);
            }
            return result;
        }

        internal static CommitmentAccountabilityPolicyData WritePolicy(CommitmentAccountabilityPolicy policy)
        {
            policy = policy ?? CommitmentAccountabilityPolicy.None;
            var result = new CommitmentAccountabilityPolicyData
            {
                Id = policy.Id.Value,
                Default = WriteConsequences(policy.Default),
            };
            foreach (KeyValuePair<CommitmentOutcomeKind, CommitmentConsequenceSet> pair in policy.ByOutcome)
                result.ByOutcome.Add(new CommitmentOutcomeConsequenceData
                {
                    Outcome = (int)pair.Key,
                    Consequences = WriteConsequences(pair.Value),
                });
            foreach (KeyValuePair<StakeholderRole, CommitmentConsequenceSet> pair in policy.ByRole)
                result.ByRole.Add(new CommitmentRoleConsequenceData
                {
                    Role = (int)pair.Key,
                    Consequences = WriteConsequences(pair.Value),
                });
            for (int i = 0; i < policy.SpecificOverrides.Count; i++)
            {
                CommitmentAccountabilityOverride item = policy.SpecificOverrides[i];
                result.SpecificOverrides.Add(new CommitmentAccountabilityOverrideData
                {
                    Outcome = (int)item.Outcome,
                    Role = (int)item.Role,
                    HasPerceivedCause = item.PerceivedCause.HasValue,
                    PerceivedCause = item.PerceivedCause.HasValue ? (int)item.PerceivedCause.Value : 0,
                    Consequences = WriteConsequences(item.Consequences),
                });
            }
            return result;
        }

        internal static CommitmentAccountabilityPolicy ReadPolicy(CommitmentAccountabilityPolicyData data)
        {
            if (data == null) return CommitmentAccountabilityPolicy.None;
            var byOutcome = new SortedDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>();
            for (int i = 0; i < data.ByOutcome.Count; i++)
            {
                CommitmentOutcomeConsequenceData item = data.ByOutcome[i];
                byOutcome[(CommitmentOutcomeKind)item.Outcome] = ReadConsequences(item.Consequences);
            }
            var byRole = new SortedDictionary<StakeholderRole, CommitmentConsequenceSet>();
            for (int i = 0; i < data.ByRole.Count; i++)
            {
                CommitmentRoleConsequenceData item = data.ByRole[i];
                byRole[(StakeholderRole)item.Role] = ReadConsequences(item.Consequences);
            }
            var overrides = new List<CommitmentAccountabilityOverride>();
            for (int i = 0; i < data.SpecificOverrides.Count; i++)
            {
                CommitmentAccountabilityOverrideData item = data.SpecificOverrides[i];
                overrides.Add(new CommitmentAccountabilityOverride(
                    (CommitmentOutcomeKind)item.Outcome,
                    (StakeholderRole)item.Role,
                    ReadConsequences(item.Consequences),
                    item.HasPerceivedCause ? (PerceivedCommitmentCause?)item.PerceivedCause : null));
            }
            return new CommitmentAccountabilityPolicy(
                ReadConsequences(data.Default), byOutcome, byRole, overrides, new AuthoredId(data.Id));
        }

        private static CommitmentConsequenceSetData WriteConsequences(CommitmentConsequenceSet consequences)
        {
            consequences = consequences ?? CommitmentConsequenceSet.None;
            var result = new CommitmentConsequenceSetData
            {
                EvidenceActionId = consequences.EvidenceActionId.Value,
            };
            if (consequences.Memory != null)
            {
                result.MemoryKind = consequences.Memory.MemoryKind.Value;
                result.MemoryExplanationId = consequences.Memory.ExplanationId.Value;
                result.MemoryRetentionTier = (int)consequences.Memory.RetentionTier;
            }
            foreach (KeyValuePair<AuthoredId, long> pair in consequences.ChannelDeltas)
                result.ChannelDeltas.Add(new AuthoredLongData { Key = pair.Key.Value, Value = pair.Value });
            return result;
        }

        private static CommitmentConsequenceSet ReadConsequences(CommitmentConsequenceSetData data)
        {
            if (data == null) return CommitmentConsequenceSet.None;
            CommitmentMemoryConsequence memory = string.IsNullOrEmpty(data.MemoryKind)
                ? null
                : new CommitmentMemoryConsequence(
                    new AuthoredId(data.MemoryKind),
                    new AuthoredId(data.MemoryExplanationId),
                    (RetentionTier)data.MemoryRetentionTier);
            var deltas = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < data.ChannelDeltas.Count; i++)
                deltas[new AuthoredId(data.ChannelDeltas[i].Key)] = data.ChannelDeltas[i].Value;
            return new CommitmentConsequenceSet(memory, new AuthoredId(data.EvidenceActionId), deltas);
        }
    }
}
