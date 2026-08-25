using System;
using System.Collections.Generic;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Derives one Decision's living Importance from its consolidated world-derived reasons.
    /// The maximum absolute expected score lets one strong reason outrank many trivial ones.
    /// </summary>
    public sealed class DecisionImportanceEvaluator
    {
        public int Evaluate(IReadOnlyList<CandidateReason> reasons)
        {
            if (reasons == null) throw new ArgumentNullException(nameof(reasons));

            long maximum = 0;
            for (int i = 0; i < reasons.Count; i++)
            {
                CandidateReason reason = reasons[i];
                if (reason == null) continue;
                long magnitude = Magnitude(reason.Evaluation.ExpectedScore);
                if (magnitude > maximum) maximum = magnitude;
            }
            return (int)Math.Min(maximum, SignalNumeric.Scale);
        }

        public int Evaluate(Decision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            long maximum = 0;
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (influence.IsRetracted) continue;
                long magnitude = Magnitude(influence.Evaluation.ExpectedScore);
                if (magnitude > maximum) maximum = magnitude;
            }
            return (int)Math.Min(maximum, SignalNumeric.Scale);
        }

        public bool Recompute(Decision decision) => decision.SetDerivedImportance(Evaluate(decision));

        private static long Magnitude(long value) =>
            value == long.MinValue ? long.MaxValue : Math.Abs(value);
    }
}
