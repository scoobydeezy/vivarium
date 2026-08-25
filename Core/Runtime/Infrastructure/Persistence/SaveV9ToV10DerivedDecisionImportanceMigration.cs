using System;
using Vivarium.Application.Persistence;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>v10 replaces authored Decision importance with magnitude derived from saved reasons.</summary>
    public sealed class SaveV9ToV10DerivedDecisionImportanceMigration : ISaveMigration
    {
        public int FromSchemaVersion => 9;
        public int ToSchemaVersion => 10;

        public void Apply(SaveGameData data)
        {
            for (int d = 0; d < data.Decisions.Count; d++)
            {
                DecisionData decision = data.Decisions[d];
                long maximum = 0;
                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    DecisionInfluenceData influence = decision.Influences[i];
                    if (influence.IsRetracted || influence.Evaluation == null) continue;
                    long score = influence.Evaluation.ExpectedScore;
                    long magnitude = score == long.MinValue ? long.MaxValue : Math.Abs(score);
                    if (magnitude > maximum) maximum = magnitude;
                }
                decision.Importance = (int)Math.Min(maximum, SignalNumeric.Scale);
            }
        }
    }
}
