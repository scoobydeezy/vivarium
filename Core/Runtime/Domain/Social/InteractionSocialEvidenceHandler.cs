using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// Turns a bounded shared-context interaction into character-held evidence. Candidate witnesses
    /// come from the existing occupancy/travel indexes; this never scans the population or all pairs.
    /// </summary>
    public sealed class InteractionSocialEvidenceHandler : DomainEventHandler<InteractionOccurredEvent>
    {
        private readonly DefinitionCatalog _catalog;
        private readonly SocialBeliefUpdateService _beliefs;
        private readonly AuthoredId _actionDefinitionId;
        private readonly int _maxWitnesses;

        public InteractionSocialEvidenceHandler(
            DefinitionCatalog catalog,
            SocialBeliefUpdateService beliefs,
            AuthoredId actionDefinitionId,
            int maxWitnesses = 8)
            : base(InteractionOccurredEvent.Type)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _beliefs = beliefs ?? throw new ArgumentNullException(nameof(beliefs));
            _actionDefinitionId = actionDefinitionId;
            _maxWitnesses = Math.Max(0, maxWitnesses);
        }

        protected override void Handle(InteractionOccurredEvent domainEvent, WorldState world, SimulationContext context)
        {
            if (!_catalog.SocialEvidence.TryGetValue(_actionDefinitionId, out SocialEvidenceDefinition definition))
            {
                return;
            }

            string contextValue = domainEvent.TravelSegment.HasValue
                ? "travel." + domainEvent.TravelSegment.Value.From.Value + "." + domainEvent.TravelSegment.Value.To.Value
                : "location." + domainEvent.LocationId.Value;
            var sourceContext = new AuthoredId("social.context." + contextValue);

            Apply(world, domainEvent.Counterpart, domainEvent.Actor, definition, sourceContext);
            Apply(world, domainEvent.Actor, domainEvent.Counterpart, definition, sourceContext);

            IReadOnlyCollection<CharacterId> pool = domainEvent.TravelSegment.HasValue
                ? world.Spatial.TravelersOn(domainEvent.TravelSegment.Value)
                : world.Spatial.DirectOccupantsOf(domainEvent.LocationId);
            var witnesses = new List<CharacterId>();
            foreach (CharacterId candidate in pool)
            {
                if (candidate != domainEvent.Actor && candidate != domainEvent.Counterpart)
                {
                    witnesses.Add(candidate);
                }
            }
            witnesses.Sort();

            for (int i = 0; i < witnesses.Count && i < _maxWitnesses; i++)
            {
                Apply(world, witnesses[i], domainEvent.Actor, definition, sourceContext);
                Apply(world, witnesses[i], domainEvent.Counterpart, definition, sourceContext);
            }
        }

        private void Apply(
            WorldState world,
            CharacterId observer,
            CharacterId actor,
            SocialEvidenceDefinition definition,
            AuthoredId sourceContext)
        {
            if (!world.Characters.TryGet(observer, out Characters.Character character) || !character.IsActive)
            {
                return;
            }

            _beliefs.Apply(
                world,
                new ObservedSocialEvidence(
                    actor,
                    ObserverRef.Character(observer),
                    definition.ActionDefinitionId,
                    world.Clock.Now,
                    sourceContext),
                definition);
        }
    }
}
