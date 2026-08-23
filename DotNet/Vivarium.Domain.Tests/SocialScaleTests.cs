using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class SocialScaleTests
    {
        [Fact]
        public void TwoThousandCharacterProfilesAndSparseBeliefsAreDeterministicAndLinear()
        {
            const int population = 2000;
            const int edgesPerCharacter = 4;

            (ulong firstHash, int firstEdges) = Build(population, edgesPerCharacter, 827119);
            (ulong secondHash, int secondEdges) = Build(population, edgesPerCharacter, 827119);

            Assert.Equal(firstHash, secondHash);
            Assert.Equal(population * edgesPerCharacter, firstEdges);
            Assert.Equal(firstEdges, secondEdges);
        }

        private static (ulong Hash, int Edges) Build(int population, int edgesPerCharacter, long seed)
        {
            var world = new WorldState(seed, new SimTime(0));
            var random = new DeterministicRandomOracle(seed);
            var generator = new SocialProfileGenerator(random);
            var ids = new CharacterId[population];
            ulong hash = 14695981039346656037UL;

            for (int i = 0; i < population; i++)
            {
                var character = new Character(world.RuntimeIds.Characters.Next(), "Synthetic " + i, world.Clock.Now);
                world.Characters.Add(character.Id, character);
                ids[i] = character.Id;
                generator.Generate(character, new AuthoredId("social.calibration.standard"));
                foreach (AuthoredId dimension in SocialDimensions.Provisional)
                {
                    hash = StableHash.Combine(hash, unchecked((ulong)character.Personality[dimension]));
                }
            }

            for (int i = 0; i < population; i++)
            {
                for (int edge = 1; edge <= edgesPerCharacter; edge++)
                {
                    CharacterId target = ids[(i + edge) % population];
                    BeliefDistribution belief = SocialBeliefUpdateService.BroadPrior();
                    belief.Mean.Set(SocialDimensions.Warmth, ((i + edge) % 20001) - 10000);
                    world.Knowledge.SetSocialBelief(
                        ObserverRef.Character(ids[i]),
                        target,
                        belief,
                        world.Clock.Now,
                        SocialBeliefRetention.Recent);
                    hash = StableHash.Combine(hash, unchecked((ulong)belief.Mean[SocialDimensions.Warmth]));
                }
            }

            int count = 0;
            foreach (var ignored in world.Knowledge.SocialBeliefs) count++;
            return (hash, count);
        }
    }
}
