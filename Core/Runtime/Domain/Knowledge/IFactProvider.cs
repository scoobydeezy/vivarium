using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// A truth that <i>could</i> be discovered, produced by a fact provider from live world state (§24).
    /// </summary>
    public readonly struct DiscoverableClaim
    {
        public DiscoverableClaim(FactKey key, ObservedValue trueValue, DiscoveryChannel channel)
        {
            Key = key;
            TrueValue = trueValue;
            Channel = channel;
        }

        public FactKey Key { get; }

        /// <summary>Current truth. What lands in the ledger may be coarser, or may not land at all.</summary>
        public ObservedValue TrueValue { get; }

        /// <summary>The channel this claim is being offered through.</summary>
        public DiscoveryChannel Channel { get; }

        public override string ToString() => $"{Key} = {TrueValue} via {Channel}";
    }

    /// <summary>
    /// Exposes an aggregate's discoverable truth systematically (§24).
    /// <para>
    /// Gameplay systems must not hand-maintain a parallel fact database. Adding a new discoverable
    /// system means adding a provider here, not scattering knowledge bookkeeping across the codebase.
    /// </para>
    /// </summary>
    public interface IFactProvider
    {
        /// <summary>Which fact kinds this provider can speak to. Used to route discovery, not to filter truth.</summary>
        IReadOnlyList<AuthoredId> ProvidedFactKinds { get; }

        /// <summary>
        /// Claims currently true about <paramref name="subject"/> that could be learned through
        /// <paramref name="channel"/>. Returns truth; whether the player <i>gets</i> it is
        /// <see cref="KnowledgeDiscoveryService"/>'s call.
        /// </summary>
        IEnumerable<DiscoverableClaim> ClaimsAbout(WorldState world, EntityRef subject, DiscoveryChannel channel);
    }
}
