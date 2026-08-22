using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Stable identity of an influence <i>within</i> its Decision (§17.2).
    /// <para>
    /// Interventions target this id, never a position in a collection. The influence set can grow,
    /// shrink, or reorder while a Decision is open, and an already-applied intervention must never
    /// silently retarget as a result (invariant 37).
    /// </para>
    /// </summary>
    public readonly struct DecisionInfluenceId : IEquatable<DecisionInfluenceId>, IComparable<DecisionInfluenceId>
    {
        public static readonly DecisionInfluenceId None = default;

        public DecisionInfluenceId(int value) => Value = value;

        public int Value { get; }

        public bool IsSet => Value > 0;

        public bool Equals(DecisionInfluenceId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is DecisionInfluenceId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(DecisionInfluenceId other) => Value.CompareTo(other.Value);

        public override string ToString() => IsSet ? "Influence#" + Value : "<none>";

        public static bool operator ==(DecisionInfluenceId a, DecisionInfluenceId b) => a.Value == b.Value;

        public static bool operator !=(DecisionInfluenceId a, DecisionInfluenceId b) => a.Value != b.Value;
    }

    /// <summary>
    /// What the presentation layer is permitted to reveal about an influence (§26).
    /// <para>
    /// The four facets are independently controllable, which is why <c>??? d8</c> and
    /// <c>Personal concern d8</c> and nothing-at-all are all legal renderings of the same truth
    /// (invariants 9–10). Unknown information does not imply a hidden die.
    /// </para>
    /// </summary>
    [Flags]
    public enum InfluenceVisibility
    {
        /// <summary>Not shown at all. The count of hidden influences is not exposed either (§26).</summary>
        Hidden = 0,

        /// <summary>The player can see that <i>something</i> is pulling here.</summary>
        Existence = 1,

        /// <summary>The broad category is legible: "Personal concern", "Friendship concern".</summary>
        Category = 2,

        /// <summary>The specific label is legible: "Fear of disappointing Glen".</summary>
        Label = 4,

        /// <summary>The die size is legible.</summary>
        Magnitude = 8,

        /// <summary>The full explanation is legible.</summary>
        Explanation = 16,

        /// <summary>Everything.</summary>
        Full = Existence | Category | Label | Magnitude | Explanation,
    }

    /// <summary>
    /// One true reason pulling a character toward an option (§17).
    /// <para>
    /// This is <b>world truth</b>. How much of it the player sees is decided by the projection layer
    /// from content policy plus player Knowledge — never by hiding it here (§2.3, §26).
    /// </para>
    /// </summary>
    public sealed class DecisionInfluence
    {
        public DecisionInfluence(
            DecisionInfluenceId id,
            AuthoredId optionId,
            AuthoredId category,
            AuthoredId labelId,
            Die baseDie,
            InfluenceVisibility defaultVisibility,
            DecisionDependencyKey dependencyKey = default,
            EntityRef subject = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An influence needs a stable id within its Decision (§17.2).", nameof(id));
            }

            Id = id;
            OptionId = optionId;
            Category = category;
            LabelId = labelId;
            BaseDie = baseDie;
            CurrentDie = baseDie;
            DefaultVisibility = defaultVisibility;
            DependencyKey = dependencyKey;
            Subject = subject;
        }

        public DecisionInfluenceId Id { get; }

        /// <summary>The option this influence argues for.</summary>
        public AuthoredId OptionId { get; }

        /// <summary>Authored category, e.g. <c>influence_category.personal_concern</c>.</summary>
        public AuthoredId Category { get; }

        /// <summary>Authored label id, e.g. <c>influence.fear_of_disappointing</c>.</summary>
        public AuthoredId LabelId { get; }

        /// <summary>The die as first constructed, before any intervention.</summary>
        public Die BaseDie { get; }

        /// <summary>The die that will actually be rolled, after interventions and world changes.</summary>
        public Die CurrentDie { get; private set; }

        /// <summary>Content's default visibility policy before player Knowledge is applied (§26).</summary>
        public InfluenceVisibility DefaultVisibility { get; private set; }

        /// <summary>
        /// What world state this influence derives from, so a relevant change can find this Decision
        /// through the dependency index instead of rescanning every open Decision (§17.2).
        /// </summary>
        public DecisionDependencyKey DependencyKey { get; }

        /// <summary>Who or what the influence is about — Glen, the apartment, the employer.</summary>
        public EntityRef Subject { get; }

        /// <summary>Whether the influence has been removed by a world change but retained for audit.</summary>
        public bool IsRetracted { get; private set; }

        /// <summary>
        /// Which roll index the oracle should use for this influence (§14). A reroll intervention
        /// advances it, so the new result is independent of the old one and of everything else in the
        /// world — no stream state is consumed.
        /// </summary>
        public int RollIndex { get; private set; }

        /// <summary>Advances to the next roll index. This is what "reroll" means mechanically (§14, §19).</summary>
        public void Reroll() => RollIndex++;

        public void SetDie(Die die) => CurrentDie = die;

        public void SetVisibility(InfluenceVisibility visibility) => DefaultVisibility = visibility;

        /// <summary>
        /// Retracts the influence when the world stops supporting it. Kept rather than deleted so any
        /// intervention already bound to this id stays explicable (§17.2).
        /// </summary>
        public void Retract() => IsRetracted = true;

        public void Reinstate() => IsRetracted = false;

        public override string ToString() => $"{LabelId} {CurrentDie} → {OptionId}{(IsRetracted ? " (retracted)" : string.Empty)}";
    }
}
