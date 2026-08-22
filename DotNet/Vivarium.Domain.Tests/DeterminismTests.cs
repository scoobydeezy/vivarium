using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;
using Xunit;

namespace Vivarium.Domain.Tests
{
    /// <summary>
    /// Randomness and identity determinism (§7, §14, §15).
    /// </summary>
    public sealed class DeterminismTests
    {
        private static readonly AuthoredId Purpose = RandomPurposes.DecisionInfluenceRoll;

        [Fact]
        public void SameCoordinateAlwaysProducesTheSameRoll()
        {
            var first = new DeterministicRandomOracle(827119);
            var second = new DeterministicRandomOracle(827119);
            var scope = new RandomScope(RandomScopeTypes.Decision, 1837);

            Assert.Equal(
                first.RollDie(scope, Purpose, 0, 10),
                second.RollDie(scope, Purpose, 0, 10));
        }

        [Fact]
        public void RollIsIndependentOfUnrelatedRngActivity()
        {
            // The point of counter-based randomness: there is no stream to consume, so rolling for one
            // decision cannot perturb another (§14).
            var oracle = new DeterministicRandomOracle(827119);
            var mina = new RandomScope(RandomScopeTypes.Decision, 1837);
            var other = new RandomScope(RandomScopeTypes.Decision, 999);

            int before = oracle.RollDie(mina, Purpose, 0, 20);

            for (int i = 0; i < 1000; i++)
            {
                oracle.RollDie(other, Purpose, i, 20);
            }

            Assert.Equal(before, oracle.RollDie(mina, Purpose, 0, 20));
        }

        [Fact]
        public void RerollUsesTheNextRollIndexAndGenerallyDiffers()
        {
            var oracle = new DeterministicRandomOracle(827119);
            var scope = new RandomScope(RandomScopeTypes.Decision, 1837);

            // Not a guarantee for any single die, so check across a range of dice that the streams are
            // genuinely distinct rather than accidentally aliased.
            int differences = 0;
            for (int sides = 2; sides <= 20; sides++)
            {
                if (oracle.RollDie(scope, Purpose, 0, sides) != oracle.RollDie(scope, Purpose, 1, sides))
                {
                    differences++;
                }
            }

            Assert.True(differences > 10, "roll index 0 and 1 should produce visibly different streams");
        }

        [Fact]
        public void DifferentSeedsProduceDifferentWorlds()
        {
            var a = new DeterministicRandomOracle(1);
            var b = new DeterministicRandomOracle(2);
            var scope = new RandomScope(RandomScopeTypes.Decision, 1);

            int differences = 0;
            for (int i = 0; i < 32; i++)
            {
                if (a.RollDie(scope, Purpose, i, 20) != b.RollDie(scope, Purpose, i, 20))
                {
                    differences++;
                }
            }

            Assert.True(differences > 20, "distinct seeds should diverge quickly");
        }

        [Fact]
        public void PurposeSeparatesStreamsWithinOneScope()
        {
            var oracle = new DeterministicRandomOracle(827119);
            var scope = new RandomScope(RandomScopeTypes.Decision, 1837);

            AuthoredId ambition = RandomPurposes.Qualified(Purpose, "option.accept/influence.ambition");
            AuthoredId baking = RandomPurposes.Qualified(Purpose, "option.accept/influence.baking");

            int differences = 0;
            for (int sides = 2; sides <= 20; sides++)
            {
                if (oracle.RollDie(scope, ambition, 0, sides) != oracle.RollDie(scope, baking, 0, sides))
                {
                    differences++;
                }
            }

            Assert.True(differences > 10, "different purposes should be independent streams");
        }

        [Fact]
        public void DieRollsStayWithinBounds()
        {
            var oracle = new DeterministicRandomOracle(42);
            var scope = new RandomScope(RandomScopeTypes.Character, 7);

            for (int i = 0; i < 2000; i++)
            {
                int rolled = oracle.RollDie(scope, Purpose, i, 6);
                Assert.InRange(rolled, 1, 6);
            }
        }

        [Fact]
        public void ChanceHonoursBasisPointsWithinTolerance()
        {
            var oracle = new DeterministicRandomOracle(42);
            var scope = new RandomScope(RandomScopeTypes.World, 0);
            int hits = 0;

            for (int i = 0; i < 10000; i++)
            {
                if (oracle.Chance(scope, Purpose, i, 2500))
                {
                    hits++;
                }
            }

            // 25% of 10,000, with room for the distribution rather than an exact count.
            Assert.InRange(hits, 2200, 2800);
        }

        [Fact]
        public void StableStringHashDoesNotDependOnTheRuntime()
        {
            // Pinned values. If these ever change, RandomAlgorithmVersion must be bumped, because every
            // future roll in every existing save depends on them (§14).
            Assert.Equal(620337896427418084UL, StableHash.OfString("a"));
            Assert.Equal(14695981039346656037UL, StableHash.OfString(null));
            Assert.NotEqual(StableHash.OfString("trait.ambitious"), StableHash.OfString("trait.homebound"));
        }

        [Fact]
        public void AuthoredIdHashingIsStableRatherThanRuntimeDependent()
        {
            var id = new AuthoredId("rng.decision.influence_roll");

            Assert.Equal(StableHash.OfString("rng.decision.influence_roll"), id.StableHashCode);
        }

        [Fact]
        public void IdAllocationIsMonotonicAndNeverReused()
        {
            var allocator = new MonotonicIdAllocator<CharacterId>(v => new CharacterId(v));

            CharacterId first = allocator.Next();
            CharacterId second = allocator.Next();

            Assert.Equal(1, first.Value);
            Assert.Equal(2, second.Value);
            Assert.Equal(2, allocator.IssuedCount);
        }

        [Fact]
        public void RestoredAllocatorContinuesWhereTheSaveLeftOff()
        {
            // §7.1: retiring an entity does not release its identity, so a reload must not reissue it.
            var allocator = new MonotonicIdAllocator<DecisionId>(v => new DecisionId(v), alreadyIssued: 1836);

            Assert.Equal(1837, allocator.Next().Value);
        }

        [Fact]
        public void RemovingAnEntityDoesNotFreeItsIdForReuse()
        {
            var repository = new EntityRepository<CharacterId, string>("Character");
            var id = new CharacterId(1);

            repository.Add(id, "Mina");
            Assert.True(repository.Remove(id));

            // The identity is gone from the active repository, but Knowledge and history may still refer
            // to it, so re-adding the same id is a bug rather than a convenience.
            repository.Add(id, "someone else");
            Assert.Throws<System.InvalidOperationException>(() => repository.Add(id, "third"));
        }

        [Fact]
        public void RepositoryEnumeratesInIdOrderRegardlessOfInsertionOrder()
        {
            var repository = new EntityRepository<CharacterId, string>("Character");

            repository.Add(new CharacterId(30), "c");
            repository.Add(new CharacterId(10), "a");
            repository.Add(new CharacterId(20), "b");

            var order = new List<string>(repository.All);
            Assert.Equal(new[] { "a", "b", "c" }, order);
        }

        [Fact]
        public void IndexedMembershipIsBidirectionalAndOrdered()
        {
            var membership = new IndexedMembership<GroupId, CharacterId>();

            membership.Add(new GroupId(2), new CharacterId(5));
            membership.Add(new GroupId(1), new CharacterId(5));
            membership.Add(new GroupId(1), new CharacterId(3));

            Assert.Equal(new[] { new CharacterId(3), new CharacterId(5) }, membership.MembersOf(new GroupId(1)));
            Assert.Equal(new[] { new GroupId(1), new GroupId(2) }, membership.ContainersOf(new CharacterId(5)));

            membership.RemoveMember(new CharacterId(5));
            Assert.Empty(membership.ContainersOf(new CharacterId(5)));
            Assert.Equal(new[] { new CharacterId(3) }, membership.MembersOf(new GroupId(1)));
        }
    }
}
