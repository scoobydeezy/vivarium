using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>v5 adds optional authoritative commitment-conflict plans and identity.</summary>
    public sealed class SaveV4ToV5CommitmentConflictMigration : ISaveMigration
    {
        public int FromSchemaVersion => 4;
        public int ToSchemaVersion => 5;
        public void Apply(SaveGameData data) { }
    }
}
