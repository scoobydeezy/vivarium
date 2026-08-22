using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Turns observations into knowledge (§24).
    /// <para>
    /// The pipeline is: truth → fact providers → potential discoverable claims → discovery rules →
    /// knowledge entries. Observation supplies the <i>opportunity</i>; this service decides what that
    /// opportunity actually teaches.
    /// </para>
    /// <para>
    /// Any chance involved goes through the deterministic oracle with a stable authored purpose (§14),
    /// so a replayed session discovers exactly the same facts.
    /// </para>
    /// </summary>
    public sealed class KnowledgeDiscoveryService
    {
        private readonly List<IFactProvider> _providers = new List<IFactProvider>();

        /// <summary>
        /// Registers a provider. Order is registration order and providers are consulted in it, so
        /// nothing depends on reflection or load order (§15).
        /// </summary>
        public void RegisterProvider(IFactProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _providers.Add(provider);
        }

        /// <summary>
        /// Offers every claim about <paramref name="subject"/> available through
        /// <paramref name="channel"/> to the player's ledger, recording those that pass the channel's
        /// difficulty.
        /// </summary>
        /// <param name="observationOrdinal">
        /// Monotonic per-observation counter supplying the oracle's roll index. Persisted alongside
        /// observation state so a reload rolls identically rather than re-rolling from zero.
        /// </param>
        /// <returns>The entries newly recorded, in deterministic order.</returns>
        public IReadOnlyList<KnowledgeEntry> Discover(
            WorldState world,
            EntityRef subject,
            DiscoveryChannel channel,
            SimulationContext context,
            int observationOrdinal)
        {
            var recorded = new List<KnowledgeEntry>();
            var claims = new List<DiscoverableClaim>();

            for (int i = 0; i < _providers.Count; i++)
            {
                foreach (DiscoverableClaim claim in _providers[i].ClaimsAbout(world, subject, channel))
                {
                    claims.Add(claim);
                }
            }

            // Explicit ordering: providers may enumerate in their own order, but what the player learns
            // must not depend on that (§15).
            claims.Sort((a, b) => a.Key.CompareTo(b.Key));

            var scope = new RandomScope(RandomScopeTypes.Character, subject.RuntimeId);

            for (int i = 0; i < claims.Count; i++)
            {
                DiscoverableClaim claim = claims[i];
                bool learned = claim.Channel.DifficultyBasisPoints <= 0 || context.Random.Chance(
                    scope,
                    RandomPurposes.Qualified(RandomPurposes.KnowledgeDiscovery, claim.Key.Kind.Value),
                    observationOrdinal,
                    10000 - claim.Channel.DifficultyBasisPoints);

                if (!learned)
                {
                    continue;
                }

                var entry = new KnowledgeEntry(
                    claim.Key,
                    claim.TrueValue,
                    world.Clock.Now,
                    KnowledgeConfidence.Known,
                    DiscoverySource.Channel(channel.Id));

                world.Knowledge.Record(entry);
                recorded.Add(entry);
            }

            return recorded;
        }
    }
}
