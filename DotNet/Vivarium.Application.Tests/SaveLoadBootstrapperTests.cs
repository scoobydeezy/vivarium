using Vivarium.Application.Persistence;
using Vivarium.Application.Session;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    /// <summary>
    /// Tests for SaveLoadBootstrapper types (Phase 6 §8.2).
    /// Covers deserialize → migrate → restore pipeline with full save cycle.
    /// </summary>
    public sealed class SaveLoadBootstrapperTests
    {
        [Fact]
        public void SaveLoadErrorKind_CoversAllCategories()
        {
            Assert.Equal(0, (int)SaveLoadErrorKind.None);
            Assert.Equal(1, (int)SaveLoadErrorKind.FileNotFound);
            Assert.Equal(2, (int)SaveLoadErrorKind.CorruptedSave);
            Assert.Equal(3, (int)SaveLoadErrorKind.SchemaIncompatible);
            Assert.Equal(4, (int)SaveLoadErrorKind.MigrationFailed);
            Assert.Equal(5, (int)SaveLoadErrorKind.RestorationFailed);
        }

        [Fact]
        public void TryLoadAndRestore_WithValidSerializedSave_RestoresWorld()
        {
            // Arrange: Create a world, save it, and serialize via JsonGzipSaveGameSerializer
            TestWorld testWorld = TestWorld.Create();
            testWorld.Host.Session.Advance(SimDuration.FromMinutes(10));

            SaveGameData saved = testWorld.Host.Session.Save("slot1");
            var serializer = new JsonGzipSaveGameSerializer();
            byte[] serialized = serializer.Serialize(saved);

            // Act: Create bootstrapper and deserialize → migrate → restore
            var bootstrapper = new SaveLoadBootstrapper(
                serializer,
                testWorld.Host.SaveMapper,
                testWorld.Host.Catalog.ContentVersion,
                1);

            SaveLoadResult loadResult = bootstrapper.TryLoadAndRestore(serialized);

            // Assert: Result is success and world state matches original
            Assert.True(loadResult.IsSuccess, $"Load failed: {loadResult.ErrorMessage}");
            Assert.NotNull(loadResult.World);
            Assert.Equal(testWorld.Host.World.Clock.Now, loadResult.World.Clock.Now);
            Assert.Equal(testWorld.Host.World.Characters.Count, loadResult.World.Characters.Count);
            Assert.Equal(testWorld.Host.World.Locations.Count, loadResult.World.Locations.Count);
        }

        [Fact]
        public void TryLoadAndRestore_WithCorruptedData_ReturnsCorruptedSaveError()
        {
            // Arrange: Create obviously corrupted bytes (not valid gzip)
            byte[] corrupted = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

            TestWorld testWorld = TestWorld.Create();
            var bootstrapper = new SaveLoadBootstrapper(
                new JsonGzipSaveGameSerializer(),
                testWorld.Host.SaveMapper,
                testWorld.Host.Catalog.ContentVersion,
                1);

            // Act
            SaveLoadResult result = bootstrapper.TryLoadAndRestore(corrupted);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(SaveLoadErrorKind.CorruptedSave, result.ErrorKind);
        }

        [Fact]
        public void TryRestore_WithVersionDrift_LoadsAndReportsScopedReproduction()
        {
            TestWorld testWorld = TestWorld.Create();
            SaveGameData saved = testWorld.Host.Session.Save("version_drift");
            saved.ContentVersion = testWorld.Host.Catalog.ContentVersion + 1;
            saved.SimulationRulesVersion = 99;
            saved.RandomAlgorithmVersion = RandomAlgorithmVersion.Current + 1;

            var bootstrapper = new SaveLoadBootstrapper(
                new JsonGzipSaveGameSerializer(),
                testWorld.Host.SaveMapper,
                testWorld.Host.Catalog.ContentVersion,
                1);

            SaveLoadResult result = bootstrapper.TryRestore(saved);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.CompatibilityReport.ReproductionIsVersionScoped);
            Assert.True(result.CompatibilityReport.ContentVersionDiffers);
            Assert.True(result.CompatibilityReport.RulesVersionDiffers);
            Assert.True(result.CompatibilityReport.RandomAlgorithmVersionDiffers);
        }

        [Fact]
        public void TryRestore_WithFutureSchema_ReturnsSchemaIncompatible()
        {
            TestWorld testWorld = TestWorld.Create();
            SaveGameData saved = testWorld.Host.Session.Save("future_schema");
            saved.SchemaVersion = SaveGameData.CurrentSchemaVersion + 1;
            var bootstrapper = new SaveLoadBootstrapper(
                new JsonGzipSaveGameSerializer(),
                testWorld.Host.SaveMapper,
                testWorld.Host.Catalog.ContentVersion,
                1);

            SaveLoadResult result = bootstrapper.TryRestore(saved);

            Assert.False(result.IsSuccess);
            Assert.Equal(SaveLoadErrorKind.SchemaIncompatible, result.ErrorKind);
            Assert.Contains("newer", result.ErrorMessage);
        }
    }
}
