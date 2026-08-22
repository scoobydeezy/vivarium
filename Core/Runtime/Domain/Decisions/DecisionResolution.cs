using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// How decisively the chosen option won (§18). The exact system is deferred content (§57); the
    /// architectural requirement is that a degree exists and is derived deterministically.
    /// </summary>
    public enum DegreeOfSuccess
    {
        /// <summary>Chosen, but barely — the alternatives nearly won.</summary>
        Reluctant = 0,

        Marginal = 1,

        Clear = 2,

        Decisive = 3,
    }

    /// <summary>One influence's roll, retained so a resolution can be explained and reproduced (§53).</summary>
    public readonly struct InfluenceRoll
    {
        public InfluenceRoll(DecisionInfluenceId influenceId, AuthoredId optionId, Die die, int rolled, int rollIndex)
        {
            InfluenceId = influenceId;
            OptionId = optionId;
            Die = die;
            Rolled = rolled;
            RollIndex = rollIndex;
        }

        public DecisionInfluenceId InfluenceId { get; }

        public AuthoredId OptionId { get; }

        public Die Die { get; }

        public int Rolled { get; }

        /// <summary>Which roll index produced this — 0 normally, higher after a reroll intervention (§14).</summary>
        public int RollIndex { get; }

        public override string ToString() => $"{InfluenceId} {Die} → {Rolled}";
    }

    /// <summary>Total rolled for one option.</summary>
    public readonly struct OptionTotal
    {
        public OptionTotal(AuthoredId optionId, int total, int orderIndex)
        {
            OptionId = optionId;
            Total = total;
            OrderIndex = orderIndex;
        }

        public AuthoredId OptionId { get; }

        public int Total { get; }

        public int OrderIndex { get; }

        public override string ToString() => $"{OptionId} = {Total}";
    }

    /// <summary>
    /// The outcome of a resolved Decision (§18).
    /// <para>
    /// Retains every roll and total. That is what makes "reproduce Decision #1837" (§52) a real
    /// capability rather than an aspiration.
    /// </para>
    /// </summary>
    public sealed class DecisionResolution
    {
        public DecisionResolution(
            AuthoredId chosenOptionId,
            DegreeOfSuccess degree,
            SimTime resolvedAt,
            IReadOnlyList<OptionTotal> optionTotals,
            IReadOnlyList<InfluenceRoll> rolls,
            OutcomeSource source)
        {
            ChosenOptionId = chosenOptionId;
            Degree = degree;
            ResolvedAt = resolvedAt;
            OptionTotals = optionTotals;
            Rolls = rolls;
            Source = source;
        }

        public AuthoredId ChosenOptionId { get; }

        public DegreeOfSuccess Degree { get; }

        public SimTime ResolvedAt { get; }

        public IReadOnlyList<OptionTotal> OptionTotals { get; }

        public IReadOnlyList<InfluenceRoll> Rolls { get; }

        /// <summary>Automatic dice resolution, or player-provided. Shared provenance convention (§29.6).</summary>
        public OutcomeSource Source { get; }

        public override string ToString() => $"{ChosenOptionId} ({Degree}) at {ResolvedAt}";
    }
}
