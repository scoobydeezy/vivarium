using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Observation
{
    /// <summary>The semantic observation events presentation may report (§25).</summary>
    public enum ObservationKind
    {
        /// <summary>The player began meaningfully observing a character.</summary>
        BeginObserving = 0,

        EndObserving = 1,

        /// <summary>The player inspected a character explicitly.</summary>
        InspectCharacter = 2,

        InspectRelationship = 3,

        /// <summary>The player watched a decision resolve.</summary>
        WitnessDecision = 4,

        /// <summary>The player watched two characters interact in a shared context.</summary>
        WitnessInteraction = 5,
    }

    /// <summary>
    /// A semantic observation input (§25).
    /// <para>
    /// Observation is a first-class gameplay input, not a rendering side effect. Presentation
    /// aggregates meaningful transitions into these — <b>never one per rendered frame</b> — and the
    /// Domain decides what they can teach (invariant 7).
    /// </para>
    /// <para>
    /// Knowing that Mina is visible does not itself reveal anything. It creates the opportunity that
    /// <see cref="Vivarium.Domain.Knowledge.KnowledgeDiscoveryService"/> evaluates.
    /// </para>
    /// </summary>
    public readonly struct Observation
    {
        public Observation(ObservationKind kind, EntityRef subject, SimTime at, AuthoredId channelId)
        {
            Kind = kind;
            Subject = subject;
            At = at;
            ChannelId = channelId;
        }

        public ObservationKind Kind { get; }

        public EntityRef Subject { get; }

        public SimTime At { get; }

        /// <summary>The discovery channel this observation acts through (§24).</summary>
        public AuthoredId ChannelId { get; }

        public override string ToString() => $"{Kind} {Subject} at {At} via {ChannelId}";
    }
}
