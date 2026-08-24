using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// v7 adds an optional post-arrival Activity continuation to travel payloads. Missing payload
    /// fields decode as an unset continuation, which exactly preserves the v6 arrival behavior.
    /// </summary>
    public sealed class SaveV6ToV7TravelContinuationMigration : ISaveMigration
    {
        public int FromSchemaVersion => 6;
        public int ToSchemaVersion => 7;
        public void Apply(SaveGameData data) { }
    }
}
