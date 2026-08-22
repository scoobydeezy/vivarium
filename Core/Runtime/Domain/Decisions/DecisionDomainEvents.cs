using Vivarium.Domain.Common;
using Vivarium.Domain.Events;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Authored Domain Event types for decisions (§12.1).</summary>
    public static class DecisionDomainEventTypes
    {
        public static readonly AuthoredId DecisionCreated = new AuthoredId("domain.decision.created");
        public static readonly AuthoredId DecisionResolved = new AuthoredId("domain.decision.resolved");
        public static readonly AuthoredId DecisionInfluencesChanged = new AuthoredId("domain.decision.influences_changed");
    }

    /// <summary>A new Decision entered the world.</summary>
    public sealed class DecisionCreatedEvent : IDomainEvent
    {
        public DecisionCreatedEvent(DecisionId decisionId, CharacterId characterId, AuthoredId definitionId)
        {
            DecisionId = decisionId;
            CharacterId = characterId;
            DefinitionId = definitionId;
        }

        public AuthoredId EventType => DecisionDomainEventTypes.DecisionCreated;

        public DecisionId DecisionId { get; }

        public CharacterId CharacterId { get; }

        public AuthoredId DefinitionId { get; }
    }

    /// <summary>A Decision resolved. Consequences hang off ordered handlers for this event (§18).</summary>
    public sealed class DecisionResolvedEvent : IDomainEvent
    {
        public DecisionResolvedEvent(DecisionId decisionId, CharacterId characterId, DecisionResolution resolution)
        {
            DecisionId = decisionId;
            CharacterId = characterId;
            Resolution = resolution;
        }

        public AuthoredId EventType => DecisionDomainEventTypes.DecisionResolved;

        public DecisionId DecisionId { get; }

        public CharacterId CharacterId { get; }

        public DecisionResolution Resolution { get; }
    }

    /// <summary>
    /// An open Decision's influence set changed because the world changed (§17.2).
    /// <para>
    /// Presentation refreshes its projection at the next quiescent boundary — never mid-cascade (§13.1).
    /// </para>
    /// </summary>
    public sealed class DecisionInfluencesChangedEvent : IDomainEvent
    {
        public DecisionInfluencesChangedEvent(DecisionId decisionId, int influenceRevision)
        {
            DecisionId = decisionId;
            InfluenceRevision = influenceRevision;
        }

        public AuthoredId EventType => DecisionDomainEventTypes.DecisionInfluencesChanged;

        public DecisionId DecisionId { get; }

        public int InfluenceRevision { get; }
    }
}
