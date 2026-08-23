using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Relationships
{
    public static class RelationshipChannels
    {
        public static readonly AuthoredId Affection = new AuthoredId("relationship.channel.affection");
        public static readonly AuthoredId TrustJudgment = new AuthoredId("relationship.channel.trust_judgment");
        public static readonly AuthoredId TrustMotives = new AuthoredId("relationship.channel.trust_motives");
        public static readonly AuthoredId Respect = new AuthoredId("relationship.channel.respect");
        public static readonly AuthoredId Admiration = new AuthoredId("relationship.channel.admiration");
        public static readonly AuthoredId Comfort = new AuthoredId("relationship.channel.comfort");
        public static readonly AuthoredId Resentment = new AuthoredId("relationship.channel.resentment");
        public static readonly AuthoredId Obligation = new AuthoredId("relationship.channel.obligation");
        public static readonly AuthoredId Attraction = new AuthoredId("relationship.channel.attraction");
    }

    /// <summary>A provenance-retaining salient memory owned by one direction of a relationship.</summary>
    public sealed class RelationshipMemory
    {
        public RelationshipMemory(
            AuthoredId memoryKind,
            SimTime occurredAt,
            AuthoredId explanationId,
            IReadOnlyDictionary<AuthoredId, long> channelEffects,
            HistoryEntryId sourceHistoryEntryId = default,
            CommitmentOutcomeId sourceOutcomeId = default)
        {
            if (!memoryKind.IsSet)
            {
                throw new ArgumentException("A relationship memory needs a stable kind.", nameof(memoryKind));
            }

            MemoryKind = memoryKind;
            OccurredAt = occurredAt;
            ExplanationId = explanationId;
            ChannelEffects = channelEffects ?? new SortedDictionary<AuthoredId, long>();
            SourceHistoryEntryId = sourceHistoryEntryId;
            SourceOutcomeId = sourceOutcomeId;
        }

        public AuthoredId MemoryKind { get; }
        public SimTime OccurredAt { get; }
        public AuthoredId ExplanationId { get; }
        public IReadOnlyDictionary<AuthoredId, long> ChannelEffects { get; }
        public HistoryEntryId SourceHistoryEntryId { get; }
        public CommitmentOutcomeId SourceOutcomeId { get; }
    }

    /// <summary>
    /// Directional durable state for Observer → Target. Contradictory channels remain independent;
    /// familiarity/exposure records social embeddedness rather than liking or belief certainty.
    /// </summary>
    public sealed class DirectionalRelationshipState
    {
        private readonly SortedDictionary<AuthoredId, AnalyticalProgression> _channels =
            new SortedDictionary<AuthoredId, AnalyticalProgression>();
        private readonly List<RelationshipMemory> _memories = new List<RelationshipMemory>();

        public DirectionalRelationshipState(CharacterId observerId, CharacterId targetId, SimTime establishedAt)
        {
            if (!observerId.IsSet || !targetId.IsSet || observerId == targetId)
            {
                throw new ArgumentException("A directional relationship needs two distinct characters.");
            }

            ObserverId = observerId;
            TargetId = targetId;
            EstablishedAt = establishedAt;
            FamiliarityProgression = AnalyticalProgression.Constant(0, establishedAt, 0, 10000);
        }

        public CharacterId ObserverId { get; }
        public CharacterId TargetId { get; }
        public SimTime EstablishedAt { get; }
        public AnalyticalProgression FamiliarityProgression { get; private set; }
        public long ExposureMinutes { get; private set; }
        public SimTime? LastInteractionAt { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyDictionary<AuthoredId, AnalyticalProgression> Channels => _channels;
        public IReadOnlyList<RelationshipMemory> Memories => _memories;

        public long ChannelAt(AuthoredId channelId, SimTime at) =>
            _channels.TryGetValue(channelId, out AnalyticalProgression value) ? value.ValueAt(at) : 0;

        public long FamiliarityAt(SimTime at) => FamiliarityProgression.ValueAt(at);

        public void SetChannel(AuthoredId channelId, AnalyticalProgression value)
        {
            if (!channelId.IsSet)
            {
                throw new ArgumentException("A relationship channel needs a stable authored id.", nameof(channelId));
            }

            _channels[channelId] = value;
            Revision++;
        }

        public void ApplyChannelDelta(AuthoredId channelId, SimTime at, long delta)
        {
            AnalyticalProgression current = _channels.TryGetValue(channelId, out AnalyticalProgression existing)
                ? existing
                : AnalyticalProgression.Constant(0, at);
            long bounded = IntegerMath.Clamp(current.ValueAt(at) + delta, -10000, 10000);
            _channels[channelId] = AnalyticalProgression.Constant(bounded, at);
            Revision++;
        }

        public void SetChannelDrift(AuthoredId channelId, SimTime at, long numerator, long denominator = 1)
        {
            AnalyticalProgression current = _channels.TryGetValue(channelId, out AnalyticalProgression existing)
                ? existing
                : AnalyticalProgression.Constant(0, at);
            _channels[channelId] = current.WithRate(at, numerator, denominator);
            Revision++;
        }

        public void RecordExposure(SimTime at, long exposureMinutes, int familiarityDelta)
        {
            ExposureMinutes = Math.Max(0, checked(ExposureMinutes + exposureMinutes));
            FamiliarityProgression = FamiliarityProgression.WithOffset(at, familiarityDelta);
            LastInteractionAt = at;
            Revision++;
        }

        public void SetFamiliarityDrift(SimTime at, long numerator, long denominator = 1)
        {
            FamiliarityProgression = FamiliarityProgression.WithRate(at, numerator, denominator);
            Revision++;
        }

        public void AddMemory(RelationshipMemory memory)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            _memories.Add(memory);
            _memories.Sort((a, b) =>
            {
                int at = a.OccurredAt.CompareTo(b.OccurredAt);
                return at != 0 ? at : a.MemoryKind.CompareTo(b.MemoryKind);
            });
            Revision++;
        }

        public void RestoreState(
            AnalyticalProgression familiarity,
            long exposureMinutes,
            SimTime? lastInteractionAt,
            int revision)
        {
            FamiliarityProgression = familiarity.WithBounds(familiarity.AnchoredAt, 0, 10000);
            ExposureMinutes = Math.Max(0, exposureMinutes);
            LastInteractionAt = lastInteractionAt;
            Revision = revision;
        }
    }
}
