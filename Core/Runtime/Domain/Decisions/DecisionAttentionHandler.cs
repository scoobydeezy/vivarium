using System;
using System.Collections.Generic;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Applies prospective character Auto-Hold policy when a qualifying Decision is created.</summary>
    public sealed class DecisionAutoHoldHandler : DomainEventHandler<DecisionCreatedEvent>
    {
        private readonly DefinitionCatalog _catalog;
        private readonly DecisionHoldPolicy _holdPolicy;
        private readonly DecisionResolutionService _resolution;

        public DecisionAutoHoldHandler(
            DefinitionCatalog catalog,
            DecisionHoldPolicy holdPolicy,
            DecisionResolutionService resolution)
            : base(DecisionDomainEventTypes.DecisionCreated)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _holdPolicy = holdPolicy ?? throw new ArgumentNullException(nameof(holdPolicy));
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        }

        protected override void Handle(
            DecisionCreatedEvent domainEvent,
            WorldState world,
            SimulationContext context)
        {
            if (!context.AllowsHeldDecisions ||
                _catalog.DecisionImportancePolicy == null ||
                world.Attention.PolicyFor(domainEvent.CharacterId) != AttentionPolicy.AutoHold ||
                !_catalog.Decisions.TryGetValue(domainEvent.DefinitionId, out DecisionDefinition definition) ||
                !definition.HoldEligible ||
                !world.Decisions.TryGet(domainEvent.DecisionId, out Decision decision) ||
                !decision.IsActive ||
                decision.Importance < _catalog.DecisionImportancePolicy.AutoHoldFloor)
            {
                return;
            }

            world.Attention.Hold(decision.Id);
            world.Attention.SetPolicy(decision.Id, AttentionPolicy.Hold);
            ResolveOverflow(world, decision.CharacterId, context);
        }

        private void ResolveOverflow(WorldState world, CharacterId characterId, SimulationContext context)
        {
            while (_holdPolicy.CharacterCapacityExceeded(HeldForCharacter(world, characterId)) ||
                   _holdPolicy.GlobalCapacityExceeded(world.Attention.HeldCount))
            {
                bool characterOverflow = _holdPolicy.CharacterCapacityExceeded(HeldForCharacter(world, characterId));
                var candidates = new List<Decision>();
                foreach (DecisionId heldId in world.Attention.HeldDecisions)
                {
                    if (!world.Decisions.TryGet(heldId, out Decision held) || !held.IsActive)
                    {
                        continue;
                    }
                    if (!characterOverflow || held.CharacterId == characterId)
                    {
                        candidates.Add(held);
                    }
                }

                Decision victim = _holdPolicy.SelectOverflowVictim(candidates);
                if (victim == null)
                {
                    throw new InvalidOperationException("Held-decision capacity overflow had no active victim.");
                }

                if (victim.PendingResolveEventId.IsSet)
                {
                    world.Scheduler.Cancel(victim.PendingResolveEventId);
                }
                DecisionResolution resolution = victim.IsAwaitingCommit
                    ? _resolution.Complete(
                        victim,
                        victim.PendingResolution.AcceptedRolls,
                        world.Clock.Now,
                        victim.PendingResolution.SupersededRolls)
                    : _resolution.Resolve(victim, context);
                DecisionResolutionCommitter.Commit(world, victim, resolution, context);
            }
        }

        private static int HeldForCharacter(WorldState world, CharacterId characterId)
        {
            int count = 0;
            foreach (DecisionId heldId in world.Attention.HeldDecisions)
            {
                if (world.Decisions.TryGet(heldId, out Decision held) &&
                    held.IsActive && held.CharacterId == characterId)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
