using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
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

        public DecisionResolution Resolve(Decision decision, SimulationContext context)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            var rolls = new List<InfluenceRoll>();
            var totalsByOption = new SortedDictionary<AuthoredId, int>();

            IReadOnlyList<DecisionOption> options = decision.Options;
            for (int i = 0; i < options.Count; i++)
            {
                totalsByOption[options[i].Id] = 0;
            }

            var scope = new RandomScope(RandomScopeTypes.Decision, decision.Id.Value);

            // Influences roll in stable id order so the trace reads the same on every replay (§15).
            var live = new List<DecisionInfluence>();
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (!influence.IsRetracted && influence.CurrentDie.IsSet)
                {
                    live.Add(influence);
                }
            }

            live.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < live.Count; i++)
            {
                DecisionInfluence influence = live[i];
                AuthoredId purpose = PurposeFor(influence);
                int rolled = context.Random.RollDie(scope, purpose, influence.RollIndex, influence.CurrentDie.Sides);

                rolls.Add(new InfluenceRoll(influence.Id, influence.OptionId, influence.CurrentDie, rolled, influence.RollIndex));

                if (totalsByOption.TryGetValue(influence.OptionId, out int running))
                {
                    totalsByOption[influence.OptionId] = running + rolled;
                }
                else
                {
                    // An influence for an option the decision does not declare is a content error;
                    // fail loudly rather than silently dropping a reason the character actually had.
                    throw new InvalidOperationException(
                        $"Influence {influence.Id} on decision {decision.Id} argues for unknown option '{influence.OptionId}'.");
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
                context.World.Clock.Now,
                optionTotals,
                rolls,
                OutcomeSource.Automatic);
        }

        /// <summary>
        /// The authored random purpose for one influence. Built from authored option and label ids plus
        /// the influence's stable within-decision id — never from method names or display strings (§14).
        /// </summary>
        public static AuthoredId PurposeFor(DecisionInfluence influence) =>
            RandomPurposes.Qualified(
                RandomPurposes.DecisionInfluenceRoll,
                influence.OptionId.Value + "/" + influence.LabelId.Value + "#" + influence.Id.Value);

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
