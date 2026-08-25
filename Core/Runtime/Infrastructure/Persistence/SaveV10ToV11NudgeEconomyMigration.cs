using Vivarium.Application.Persistence;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.PlayerAgency;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>Adds the MVP Nudge account; legacy interventions were free and therefore refund nothing.</summary>
    public sealed class SaveV10ToV11NudgeEconomyMigration : ISaveMigration
    {
        public int FromSchemaVersion => 10;

        public int ToSchemaVersion => 11;

        public void Apply(SaveGameData data)
        {
            data.NudgeBalance = NudgePolicy.InitialBalance;
            data.NudgeRevision = 0;

            for (int d = 0; d < data.Decisions.Count; d++)
            {
                for (int i = 0; i < data.Decisions[d].Interventions.Count; i++)
                {
                    AppliedInterventionData intervention = data.Decisions[d].Interventions[i];
                    intervention.ResourceKind = (int)InterventionResourceKind.None;
                    intervention.ResourceCost = 0;
                }
            }
        }
    }
}
