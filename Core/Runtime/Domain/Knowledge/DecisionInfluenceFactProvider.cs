using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>Exposes the explainable influences of active Decisions as discoverable facts.</summary>
    public sealed class DecisionInfluenceFactProvider : IFactProvider
    {
        private static readonly AuthoredId[] Kinds = { FactKinds.DecisionInfluence };

        public IReadOnlyList<AuthoredId> ProvidedFactKinds => Kinds;

        public IEnumerable<DiscoverableClaim> ClaimsAbout(
            WorldState world,
            EntityRef subject,
            DiscoveryChannel channel)
        {
            foreach (Decision decision in world.Decisions.All)
            {
                if (!decision.IsActive)
                {
                    continue;
                }

                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    DecisionInfluence influence = decision.Influences[i];
                    if (!influence.IsRetracted && influence.Subject == subject)
                    {
                        yield return new DiscoverableClaim(
                            new FactKey(FactKinds.DecisionInfluence, subject, influence.LabelId),
                            ObservedValue.Of(influence.LabelId),
                            channel);
                    }
                }
            }
        }
    }
}
