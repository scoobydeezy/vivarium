using Vivarium.Domain.Common;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// A way something can become known: conversation, repeated observation, inspection (§24).
    /// Definitions list the channels through which their facts are discoverable.
    /// </summary>
    public readonly struct DiscoveryChannel
    {
        public DiscoveryChannel(AuthoredId id, int difficultyBasisPoints = 0)
        {
            Id = id;
            DifficultyBasisPoints = difficultyBasisPoints;
        }

        /// <summary>Authored channel id, e.g. <c>discovery.conversation</c>.</summary>
        public AuthoredId Id { get; }

        /// <summary>How hard this channel is, in basis points (§16). 0 means "always yields".</summary>
        public int DifficultyBasisPoints { get; }

        public override string ToString() => Id.ToString();
    }

    /// <summary>
    /// Provenance of a knowledge entry (§23.1).
    /// <para>
    /// Primarily durable descriptive data — "observed during conversation", "learned from Mina". The
    /// optional <see cref="SourceHistoryEntryId"/> is a <b>weak</b> reference for recent-history
    /// navigation: if that history entry is later pruned or compacted (§37), the knowledge entry stays
    /// completely valid.
    /// </para>
    /// </summary>
    public readonly struct DiscoverySource
    {
        public DiscoverySource(AuthoredId channelId, EntityRef informant = default, HistoryEntryId sourceHistoryEntryId = default)
        {
            ChannelId = channelId;
            Informant = informant;
            SourceHistoryEntryId = sourceHistoryEntryId;
        }

        public AuthoredId ChannelId { get; }

        /// <summary>Who it came from, when that matters ("learned from Mina"). Weak reference (§7.1).</summary>
        public EntityRef Informant { get; }

        /// <summary>Optional weak pointer into history. Never required for the entry to remain valid.</summary>
        public HistoryEntryId SourceHistoryEntryId { get; }

        public static DiscoverySource Channel(AuthoredId channelId) => new DiscoverySource(channelId);

        public override string ToString() =>
            Informant.IsSet ? $"{ChannelId} via {Informant}" : ChannelId.ToString();
    }

    /// <summary>Authored discovery channel ids.</summary>
    public static class DiscoveryChannels
    {
        public static readonly AuthoredId Conversation = new AuthoredId("discovery.conversation");
        public static readonly AuthoredId DirectObservation = new AuthoredId("discovery.direct_observation");
        public static readonly AuthoredId RepeatedObservation = new AuthoredId("discovery.repeated_observation");
        public static readonly AuthoredId Inspection = new AuthoredId("discovery.inspection");
        public static readonly AuthoredId DecisionOutcome = new AuthoredId("discovery.decision_outcome");
        public static readonly AuthoredId Hearsay = new AuthoredId("discovery.hearsay");
    }
}
