using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;

namespace Vivarium.Domain.Social
{
    /// <summary>Projects named authoring/explanation traits from the one latent personality space.</summary>
    public sealed class TraitProjectionEvaluator
    {
        public long Evaluate(TraitDefinition definition, SocialVector personality)
        {
            if (definition == null || personality == null)
            {
                throw new ArgumentNullException("Trait definition and personality are required.");
            }

            long score = definition.ProjectionBias;
            for (int i = 0; i < definition.ProjectionLinearTerms.Count; i++)
            {
                SocialLinearTerm term = definition.ProjectionLinearTerms[i];
                score = checked(score + SocialNumeric.Multiply(term.Coefficient, personality[term.Dimension]));
            }
            for (int i = 0; i < definition.ProjectionPairwiseTerms.Count; i++)
            {
                SocialPairwiseTerm term = definition.ProjectionPairwiseTerms[i];
                long product = SocialNumeric.Multiply(personality[term.Pair.First], personality[term.Pair.Second]);
                score = checked(score + SocialNumeric.Multiply(term.Coefficient, product));
            }

            return SocialNumeric.BoundedResponse(score);
        }

        public long Expected(TraitDefinition definition, BeliefDistribution belief)
        {
            if (definition == null || belief == null)
            {
                throw new ArgumentNullException("Trait definition and belief are required.");
            }

            long score = definition.ProjectionBias;
            for (int i = 0; i < definition.ProjectionLinearTerms.Count; i++)
            {
                SocialLinearTerm term = definition.ProjectionLinearTerms[i];
                score = checked(score + SocialNumeric.Multiply(term.Coefficient, belief.Mean[term.Dimension]));
            }
            for (int i = 0; i < definition.ProjectionPairwiseTerms.Count; i++)
            {
                SocialPairwiseTerm term = definition.ProjectionPairwiseTerms[i];
                long meanProduct = SocialNumeric.Multiply(belief.Mean[term.Pair.First], belief.Mean[term.Pair.Second]);
                score = checked(score + SocialNumeric.Multiply(term.Coefficient, meanProduct));
                score = checked(score + SocialNumeric.MultiplyCovariance(
                    term.Coefficient,
                    belief.Covariance(term.Pair.First, term.Pair.Second)));
            }

            return SocialNumeric.BoundedResponse(score);
        }
    }
}
