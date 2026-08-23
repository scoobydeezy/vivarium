using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// Deterministically generates inspectable population-scale personality and preference fields.
    /// Culture is deliberately not an input until its separate design brief exists.
    /// </summary>
    public sealed class SocialProfileGenerator
    {
        private readonly IRandomOracle _random;

        public SocialProfileGenerator(IRandomOracle random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Generate(Character character, AuthoredId calibrationProfileId)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            var scope = new RandomScope(RandomScopeTypes.Character, character.Id.Value);
            var personality = new SocialVector();
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                AuthoredId dimension = SocialDimensions.Provisional[i];
                personality.Set(
                    dimension,
                    _random.Range(
                        scope,
                        RandomPurposes.Qualified(RandomPurposes.SocialPersonalityGeneration, dimension.Value),
                        0,
                        -10000,
                        10001));
            }
            character.SetPersonality(personality);

            character.SetAppraisalField(GenerateField(character, AppraisalLenses.Affiliation, calibrationProfileId, 0));
            character.SetAppraisalField(GenerateField(character, AppraisalLenses.Respect, calibrationProfileId, 100));
            character.SetAppraisalField(GenerateField(character, AppraisalLenses.Comfort, calibrationProfileId, 200));
        }

        private AppraisalField GenerateField(
            Character character,
            AuthoredId lensId,
            AuthoredId calibrationProfileId,
            int rollOffset)
        {
            var scope = new RandomScope(RandomScopeTypes.Character, character.Id.Value);
            var linear = new List<SocialLinearTerm>();
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                AuthoredId dimension = SocialDimensions.Provisional[i];
                int prior = Prior(lensId, dimension);
                int variation = _random.Range(
                    scope,
                    RandomPurposes.Qualified(
                        RandomPurposes.SocialPreferenceGeneration,
                        lensId.Value + "/" + dimension.Value),
                    rollOffset + i,
                    -2500,
                    2501);
                linear.Add(new SocialLinearTerm(
                    dimension,
                    IntegerMath.Clamp(prior + variation, -10000, 10000),
                    new AuthoredId("social.provenance.generated_prior")));
            }

            return new AppraisalField(
                character.Id,
                lensId,
                0,
                linear,
                new[]
                {
                    new SocialPairwiseTerm(
                        SocialDimensions.Agency,
                        SocialDimensions.Attunement,
                        lensId == AppraisalLenses.Comfort ? 3500 : 1000,
                        new AuthoredId("social.provenance.agency_attunement")),
                },
                character.Personality.Copy(),
                new[]
                {
                    new IdealFactor(
                        new AuthoredId("social.factor.generated_tolerance." + lensId.Value.Substring("social.lens.".Length)),
                        new[]
                        {
                            new SocialLinearTerm(
                                PrimaryIdealDimension(lensId),
                                2000,
                                new AuthoredId("social.provenance.generated_broad_tolerance")),
                        },
                        new AuthoredId("social.provenance.generated_broad_tolerance")),
                },
                null,
                calibrationProfileId);
        }

        private static AuthoredId PrimaryIdealDimension(AuthoredId lens)
        {
            if (lens == AppraisalLenses.Respect) return SocialDimensions.Discipline;
            if (lens == AppraisalLenses.Comfort) return SocialDimensions.Attunement;
            return SocialDimensions.Warmth;
        }

        private static int Prior(AuthoredId lens, AuthoredId dimension)
        {
            if (lens == AppraisalLenses.Affiliation)
            {
                if (dimension == SocialDimensions.Warmth) return 5000;
                if (dimension == SocialDimensions.Sociability) return 2500;
                if (dimension == SocialDimensions.Attunement) return 3500;
            }
            else if (lens == AppraisalLenses.Respect)
            {
                if (dimension == SocialDimensions.Discipline) return 5000;
                if (dimension == SocialDimensions.Stability) return 4000;
                if (dimension == SocialDimensions.Agency) return 3000;
            }
            else if (lens == AppraisalLenses.Comfort)
            {
                if (dimension == SocialDimensions.Warmth) return 4000;
                if (dimension == SocialDimensions.Attunement) return 5500;
                if (dimension == SocialDimensions.Stability) return 2500;
            }

            return 0;
        }
    }
}
