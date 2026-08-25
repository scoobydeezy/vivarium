using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>Legacy saves had neither frozen pending rolls nor non-Nudge availability state.</summary>
    public sealed class SaveV11ToV12DecisionInterventionsMigration : ISaveMigration
    {
        public int FromSchemaVersion => 11;
        public int ToSchemaVersion => 12;
        public void Apply(SaveGameData data) { }
    }
}
