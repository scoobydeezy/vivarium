using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>Legacy locations begin open and are not player-managed.</summary>
    public sealed class SaveV12ToV13LocationAvailabilityMigration : ISaveMigration
    {
        public int FromSchemaVersion => 12;
        public int ToSchemaVersion => 13;

        public void Apply(SaveGameData data)
        {
            for (int i = 0; i < data.Locations.Count; i++)
                data.Locations[i].IsOpen = true;
        }
    }
}
