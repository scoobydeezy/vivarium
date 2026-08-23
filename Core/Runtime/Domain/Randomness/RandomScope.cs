using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Randomness
{
    /// <summary>
    /// The semantic address of a random stream: <c>(scope type, scope id)</c> (§14).
    /// <para>
    /// Keying randomness to <i>meaning</i> rather than consumption order is what makes a roll
    /// independent of unrelated RNG activity elsewhere in the world. Because the scope id is a
    /// runtime id, deterministic id allocation (§7) is a precondition for deterministic randomness.
    /// </para>
    /// </summary>
    public readonly struct RandomScope : IEquatable<RandomScope>
    {
        public RandomScope(AuthoredId scopeType, int scopeId)
        {
            ScopeType = scopeType;
            ScopeId = scopeId;
        }

        /// <summary>Stable authored scope kind, e.g. <c>rng.scope.decision</c>.</summary>
        public AuthoredId ScopeType { get; }

        /// <summary>Runtime id of the scoped entity, or 0 for world scope.</summary>
        public int ScopeId { get; }

        public static RandomScope World => new RandomScope(RandomScopeTypes.World, 0);

        public static RandomScope Decision(DecisionIdLike decision) => new RandomScope(RandomScopeTypes.Decision, decision.Value);

        public static RandomScope Of(AuthoredId scopeType, int scopeId) => new RandomScope(scopeType, scopeId);

        public static RandomScope Of(AuthoredId scopeType, IRuntimeId id) => new RandomScope(scopeType, id.Value);

        public bool Equals(RandomScope other) => ScopeType.Equals(other.ScopeType) && ScopeId == other.ScopeId;

        public override bool Equals(object obj) => obj is RandomScope other && Equals(other);

        public override int GetHashCode() => (ScopeType.GetHashCode() * 397) ^ ScopeId;

        public override string ToString() => $"{ScopeType}#{ScopeId}";
    }

    /// <summary>
    /// Lets <see cref="RandomScope.Decision"/> accept any runtime id without the Randomness folder
    /// taking a dependency on the Decisions feature.
    /// </summary>
    public readonly struct DecisionIdLike
    {
        public DecisionIdLike(int value) => Value = value;

        public int Value { get; }

        public static implicit operator DecisionIdLike(int value) => new DecisionIdLike(value);
    }

    /// <summary>Authored scope-type ids. Stable forever; they participate in the seed.</summary>
    public static class RandomScopeTypes
    {
        public static readonly AuthoredId World = new AuthoredId("rng.scope.world");
        public static readonly AuthoredId Character = new AuthoredId("rng.scope.character");
        public static readonly AuthoredId Decision = new AuthoredId("rng.scope.decision");
        public static readonly AuthoredId Activity = new AuthoredId("rng.scope.activity");
        public static readonly AuthoredId Relationship = new AuthoredId("rng.scope.relationship");
        public static readonly AuthoredId Location = new AuthoredId("rng.scope.location");
        public static readonly AuthoredId TravelSegment = new AuthoredId("rng.scope.travel_segment");
        public static readonly AuthoredId Group = new AuthoredId("rng.scope.group");
    }

    /// <summary>
    /// Authored purpose ids (§14). Purposes must never be method names, display strings, or anything
    /// else a refactor can rename — renaming one silently changes every future roll it seeds.
    /// </summary>
    public static class RandomPurposes
    {
        public static readonly AuthoredId DecisionInfluenceRoll = new AuthoredId("rng.decision.influence_roll");
        public static readonly AuthoredId DecisionGeneration = new AuthoredId("rng.decision.generation");
        public static readonly AuthoredId RelationshipInteraction = new AuthoredId("rng.relationship.interaction");
        public static readonly AuthoredId CharacterInitialTrait = new AuthoredId("rng.character.initial_trait");
        public static readonly AuthoredId ActivityPerformance = new AuthoredId("rng.activity.performance");
        public static readonly AuthoredId InteractionCandidateSample = new AuthoredId("rng.interaction.candidate_sample");
        public static readonly AuthoredId KnowledgeDiscovery = new AuthoredId("rng.knowledge.discovery");

        /// <summary>
        /// Composes a sub-purpose such as <c>rng.decision.influence_roll/option.accept</c> so distinct
        /// options and influences within one decision draw independent streams.
        /// </summary>
        public static AuthoredId Qualified(AuthoredId purpose, string qualifier) =>
            new AuthoredId(purpose.Value + "/" + qualifier);
    }
}
