using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Knowledge;

namespace Vivarium.Domain.Social
{
    public sealed class SocialBeliefChangedEvent : IDomainEvent
    {
        public static readonly AuthoredId Type = new AuthoredId("domain.social.belief_changed");

        public SocialBeliefChangedEvent(ObserverRef observer, CharacterId targetId, int evidenceRevision)
        {
            Observer = observer;
            TargetId = targetId;
            EvidenceRevision = evidenceRevision;
        }

        public AuthoredId EventType => Type;
        public ObserverRef Observer { get; }
        public CharacterId TargetId { get; }
        public int EvidenceRevision { get; }
    }
}
