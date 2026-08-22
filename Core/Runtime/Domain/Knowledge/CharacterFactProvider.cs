using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Exposes a character's discoverable truth: traits and need states (§24).
    /// <para>
    /// A worked example of the provider pattern. Each new discoverable system gets a provider like
    /// this one rather than writing into the ledger from wherever the state happens to change.
    /// </para>
    /// </summary>
    public sealed class CharacterFactProvider : IFactProvider
    {
        private static readonly AuthoredId[] Kinds = { FactKinds.CharacterTrait, FactKinds.CharacterNeed };

        private readonly IReadOnlyDictionary<AuthoredId, TraitDefinition> _traits;

        /// <param name="traitDefinitions">
        /// Trait catalog, used to honour each trait's authored discovery channels (§24).
        /// </param>
        public CharacterFactProvider(IReadOnlyDictionary<AuthoredId, TraitDefinition> traitDefinitions)
        {
            _traits = traitDefinitions;
        }

        public IReadOnlyList<AuthoredId> ProvidedFactKinds => Kinds;

        public IEnumerable<DiscoverableClaim> ClaimsAbout(WorldState world, EntityRef subject, DiscoveryChannel channel)
        {
            if (subject.Kind != EntityKind.Character)
            {
                yield break;
            }

            if (!world.Characters.TryGet(new CharacterId(subject.RuntimeId), out Character character))
            {
                yield break;
            }

            foreach (AuthoredId traitId in character.Traits)
            {
                if (!IsDiscoverableThrough(traitId, channel))
                {
                    continue;
                }

                yield return new DiscoverableClaim(
                    new FactKey(FactKinds.CharacterTrait, subject, traitId),
                    ObservedValue.Of(traitId),
                    channel);
            }

            foreach (KeyValuePair<AuthoredId, NeedState> need in character.Needs)
            {
                yield return new DiscoverableClaim(
                    new FactKey(FactKinds.CharacterNeed, subject, need.Key),
                    ObservedValue.Of(need.Value.ValueAt(world.Clock.Now)),
                    channel);
            }
        }

        private bool IsDiscoverableThrough(AuthoredId traitId, DiscoveryChannel channel)
        {
            if (_traits == null || !_traits.TryGetValue(traitId, out TraitDefinition definition))
            {
                return false;
            }

            IReadOnlyList<DiscoveryChannel> channels = definition.DiscoverableThrough;
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Id.Equals(channel.Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
