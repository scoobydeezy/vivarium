using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Queries
{
    /// <summary>
    /// Projects a true Decision into the player-facing view (§26).
    /// <para>
    /// This is where truth, knowledge, and presentation stay separated (§2.3). The Domain constructed
    /// the <b>true</b> influence set; this decides how much of it the player sees, from content's
    /// visibility policy widened by what the player has actually learned.
    /// </para>
    /// <para>
    /// Truth: Mina fears disappointing Glen, d8. Knowledge: the player knows she cares about Glen but
    /// has not identified this fear. Presentation: "Personal concern d8".
    /// </para>
    /// </summary>
    public sealed class DecisionProjector
    {
        private readonly IReadOnlyDictionary<AuthoredId, InterventionDefinition> _interventions;

        /// <param name="interventions">
        /// Used to answer "should this control be enabled?" with the same rules the command handler
        /// enforces (§19). Pass an empty dictionary to project without intervention affordances.
        /// </param>
        public DecisionProjector(IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions = null)
        {
            _interventions = interventions ?? new Dictionary<AuthoredId, InterventionDefinition>();
        }

        public DecisionView Project(WorldState world, Decision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            string characterName = world.Characters.TryGet(decision.CharacterId, out Character character)
                ? character.DisplayName
                : decision.CharacterId.ToString();

            var options = new List<DecisionOptionView>(decision.Options.Count);

            for (int o = 0; o < decision.Options.Count; o++)
            {
                DecisionOption option = decision.Options[o];
                var influences = new List<InfluenceView>();

                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    DecisionInfluence influence = decision.Influences[i];
                    if (influence.OptionId != option.Id || influence.IsRetracted)
                    {
                        continue;
                    }

                    InfluenceView view = ProjectInfluence(world, decision, influence);
                    if (view != null)
                    {
                        influences.Add(view);
                    }
                }

                options.Add(new DecisionOptionView(option.Id.Value, option.LabelId.Value, influences));
            }

            DecisionResolutionView resolution = decision.Resolution == null
                ? null
                : new DecisionResolutionView(
                    decision.Resolution.ChosenOptionId.Value,
                    decision.Resolution.Degree.ToString(),
                    decision.Resolution.ResolvedAt.ToString(),
                    decision.Resolution.Source.ToString());

            return new DecisionView(
                decision.Id.Value,
                decision.CharacterId.Value,
                characterName,
                decision.DefinitionId.Value,
                decision.Status.ToString(),
                decision.ResolveAt.ToString(),
                decision.InfluenceRevision,
                world.Attention.IsHeld(decision.Id),
                decision.IsActive,
                options,
                resolution);
        }

        /// <summary>
        /// Applies visibility policy to one influence.
        /// <para>
        /// Returns <c>null</c> when the influence should not be shown at all — and the caller must not
        /// substitute a placeholder, because the <i>number</i> of hidden influences is not exposed
        /// either (§26).
        /// </para>
        /// </summary>
        private InfluenceView ProjectInfluence(WorldState world, Decision decision, DecisionInfluence influence)
        {
            InfluenceVisibility visibility = EffectiveVisibility(world, influence);

            if ((visibility & InfluenceVisibility.Existence) == 0)
            {
                return null;
            }

            string label = (visibility & InfluenceVisibility.Label) != 0 ? influence.LabelId.Value : null;
            string category = (visibility & InfluenceVisibility.Category) != 0 ? influence.Category.Value : null;
            int? dieSides = (visibility & InfluenceVisibility.Magnitude) != 0 ? influence.CurrentDie.Sides : (int?)null;
            string explanation = (visibility & InfluenceVisibility.Explanation) != 0 ? influence.LabelId.Value : null;

            return new InfluenceView(
                influence.Id.Value,
                label,
                category,
                dieSides,
                explanation,
                AnyInterventionAvailable(decision, influence.Id));
        }

        /// <summary>
        /// Content's default visibility, widened by what the player knows.
        /// <para>
        /// Knowledge can only ever <i>reveal</i> here. It never hides something content chose to show,
        /// which keeps "why can I see this?" answerable.
        /// </para>
        /// </summary>
        private static InfluenceVisibility EffectiveVisibility(WorldState world, DecisionInfluence influence)
        {
            InfluenceVisibility visibility = influence.DefaultVisibility;

            if (!influence.Subject.IsSet)
            {
                return visibility;
            }

            // Knowing the underlying fact promotes a generalized influence to a specific one.
            var influenceFact = new FactKey(FactKinds.DecisionInfluence, influence.Subject, influence.LabelId);
            var legacyTraitFact = new FactKey(FactKinds.CharacterTrait, influence.Subject, influence.LabelId);
            if (world.Knowledge.Knows(influenceFact) || world.Knowledge.Knows(legacyTraitFact))
            {
                visibility |= InfluenceVisibility.Label | InfluenceVisibility.Explanation;
            }

            return visibility;
        }

        private bool AnyInterventionAvailable(Decision decision, DecisionInfluenceId influenceId)
        {
            foreach (KeyValuePair<AuthoredId, InterventionDefinition> pair in _interventions)
            {
                if (DecisionInterventionRules.Evaluate(decision, pair.Value, influenceId).IsSuccess)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
