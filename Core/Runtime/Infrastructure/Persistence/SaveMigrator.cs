using System;
using System.Collections.Generic;
using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>One step in the migration chain: understands version N and produces version N+1.</summary>
    public interface ISaveMigration
    {
        /// <summary>The schema version this migration reads.</summary>
        int FromSchemaVersion { get; }

        /// <summary>The schema version it produces. Must be <c>FromSchemaVersion + 1</c>.</summary>
        int ToSchemaVersion { get; }

        void Apply(SaveGameData data);
    }

    /// <summary>
    /// Walks a save forward to the current schema (§39).
    /// <para>
    /// <c>Save V1 → V2 → V3</c>, one explicit step at a time. Only
    /// <see cref="SaveGameData.SchemaVersion"/> governs whether a save can be understood; content,
    /// rules, and random-algorithm mismatches are compatibility metadata and diagnostics, <b>not</b>
    /// automatic load blockers (§39.1, invariant 62).
    /// </para>
    /// <para>
    /// The consequence is deliberate: an ordinary balance patch does not invalidate every existing save.
    /// What it does invalidate is the promise of an <i>identical</i> replay, which is why the versions
    /// are recorded rather than ignored.
    /// </para>
    /// </summary>
    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, ISaveMigration> _migrations = new Dictionary<int, ISaveMigration>();

        public SaveMigrator()
        {
            Register(new SaveV1ToV2SocialMigration());
            Register(new SaveV2ToV3DecisionPolarityMigration());
            Register(new SaveV3ToV4DecisionReasoningMigration());
            Register(new SaveV4ToV5CommitmentConflictMigration());
            Register(new SaveV5ToV6CommitmentAccountabilityMigration());
            Register(new SaveV6ToV7TravelContinuationMigration());
        }

        public void Register(ISaveMigration migration)
        {
            if (migration == null)
            {
                throw new ArgumentNullException(nameof(migration));
            }

            if (migration.ToSchemaVersion != migration.FromSchemaVersion + 1)
            {
                throw new ArgumentException(
                    $"Migrations must advance exactly one version; {migration.GetType().Name} goes {migration.FromSchemaVersion} → {migration.ToSchemaVersion}.",
                    nameof(migration));
            }

            if (_migrations.ContainsKey(migration.FromSchemaVersion))
            {
                throw new InvalidOperationException($"A migration from schema version {migration.FromSchemaVersion} is already registered.");
            }

            _migrations.Add(migration.FromSchemaVersion, migration);
        }

        /// <summary>
        /// Migrates in place to <see cref="SaveGameData.CurrentSchemaVersion"/>.
        /// </summary>
        /// <returns>A report describing what happened, including version mismatches worth surfacing.</returns>
        public SaveCompatibilityReport Migrate(SaveGameData data, int currentContentVersion, int currentRulesVersion, int currentRandomAlgorithmVersion)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var steps = new List<string>();

            if (data.SchemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                return SaveCompatibilityReport.Unloadable(
                    data,
                    $"Save schema version {data.SchemaVersion} is newer than this build understands ({SaveGameData.CurrentSchemaVersion}).");
            }

            while (data.SchemaVersion < SaveGameData.CurrentSchemaVersion)
            {
                if (!_migrations.TryGetValue(data.SchemaVersion, out ISaveMigration migration))
                {
                    return SaveCompatibilityReport.Unloadable(
                        data,
                        $"No migration registered from schema version {data.SchemaVersion}; the save cannot be brought forward.");
                }

                migration.Apply(data);
                data.SchemaVersion = migration.ToSchemaVersion;
                steps.Add($"{migration.FromSchemaVersion} → {migration.ToSchemaVersion} ({migration.GetType().Name})");
            }

            return SaveCompatibilityReport.Loadable(
                data,
                steps,
                data.ContentVersion != currentContentVersion,
                data.SimulationRulesVersion != currentRulesVersion,
                data.RandomAlgorithmVersion != currentRandomAlgorithmVersion);
        }
    }

    /// <summary>
    /// What a load found: whether it can proceed, what was migrated, and which versions differ (§39.1).
    /// </summary>
    public sealed class SaveCompatibilityReport
    {
        private static readonly string[] NoSteps = new string[0];

        private SaveCompatibilityReport(
            bool canLoad,
            string blockingReason,
            IReadOnlyList<string> migrationSteps,
            bool contentVersionDiffers,
            bool rulesVersionDiffers,
            bool randomAlgorithmVersionDiffers)
        {
            CanLoad = canLoad;
            BlockingReason = blockingReason;
            MigrationSteps = migrationSteps;
            ContentVersionDiffers = contentVersionDiffers;
            RulesVersionDiffers = rulesVersionDiffers;
            RandomAlgorithmVersionDiffers = randomAlgorithmVersionDiffers;
        }

        public bool CanLoad { get; }

        /// <summary>Set only when <see cref="CanLoad"/> is false.</summary>
        public string BlockingReason { get; }

        public IReadOnlyList<string> MigrationSteps { get; }

        public bool ContentVersionDiffers { get; }

        public bool RulesVersionDiffers { get; }

        public bool RandomAlgorithmVersionDiffers { get; }

        /// <summary>
        /// Whether the save can be resumed but not replayed identically. Loading is still fine — exact
        /// historical replay is what needs the matching build (§39.1).
        /// </summary>
        public bool ReproductionIsVersionScoped =>
            ContentVersionDiffers || RulesVersionDiffers || RandomAlgorithmVersionDiffers;

        internal static SaveCompatibilityReport Loadable(
            SaveGameData data,
            IReadOnlyList<string> steps,
            bool contentDiffers,
            bool rulesDiffer,
            bool randomDiffers) =>
            new SaveCompatibilityReport(true, null, steps ?? NoSteps, contentDiffers, rulesDiffer, randomDiffers);

        internal static SaveCompatibilityReport Unloadable(SaveGameData data, string reason) =>
            new SaveCompatibilityReport(false, reason, NoSteps, false, false, false);

        public override string ToString()
        {
            if (!CanLoad)
            {
                return "unloadable: " + BlockingReason;
            }

            string migrations = MigrationSteps.Count == 0 ? "none" : string.Join(", ", MigrationSteps);
            return $"loadable (migrations: {migrations}; version drift: content={ContentVersionDiffers}, rules={RulesVersionDiffers}, rng={RandomAlgorithmVersionDiffers})";
        }
    }
}
