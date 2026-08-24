using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>v9 adds optional location Activity affordances and travel-continuation parameters.</summary>
    public sealed class SaveV8ToV9ActivityAffordanceMigration : ISaveMigration
    {
        public int FromSchemaVersion => 8;
        public int ToSchemaVersion => 9;
        public void Apply(SaveGameData data) { }
    }
}
