using Vivarium.Domain.Common;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Authored fact kinds. Persisted inside <see cref="FactKey"/>, so these strings are save data —
    /// renaming one is a content migration (§39).
    /// </summary>
    public static class FactKinds
    {
        public static readonly AuthoredId CharacterTrait = new AuthoredId("fact.character.trait");
        public static readonly AuthoredId CharacterNeed = new AuthoredId("fact.character.need");
        public static readonly AuthoredId CharacterActivity = new AuthoredId("fact.character.activity");
        public static readonly AuthoredId RelationshipStanding = new AuthoredId("fact.relationship.standing");
        public static readonly AuthoredId RelationshipResentment = new AuthoredId("fact.relationship.resentment");
        public static readonly AuthoredId EmploymentEmployer = new AuthoredId("fact.employment.employer");
        public static readonly AuthoredId HouseholdMembership = new AuthoredId("fact.household.membership");
        public static readonly AuthoredId DecisionInfluence = new AuthoredId("fact.decision.influence");
    }

    /// <summary>Authored qualitative bands used by <see cref="ObservedValue"/>.</summary>
    public static class ValueBands
    {
        public static readonly AuthoredId None = new AuthoredId("band.none");
        public static readonly AuthoredId Slight = new AuthoredId("band.slight");
        public static readonly AuthoredId Moderate = new AuthoredId("band.moderate");
        public static readonly AuthoredId Strong = new AuthoredId("band.strong");
        public static readonly AuthoredId Overwhelming = new AuthoredId("band.overwhelming");
    }
}
