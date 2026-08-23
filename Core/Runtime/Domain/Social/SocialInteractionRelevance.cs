using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Social
{
    /// <summary>Uses calibrated directional appraisal to rank already-bounded interaction candidates.</summary>
    public sealed class SocialInteractionRelevance : IInteractionRelevance
    {
        private readonly WorldState _world;
        private readonly DefinitionCatalog _catalog;
        private readonly SocialPressureDefinition _pressure;
        private readonly AuthoredId _lensId;
        private readonly SocialPressureEvaluator _evaluator = new SocialPressureEvaluator();

        public SocialInteractionRelevance(
            WorldState world,
            DefinitionCatalog catalog,
            SocialPressureDefinition pressure,
            AuthoredId lensId)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _pressure = pressure ?? throw new ArgumentNullException(nameof(pressure));
            _lensId = lensId;
        }

        public long Score(CharacterId actor, CharacterId candidate)
        {
            if (!_world.Characters.TryGet(actor, out Characters.Character character) ||
                !character.TryGetAppraisalField(_lensId, out AppraisalField _))
            {
                if (_world.RelationshipIndex.TryGetBetween(actor, candidate, out RelationshipId relationshipId))
                {
                    return _world.Relationships.Get(relationshipId).From(actor).FamiliarityAt(_world.Clock.Now);
                }
                return 0;
            }

            return _evaluator.Evaluate(
                _world,
                actor,
                candidate,
                _lensId,
                new SocialEvaluationContext(),
                _pressure,
                _catalog).NormalizedAppraisal;
        }
    }
}
