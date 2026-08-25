using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Owns how one option-relative roll changes that option's score.</summary>
    public interface IDecisionResolutionPolicy
    {
        int ApplyRoll(int currentTotal, InfluenceRoll roll);
    }

    public sealed class SignedOptionRelativeResolutionPolicy : IDecisionResolutionPolicy
    {
        public int ApplyRoll(int currentTotal, InfluenceRoll roll) =>
            roll.Polarity == InfluencePolarity.Supporting
                ? checked(currentTotal + roll.Rolled)
                : checked(currentTotal - roll.Rolled);
    }

    /// <summary>
    /// Resolves a Decision through deterministic dice (§18).
    /// <para>
    /// Each live influence rolls its current die in its own random stream, keyed to the decision, the
    /// influence, and a roll index (§14). Unrelated RNG activity elsewhere in the world cannot perturb
    /// the result, and a reroll intervention is simply the next roll index.
    /// </para>
    /// <para>
    /// The player can change the odds. The player cannot pick the winner (§19).
    /// </para>
    /// </summary>
    public sealed class DecisionResolutionService
    {
        /// <summary>Margin thresholds separating degrees of success. Content may tune these (§57).</summary>
        private const int DecisiveMargin = 8;
        private const int ClearMargin = 4;
        private const int MarginalMargin = 2;
        private readonly IDecisionResolutionPolicy _policy;

        public DecisionResolutionService(IDecisionResolutionPolicy policy = null)
        {
            _policy = policy ?? new SignedOptionRelativeResolutionPolicy();
        }

        public DecisionResolution Resolve(Decision decision, SimulationContext context)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            return Complete(decision, ProduceRolls(decision, context), context.World.Clock.Now);
        }

        /// <summary>Freezes the current participating dice and produces their initial results.</summary>
        public IReadOnlyList<InfluenceRoll> ProduceRolls(Decision decision, SimulationContext context)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            var rolls = new List<InfluenceRoll>();
            var scope = new RandomScope(RandomScopeTypes.Decision, decision.Id.Value);

            var live = new List<DecisionInfluence>();
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (!influence.IsRetracted && influence.CurrentDie.IsSet) live.Add(influence);
            }
            live.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < live.Count; i++)
            {
                DecisionInfluence influence = live[i];
                int rolled = Roll(context, scope, PurposeFor(influence), influence.RollIndex, influence.CurrentDie);
                rolls.Add(new InfluenceRoll(influence.Id, influence.OptionId, influence.CurrentDie, rolled,
                    influence.RollIndex, influence.Polarity, FrozenDecisionReason.From(influence)));
            }
            return rolls;
        }

        /// <summary>Produces the next deterministic result for one frozen pending Influence.</summary>
        public InfluenceRoll Reroll(Decision decision, DecisionInfluenceId influenceId, SimulationContext context)
        {
            if (decision?.PendingResolution == null ||
                !decision.PendingResolution.TryGetAccepted(influenceId, out InfluenceRoll previous) ||
                !decision.TryGetInfluence(influenceId, out DecisionInfluence influence))
                throw new InvalidOperationException("Re-roll requires a participating frozen Influence.");
            influence.Reroll();
            var scope = new RandomScope(RandomScopeTypes.Decision, decision.Id.Value);
            int rolled = Roll(context, scope, PurposeFor(previous), influence.RollIndex, previous.Die);
            return new InfluenceRoll(previous.InfluenceId, previous.OptionId, previous.Die, rolled,
                influence.RollIndex, previous.Polarity, previous.Reason);
        }

        /// <summary>Calculates the outcome exclusively from the frozen accepted roll set.</summary>
        public DecisionResolution Complete(Decision decision, IReadOnlyList<InfluenceRoll> rolls, SimTime resolvedAt,
            IReadOnlyList<InfluenceRoll> supersededRolls = null)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            var totalsByOption = new SortedDictionary<AuthoredId, int>();

            IReadOnlyList<DecisionOption> options = decision.Options;
            for (int i = 0; i < options.Count; i++)
            {
                totalsByOption[options[i].Id] = 0;
            }

            for (int i = 0; i < rolls.Count; i++)
            {
                InfluenceRoll roll = rolls[i];
                if (totalsByOption.TryGetValue(roll.OptionId, out int running))
                {
                    totalsByOption[roll.OptionId] = _policy.ApplyRoll(running, roll);
                }
                else
                {
                    // An influence for an option the decision does not declare is a content error;
                    // fail loudly rather than silently dropping a reason the character actually had.
                    throw new InvalidOperationException(
                        $"Influence {roll.InfluenceId} on decision {decision.Id} argues for unknown option '{roll.OptionId}'.");
                }
            }

            var optionTotals = new List<OptionTotal>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                optionTotals.Add(new OptionTotal(options[i].Id, totalsByOption[options[i].Id], options[i].OrderIndex));
            }

            // Highest total wins; ties break by authored option order, never by iteration order (§15).
            OptionTotal best = optionTotals[0];
            OptionTotal runnerUp = optionTotals[0];
            bool haveRunnerUp = false;

            for (int i = 1; i < optionTotals.Count; i++)
            {
                OptionTotal candidate = optionTotals[i];
                if (candidate.Total > best.Total || (candidate.Total == best.Total && candidate.OrderIndex < best.OrderIndex))
                {
                    runnerUp = best;
                    haveRunnerUp = true;
                    best = candidate;
                }
                else if (!haveRunnerUp || candidate.Total > runnerUp.Total)
                {
                    runnerUp = candidate;
                    haveRunnerUp = true;
                }
            }

            int margin = haveRunnerUp ? best.Total - runnerUp.Total : best.Total;

            return new DecisionResolution(
                best.OptionId,
                DegreeFor(margin),
                resolvedAt,
                optionTotals,
                rolls,
                OutcomeSource.Automatic,
                supersededRolls);
        }

        /// <summary>
        /// The authored random purpose for one influence. Built from authored option and label ids plus
        /// the influence's stable within-decision id — never from method names or display strings (§14).
        /// </summary>
        public static AuthoredId PurposeFor(DecisionInfluence influence) =>
            RandomPurposes.Qualified(
                RandomPurposes.DecisionInfluenceRoll,
                influence.OptionId.Value + "/" + influence.LabelId.Value + "#" + influence.Id.Value);

        public static AuthoredId PurposeFor(InfluenceRoll roll) => RandomPurposes.Qualified(
            RandomPurposes.DecisionInfluenceRoll,
            roll.OptionId.Value + "/" + (roll.Reason?.LabelId.Value ?? string.Empty) + "#" + roll.InfluenceId.Value);

        private static int Roll(SimulationContext context, RandomScope scope, AuthoredId purpose, int index, Die die) =>
            die.IsFixed ? die.FixedResult : context.Random.RollDie(scope, purpose, index, die.Sides);

        private static DegreeOfSuccess DegreeFor(int margin)
        {
            if (margin >= DecisiveMargin)
            {
                return DegreeOfSuccess.Decisive;
            }

            if (margin >= ClearMargin)
            {
                return DegreeOfSuccess.Clear;
            }

            return margin >= MarginalMargin ? DegreeOfSuccess.Marginal : DegreeOfSuccess.Reluctant;
        }
    }
}
