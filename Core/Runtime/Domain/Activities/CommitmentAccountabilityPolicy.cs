using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.History;

namespace Vivarium.Domain.Activities
{
    public enum StakeholderRole
    {
        Counterparty = 0,
        Beneficiary = 1,
        Authority = 2,
        Participant = 3,
    }

    public readonly struct StakeholderRef : IEquatable<StakeholderRef>, IComparable<StakeholderRef>
    {
        public StakeholderRef(EntityRef entity, StakeholderRole role)
        {
            Entity = entity;
            Role = role;
        }
        public EntityRef Entity { get; }
        public StakeholderRole Role { get; }
        public bool Equals(StakeholderRef other) => Entity == other.Entity && Role == other.Role;
        public override bool Equals(object obj) => obj is StakeholderRef other && Equals(other);
        public override int GetHashCode() => (Entity.GetHashCode() * 397) ^ (int)Role;
        public int CompareTo(StakeholderRef other)
        {
            int entity = Entity.CompareTo(other.Entity);
            return entity != 0 ? entity : ((int)Role).CompareTo((int)other.Role);
        }
    }

    public enum PerceivedCommitmentCause
    {
        Unknown = 0,
        RelinquishedByActor = 1,
        NotAttributedToActor = 2,
    }

    public sealed class KnownCommitmentAttribution
    {
        public KnownCommitmentAttribution(
            CommitmentOutcomeKind observedOutcome,
            PerceivedCommitmentCause perceivedCause,
            Domain.Time.SimTime observedAt,
            CommitmentOutcomeId sourceOutcomeId,
            bool actorAccountable)
        {
            ObservedOutcome = observedOutcome;
            PerceivedCause = perceivedCause;
            ObservedAt = observedAt;
            SourceOutcomeId = sourceOutcomeId;
            ActorAccountable = actorAccountable;
        }
        public CommitmentOutcomeKind ObservedOutcome { get; }
        public PerceivedCommitmentCause PerceivedCause { get; }
        public Domain.Time.SimTime ObservedAt { get; }
        public CommitmentOutcomeId SourceOutcomeId { get; }
        public bool ActorAccountable { get; }
    }

    public sealed class CommitmentMemoryConsequence
    {
        public CommitmentMemoryConsequence(
            AuthoredId memoryKind,
            AuthoredId explanationId,
            RetentionTier retentionTier = RetentionTier.Significant)
        {
            MemoryKind = memoryKind;
            ExplanationId = explanationId;
            RetentionTier = retentionTier;
        }
        public AuthoredId MemoryKind { get; }
        public AuthoredId ExplanationId { get; }
        public RetentionTier RetentionTier { get; }
    }

    public sealed class CommitmentConsequenceSet
    {
        private readonly SortedDictionary<AuthoredId, long> _channelDeltas;
        public static readonly CommitmentConsequenceSet None = new CommitmentConsequenceSet();

        public CommitmentConsequenceSet(
            CommitmentMemoryConsequence memory = null,
            AuthoredId evidenceActionId = default,
            IReadOnlyDictionary<AuthoredId, long> channelDeltas = null)
        {
            Memory = memory;
            EvidenceActionId = evidenceActionId;
            _channelDeltas = new SortedDictionary<AuthoredId, long>();
            if (channelDeltas != null)
                foreach (KeyValuePair<AuthoredId, long> pair in channelDeltas)
                    _channelDeltas[pair.Key] = pair.Value;
        }
        public CommitmentMemoryConsequence Memory { get; }
        public AuthoredId EvidenceActionId { get; }
        public IReadOnlyDictionary<AuthoredId, long> ChannelDeltas => _channelDeltas;
        public bool HasAny => Memory != null || EvidenceActionId.IsSet || _channelDeltas.Count > 0;
    }

    public sealed class CommitmentAccountabilityOverride
    {
        public CommitmentAccountabilityOverride(
            CommitmentOutcomeKind outcome,
            StakeholderRole role,
            CommitmentConsequenceSet consequences,
            PerceivedCommitmentCause? perceivedCause = null)
        {
            Outcome = outcome;
            Role = role;
            PerceivedCause = perceivedCause;
            Consequences = consequences ?? CommitmentConsequenceSet.None;
        }
        public CommitmentOutcomeKind Outcome { get; }
        public StakeholderRole Role { get; }
        public PerceivedCommitmentCause? PerceivedCause { get; }
        public CommitmentConsequenceSet Consequences { get; }
    }

    /// <summary>Immutable definition-derived policy snapshotted onto a materialized Commitment.</summary>
    public sealed class CommitmentAccountabilityPolicy
    {
        public static readonly CommitmentAccountabilityPolicy None = new CommitmentAccountabilityPolicy();
        private readonly SortedDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet> _byOutcome;
        private readonly SortedDictionary<StakeholderRole, CommitmentConsequenceSet> _byRole;
        private readonly List<CommitmentAccountabilityOverride> _overrides;

        public CommitmentAccountabilityPolicy(
            CommitmentConsequenceSet defaultConsequences = null,
            IReadOnlyDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet> byOutcome = null,
            IReadOnlyDictionary<StakeholderRole, CommitmentConsequenceSet> byRole = null,
            IReadOnlyList<CommitmentAccountabilityOverride> specificOverrides = null,
            AuthoredId id = default)
        {
            Id = id;
            Default = defaultConsequences ?? CommitmentConsequenceSet.None;
            _byOutcome = Copy(byOutcome);
            _byRole = Copy(byRole);
            _overrides = new List<CommitmentAccountabilityOverride>();
            if (specificOverrides != null) _overrides.AddRange(specificOverrides);
            _overrides.Sort((a, b) =>
            {
                int outcome = a.Outcome.CompareTo(b.Outcome);
                if (outcome != 0) return outcome;
                int role = a.Role.CompareTo(b.Role);
                if (role != 0) return role;
                return Nullable.Compare(a.PerceivedCause, b.PerceivedCause);
            });
        }

        public AuthoredId Id { get; }
        public CommitmentConsequenceSet Default { get; }
        public IReadOnlyDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet> ByOutcome => _byOutcome;
        public IReadOnlyDictionary<StakeholderRole, CommitmentConsequenceSet> ByRole => _byRole;
        public IReadOnlyList<CommitmentAccountabilityOverride> SpecificOverrides => _overrides;

        public CommitmentConsequenceSet Resolve(KnownCommitmentAttribution attribution, StakeholderRole role)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < _overrides.Count; i++)
                {
                    CommitmentAccountabilityOverride item = _overrides[i];
                    if (item.Outcome != attribution.ObservedOutcome || item.Role != role) continue;
                    if (pass == 0 && item.PerceivedCause == attribution.PerceivedCause) return item.Consequences;
                    if (pass == 1 && !item.PerceivedCause.HasValue) return item.Consequences;
                }
            if (_byOutcome.TryGetValue(attribution.ObservedOutcome, out CommitmentConsequenceSet outcome)) return outcome;
            if (_byRole.TryGetValue(role, out CommitmentConsequenceSet roleSet)) return roleSet;
            return Default;
        }

        private static SortedDictionary<TKey, CommitmentConsequenceSet> Copy<TKey>(
            IReadOnlyDictionary<TKey, CommitmentConsequenceSet> source)
        {
            var copy = new SortedDictionary<TKey, CommitmentConsequenceSet>();
            if (source != null)
                foreach (KeyValuePair<TKey, CommitmentConsequenceSet> pair in source)
                    copy[pair.Key] = pair.Value ?? CommitmentConsequenceSet.None;
            return copy;
        }
    }
}
