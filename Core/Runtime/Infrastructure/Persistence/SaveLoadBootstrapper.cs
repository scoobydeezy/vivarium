using System;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// Orchestrates the complete load flow: deserialize → migrate → restore → diagnostics (Phase 6 §4.2).
    /// <para>
    /// Takes serialized bytes and a catalog, produces a restored WorldState or a detailed error result.
    /// Never raises exceptions on load failure — always returns a result with an error message and
    /// recovery guidance suitable for UI display.
    /// </para>
    /// </summary>
    public sealed class SaveLoadBootstrapper
    {
        private readonly ISaveGameSerializer _serializer;
        private readonly SaveGameMapper _saveMapper;
        private readonly SaveMigrator _migrator;
        private readonly int _currentContentVersion;
        private readonly int _currentRulesVersion;

        public SaveLoadBootstrapper(
            ISaveGameSerializer serializer,
            SaveGameMapper saveMapper,
            int currentContentVersion,
            int currentRulesVersion)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _saveMapper = saveMapper ?? throw new ArgumentNullException(nameof(saveMapper));
            _currentContentVersion = currentContentVersion;
            _currentRulesVersion = currentRulesVersion;
            _migrator = new SaveMigrator();
        }

        /// <summary>
        /// Attempts to load, migrate, and restore a save from its serialized bytes.
        /// </summary>
        /// <returns>
        /// A result describing success or failure. On success, contains the restored WorldState and
        /// compatibility diagnostics. On failure, contains an error message and recovery guidance.
        /// </returns>
        public SaveLoadResult TryLoadAndRestore(byte[] serializedBytes)
        {
            if (serializedBytes == null)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.FileNotFound,
                    "Save data is null.");
            }

            // Step 1: Deserialize from gzipped JSON
            SaveGameData data;
            try
            {
                data = _serializer.Deserialize(serializedBytes);
            }
            catch (SaveDeserializationException ex)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.CorruptedSave,
                    $"Save file is corrupted and cannot be read: {ex.Message}");
            }
            catch (Exception ex)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.CorruptedSave,
                    $"Save file is corrupted and cannot be read: {ex.Message}");
            }

            return TryRestore(data);
        }

        /// <summary>Runs migration, compatibility diagnostics, and restoration for an already decoded DTO.</summary>
        public SaveLoadResult TryRestore(SaveGameData data)
        {
            if (data == null)
                return SaveLoadResult.Error(SaveLoadErrorKind.CorruptedSave, "Save data is null.");

            // Step 1: Run migrations and gather compatibility diagnostics
            SaveCompatibilityReport compatibilityReport;
            try
            {
                compatibilityReport = _migrator.Migrate(
                    data,
                    _currentContentVersion,
                    _currentRulesVersion,
                    RandomAlgorithmVersion.Current);
            }
            catch (Exception ex)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.MigrationFailed,
                    $"Migration failed: {ex.Message}");
            }

            if (!compatibilityReport.CanLoad)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.SchemaIncompatible,
                    $"Save cannot be loaded: {compatibilityReport.BlockingReason}");
            }

            // Step 2: Restore the world from migrated data
            WorldState restoredWorld;
            try
            {
                restoredWorld = _saveMapper.Restore(data);
            }
            catch (Exception ex)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.RestorationFailed,
                    $"Failed to restore world state: {ex.Message}");
            }

            if (restoredWorld == null)
            {
                return SaveLoadResult.Error(
                    SaveLoadErrorKind.RestorationFailed,
                    "Restored world state is null.");
            }

            // Step 3: Success — return world and diagnostics
            return SaveLoadResult.Success(restoredWorld, data, compatibilityReport);
        }
    }

    /// <summary>Result of a save load attempt, including success/error and diagnostics (Phase 6 §4.2).</summary>
    public sealed class SaveLoadResult
    {
        private SaveLoadResult(
            bool isSuccess,
            SaveLoadErrorKind errorKind,
            string errorMessage,
            WorldState world,
            SaveGameData savedData,
            SaveCompatibilityReport compatibilityReport)
        {
            IsSuccess = isSuccess;
            ErrorKind = errorKind;
            ErrorMessage = errorMessage;
            World = world;
            SavedData = savedData;
            CompatibilityReport = compatibilityReport;
        }

        /// <summary>Whether the load succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Set only when <see cref="IsSuccess"/> is false. Categorizes the error for UI handling.</summary>
        public SaveLoadErrorKind ErrorKind { get; }

        /// <summary>Set only when <see cref="IsSuccess"/> is false. Human-readable error message for display.</summary>
        public string ErrorMessage { get; }

        /// <summary>Set only on success. The restored playable world.</summary>
        public WorldState World { get; }

        /// <summary>Set only on success. The loaded save data (used for offline catch-up calculation).</summary>
        public SaveGameData SavedData { get; }

        /// <summary>Set only on success. Diagnostics about schema/content/rules version compatibility.</summary>
        public SaveCompatibilityReport CompatibilityReport { get; }

        internal static SaveLoadResult Success(
            WorldState world,
            SaveGameData data,
            SaveCompatibilityReport compatibilityReport) =>
            new SaveLoadResult(
                isSuccess: true,
                errorKind: SaveLoadErrorKind.None,
                errorMessage: null,
                world: world,
                savedData: data,
                compatibilityReport: compatibilityReport);

        internal static SaveLoadResult Error(SaveLoadErrorKind kind, string message) =>
            new SaveLoadResult(
                isSuccess: false,
                errorKind: kind,
                errorMessage: message,
                world: null,
                savedData: null,
                compatibilityReport: null);
    }

    /// <summary>Categorizes save load errors for appropriate UI response (Phase 6 §6).</summary>
    public enum SaveLoadErrorKind
    {
        None = 0,
        FileNotFound = 1,
        CorruptedSave = 2,
        SchemaIncompatible = 3,
        MigrationFailed = 4,
        RestorationFailed = 5,
    }
}
