using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>v8 adds optional persisted Employment identities and obligation-pattern snapshots.</summary>
    public sealed class SaveV7ToV8EmploymentMigration : ISaveMigration
    {
        public int FromSchemaVersion => 7;
        public int ToSchemaVersion => 8;
        public void Apply(SaveGameData data) { }
    }
}
