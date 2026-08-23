using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// Records bounded reported belief/group norm facts. It never creates recursively nested
    /// "A believes B believes C" structures and never promotes reputation to omniscient truth.
    /// </summary>
    public sealed class SocialReputationService
    {
        public void RecordReport(
            WorldState world,
            ObserverRef recipient,
            CharacterId target,
            CharacterId informant,
            AuthoredId lensOrChannelId,
            long reportedValue,
            bool groupNorm = false)
        {
            if (recipient.IsCharacter && recipient.CharacterId == informant)
            {
                throw new ArgumentException("A report needs an informant distinct from its recipient.", nameof(informant));
            }

            var key = new FactKey(
                groupNorm ? FactKinds.PerceivedGroupOpinion : FactKinds.ReportedSocialBelief,
                target.ToRef(),
                lensOrChannelId);
            world.Knowledge.Record(new KnowledgeEntry(
                key,
                ObservedValue.Of(IntegerMath.Clamp(reportedValue, -10000, 10000)),
                world.Clock.Now,
                KnowledgeConfidence.Suspected,
                new DiscoverySource(new AuthoredId("discovery.social.report"), informant.ToRef()),
                recipient));
        }
    }
}
