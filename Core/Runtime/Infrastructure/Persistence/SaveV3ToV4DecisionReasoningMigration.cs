using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// Schema v4 adds optional typed Decision context and snapshotted reasoning programs. Existing
    /// v3 Decisions retain their authoritative direct Influences and therefore need no invented
    /// reasoning state; new lists are empty and legacy Decisions continue through that migration path.
    /// </summary>
    public sealed class SaveV3ToV4DecisionReasoningMigration : ISaveMigration
    {
        public int FromSchemaVersion => 3;
        public int ToSchemaVersion => 4;

        public void Apply(SaveGameData data)
        {
            // Deliberately empty. Absence means this is a legacy/direct-influence Decision, not a
            // request to rebuild its historical semantics from current content.
        }
    }
}
