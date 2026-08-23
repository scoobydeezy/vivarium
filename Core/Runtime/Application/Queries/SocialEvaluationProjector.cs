using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;

namespace Vivarium.Application.Queries
{
    /// <summary>
    /// Diagnostic/character-reasoning projection of the causal social trace. Player-facing surfaces
    /// must still apply player Knowledge before deciding which entries to reveal.
    /// </summary>
    public sealed class SocialEvaluationProjector
    {
        private readonly SocialPressureEvaluator _evaluator = new SocialPressureEvaluator();

        public SocialEvaluationView ProjectDiagnostic(
            WorldState world,
            DefinitionCatalog catalog,
            CharacterId observerId,
            CharacterId targetId,
            IReadOnlyList<AuthoredId> lenses,
            SocialPressureDefinition pressureDefinition,
            SocialEvaluationContext context)
        {
            if (world == null || catalog == null || pressureDefinition == null)
            {
                throw new ArgumentNullException("World, catalog, and pressure definition are required.");
            }

            var views = new List<SocialLensView>();
            for (int i = 0; i < lenses.Count; i++)
            {
                CompositeSocialEvaluationResult result = _evaluator.Evaluate(
                    world,
                    observerId,
                    targetId,
                    lenses[i],
                    context,
                    pressureDefinition,
                    catalog);
                var contributions = new List<SocialContributionView>();
                Add(result.PersonalityAppraisal.Contributions, contributions);
                Add(result.AdditionalContributions, contributions);
                views.Add(new SocialLensView(
                    lenses[i].Value,
                    result.PersonalityAppraisal.NormalizedAppraisal,
                    result.NormalizedAppraisal,
                    result.Strength.ToString(),
                    result.PersonalityAppraisal.UncertaintyEffect,
                    contributions));
            }

            return new SocialEvaluationView(observerId.Value, targetId.Value, views);
        }

        private static void Add(
            IReadOnlyList<SocialContribution> source,
            List<SocialContributionView> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(new SocialContributionView(
                    source[i].Kind.ToString(),
                    source[i].SourceId.Value,
                    source[i].Amount,
                    source[i].Explanation));
            }
        }
    }
}
