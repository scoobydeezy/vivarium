using Vivarium.Application.Persistence;
using Vivarium.Domain.Decisions;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>Every schema-v2 influence and retained roll used the former supporting-only model.</summary>
    public sealed class SaveV2ToV3DecisionPolarityMigration : ISaveMigration
    {
        public int FromSchemaVersion => 2;
        public int ToSchemaVersion => 3;

        public void Apply(SaveGameData data)
        {
            for (int d = 0; d < data.Decisions.Count; d++)
            {
                DecisionData decision = data.Decisions[d];
                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    decision.Influences[i].Polarity = (int)InfluencePolarity.Supporting;
                    decision.Influences[i].ReasonChannelId = decision.Influences[i].Category;
                }
                for (int r = 0; r < decision.Rolls.Count; r++)
                {
                    decision.Rolls[r].Polarity = (int)InfluencePolarity.Supporting;
                }
            }
        }
    }
}
