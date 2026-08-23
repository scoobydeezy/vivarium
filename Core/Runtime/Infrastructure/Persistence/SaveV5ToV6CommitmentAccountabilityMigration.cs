using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>v6 reserves Commitment outcome identity and accountability snapshots.</summary>
    public sealed class SaveV5ToV6CommitmentAccountabilityMigration : ISaveMigration
    {
        public int FromSchemaVersion => 5;
        public int ToSchemaVersion => 6;
        public void Apply(SaveGameData data) { }
    }
}
