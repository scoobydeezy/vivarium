using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>How sure the player is about an observation (§23). Deliberately open to extension.</summary>
    public enum KnowledgeConfidence
    {
        /// <summary>Hinted at but not established.</summary>
        Suspected = 0,

        /// <summary>Observed directly or told plainly.</summary>
        Known = 1,

        /// <summary>Corroborated from more than one source.</summary>
        Confirmed = 2,

        /// <summary>Later evidence disagrees with what was recorded.</summary>
        Contradicted = 3,
    }

    /// <summary>
    /// An observed value. Carries an authored band (<c>value.moderate</c>) and/or a magnitude, so
    /// presentation can show either a qualitative label or a number without the Domain caring which.
    /// </summary>
    public readonly struct ObservedValue
    {
        public ObservedValue(AuthoredId band, long? magnitude = null)
        {
            Band = band;
            Magnitude = magnitude;
        }

        public AuthoredId Band { get; }

        public long? Magnitude { get; }

        public static ObservedValue Of(AuthoredId band) => new ObservedValue(band);

        public static ObservedValue Of(long magnitude) => new ObservedValue(AuthoredId.None, magnitude);

        public override string ToString() =>
            Band.IsSet ? (Magnitude.HasValue ? $"{Band}({Magnitude})" : Band.ToString()) : Magnitude?.ToString() ?? "<unknown>";
    }

    /// <summary>
    /// What the player observed, when, and how sure they are (§22, §23).
    /// <para>
    /// <b>Current truth ≠ player knowledge.</b> This record is a snapshot of an observation, not a live
    /// view of the world. The relationship may have changed since; knowledge going stale is a feature.
    /// </para>
    /// </summary>
    public sealed class KnowledgeEntry
    {
        public KnowledgeEntry(
            FactKey key,
            ObservedValue observedValue,
            SimTime observedAt,
            KnowledgeConfidence confidence,
            DiscoverySource source)
        {
            Key = key;
            ObservedValue = observedValue;
            ObservedAt = observedAt;
            Confidence = confidence;
            Source = source;
        }

        public FactKey Key { get; }

        public ObservedValue ObservedValue { get; }

        public SimTime ObservedAt { get; }

        public KnowledgeConfidence Confidence { get; }

        /// <summary>Where this came from. Durable descriptive data, not a live foreign key (§23.1).</summary>
        public DiscoverySource Source { get; }

        public override string ToString() => $"{Key} = {ObservedValue} ({Confidence}, {ObservedAt})";
    }
}
