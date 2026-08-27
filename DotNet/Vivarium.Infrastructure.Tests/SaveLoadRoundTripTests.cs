using System;
using System.Collections.Generic;
using System.IO;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Infrastructure.Persistence;
using Vivarium.Infrastructure.Storage;
using Xunit;

namespace Vivarium.Infrastructure.Tests
{
    public sealed class SaveLoadRoundTripTests
    {
        [Fact]
        public void JsonGzipSerializer_RoundTripsMinimalSaveData()
        {
            var original = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                ContentVersion = 1,
                SimulationRulesVersion = 1,
                RandomAlgorithmVersion = 1,
                WorldSeed = 12345,
                ClockMinutes = 100,
                SavedAtRealTimeUtcTicks = DateTime.UtcNow.Ticks,
                LastCommandSequence = 42,
            };

            var serializer = new JsonGzipSaveGameSerializer();
            byte[] bytes = serializer.Serialize(original);
            SaveGameData restored = serializer.Deserialize(bytes);

            Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
            Assert.Equal(original.ContentVersion, restored.ContentVersion);
            Assert.Equal(original.ClockMinutes, restored.ClockMinutes);
            Assert.Equal(original.WorldSeed, restored.WorldSeed);
            Assert.Equal(original.LastCommandSequence, restored.LastCommandSequence);
        }

        [Fact]
        public void JsonGzipSerializer_CompressesTypicalSave()
        {
            var data = CreateLargeTypicalSave();
            var serializer = new JsonGzipSaveGameSerializer();

            byte[] bytes = serializer.Serialize(data);

            // Gzip should compress by 85-90%; even 30KB limit is comfortable for typical saves
            Assert.True(bytes.Length < 30 * 1024, $"Save size {bytes.Length} exceeds 30KB limit");
            Assert.True(bytes.Length > 100, $"Save size {bytes.Length} is suspiciously small; check compression");
        }

        [Fact]
        public void JsonGzipSerializer_ThrowsOnNullData()
        {
            var serializer = new JsonGzipSaveGameSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null));
        }

        [Fact]
        public void JsonGzipDeserializer_ThrowsOnEmptyBytes()
        {
            var serializer = new JsonGzipSaveGameSerializer();
            var ex = Assert.Throws<SaveDeserializationException>(() => serializer.Deserialize(new byte[0]));
            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void JsonGzipDeserializer_ThrowsOnTruncatedGzip()
        {
            var serializer = new JsonGzipSaveGameSerializer();
            var original = new SaveGameData { ClockMinutes = 100 };
            byte[] bytes = serializer.Serialize(original);

            // Truncate to half size
            byte[] truncated = new byte[bytes.Length / 2];
            Array.Copy(bytes, truncated, truncated.Length);

            var ex = Assert.Throws<SaveDeserializationException>(() => serializer.Deserialize(truncated));
            Assert.Contains("corrupted", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void JsonGzipDeserializer_ThrowsOnInvalidJson()
        {
            var serializer = new JsonGzipSaveGameSerializer();

            // Create a gzipped string that's valid gzip but invalid JSON when decompressed
            var invalidJson = System.Text.Encoding.UTF8.GetBytes("{invalid json}");
            var ms = new System.IO.MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(invalidJson, 0, invalidJson.Length);
            }
            byte[] bytes = ms.ToArray();

            var ex = Assert.Throws<SaveDeserializationException>(() => serializer.Deserialize(bytes));
            Assert.Contains("corrupted", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void JsonGzipSerializer_RoundTripsWithLists()
        {
            var original = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                ClockMinutes = 50,
                Characters = new List<CharacterData>
                {
                    new CharacterData { Id = 1, DisplayName = "Alice", CreatedAtMinutes = 0 },
                    new CharacterData { Id = 2, DisplayName = "Bob", CreatedAtMinutes = 10 },
                },
                Locations = new List<LocationData>
                {
                    new LocationData { Id = 10, LocationKindId = "home", DisplayName = "Home" },
                    new LocationData { Id = 11, LocationKindId = "work", DisplayName = "Workplace" },
                },
            };

            var serializer = new JsonGzipSaveGameSerializer();
            byte[] bytes = serializer.Serialize(original);
            SaveGameData restored = serializer.Deserialize(bytes);

            Assert.Equal(original.ClockMinutes, restored.ClockMinutes);
            Assert.Equal(2, restored.Characters.Count);
            Assert.Equal("Alice", restored.Characters[0].DisplayName);
            Assert.Equal("Bob", restored.Characters[1].DisplayName);
            Assert.Equal(2, restored.Locations.Count);
            Assert.Equal("home", restored.Locations[0].LocationKindId);
        }

        [Fact]
        public void SaveGameStores_PlatformStore_UsesSerializer()
        {
            var storage = new InMemoryTestPlatformStorage();
            var serializer = new JsonGzipSaveGameSerializer();
            var store = new PlatformSaveGameStore(storage, serializer);

            var original = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                ClockMinutes = 75,
            };

            store.Save("test_slot", original);
            bool loaded = store.TryLoad("test_slot", out SaveGameData restored);

            Assert.True(loaded);
            Assert.Equal(original.ClockMinutes, restored.ClockMinutes);
        }

        [Fact]
        public void SaveGameStores_PlatformStore_ListsSlots()
        {
            var storage = new InMemoryTestPlatformStorage();
            var serializer = new JsonGzipSaveGameSerializer();
            var store = new PlatformSaveGameStore(storage, serializer);

            store.Save("slot_1", new SaveGameData { ClockMinutes = 10 });
            store.Save("slot_2", new SaveGameData { ClockMinutes = 20 });
            store.Save("quicksave", new SaveGameData { ClockMinutes = 30 });

            IReadOnlyList<string> slots = store.ListSlots();

            Assert.Equal(3, slots.Count);
            Assert.Contains("slot_1", slots);
            Assert.Contains("slot_2", slots);
            Assert.Contains("quicksave", slots);
        }

        [Fact]
        public void SaveGameStores_PlatformStore_DeletesSlot()
        {
            var storage = new InMemoryTestPlatformStorage();
            var serializer = new JsonGzipSaveGameSerializer();
            var store = new PlatformSaveGameStore(storage, serializer);

            store.Save("slot_1", new SaveGameData { ClockMinutes = 10 });
            Assert.True(store.TryLoad("slot_1", out _));

            bool deleted = store.Delete("slot_1");
            Assert.True(deleted);
            Assert.False(store.TryLoad("slot_1", out _));
        }

        [Fact]
        public void PlatformStore_NewInstanceLoadsSaveWrittenToDisk()
        {
            string root = Path.Combine(Path.GetTempPath(), "vivarium-save-tests", Guid.NewGuid().ToString("N"));
            try
            {
                var serializer = new JsonGzipSaveGameSerializer();
                var writer = new PlatformSaveGameStore(new FileSystemPlatformStorage(root), serializer);
                writer.Save("restart_slot", new SaveGameData
                {
                    SchemaVersion = SaveGameData.CurrentSchemaVersion,
                    ClockMinutes = 9876,
                    WorldSeed = 42,
                });

                var reader = new PlatformSaveGameStore(
                    new FileSystemPlatformStorage(root),
                    new JsonGzipSaveGameSerializer());

                Assert.True(reader.TryLoad("restart_slot", out SaveGameData restored));
                Assert.Equal(9876, restored.ClockMinutes);
                Assert.Equal(42, restored.WorldSeed);
                Assert.Contains("restart_slot", reader.ListSlots());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void PlatformStore_OverwriteIsVisibleToNewInstance()
        {
            string root = Path.Combine(Path.GetTempPath(), "vivarium-save-tests", Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PlatformSaveGameStore(
                    new FileSystemPlatformStorage(root),
                    new JsonGzipSaveGameSerializer());
                store.Save("slot", new SaveGameData { ClockMinutes = 10 });
                store.Save("slot", new SaveGameData { ClockMinutes = 20 });

                var restarted = new PlatformSaveGameStore(
                    new FileSystemPlatformStorage(root),
                    new JsonGzipSaveGameSerializer());
                Assert.True(restarted.TryLoad("slot", out SaveGameData restored));
                Assert.Equal(20, restored.ClockMinutes);
                Assert.False(File.Exists(Path.Combine(root, "saves", "slot.sav.tmp")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Theory]
        [InlineData("../escape")]
        [InlineData("folder/slot")]
        [InlineData("slot.name")]
        public void PlatformStore_RejectsUnsafeSlotNames(string slot)
        {
            var store = new PlatformSaveGameStore(
                new InMemoryTestPlatformStorage(),
                new JsonGzipSaveGameSerializer());

            Assert.Throws<ArgumentException>(() => store.Save(slot, new SaveGameData()));
        }

        private static SaveGameData CreateLargeTypicalSave()
        {
            var data = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                ContentVersion = 1,
                ClockMinutes = 2400, // 40 simulated days
                SavedAtRealTimeUtcTicks = DateTime.UtcNow.Ticks,
                LastCommandSequence = 5000,
                NudgeBalance = 2,
                NudgeRevision = 15,
            };

            // Add realistic character data (10 characters)
            for (int i = 1; i <= 10; i++)
            {
                data.Characters.Add(new CharacterData
                {
                    Id = i,
                    DisplayName = $"Character_{i}",
                    CreatedAtMinutes = i * 10,
                    IsActive = true,
                    Traits = new List<string> { "trait_a", "trait_b" },
                    Needs = new List<NeedData>
                    {
                        new NeedData { NeedId = "hunger", Progression = new ProgressionData() },
                        new NeedData { NeedId = "energy", Progression = new ProgressionData() },
                    },
                });
            }

            // Add locations
            data.Locations.Add(new LocationData { Id = 100, LocationKindId = "home", DisplayName = "Home" });
            data.Locations.Add(new LocationData { Id = 101, LocationKindId = "work", DisplayName = "Workplace" });
            data.Locations.Add(new LocationData { Id = 102, LocationKindId = "commons", DisplayName = "Commons" });


            return data;
        }

        /// <summary>Simple in-memory storage for testing (doesn't use real files).</summary>
        private sealed class InMemoryTestPlatformStorage : IPlatformStorage
        {
            private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            public bool Exists(string relativePath) => _files.ContainsKey(relativePath);

            public byte[] Read(string relativePath)
            {
                if (!_files.TryGetValue(relativePath, out byte[] data))
                {
                    throw new System.IO.FileNotFoundException($"File not found: {relativePath}");
                }
                return data;
            }

            public void Write(string relativePath, byte[] contents)
            {
                _files[relativePath] = contents ?? throw new ArgumentNullException(nameof(contents));
            }

            public bool Delete(string relativePath) => _files.Remove(relativePath);

            public IReadOnlyList<string> List(string relativeDirectory)
            {
                var prefix = relativeDirectory.TrimEnd('/') + "/";
                var files = new List<string>();
                foreach (var key in _files.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        files.Add(key.Substring(prefix.Length));
                    }
                }
                files.Sort();
                return files;
            }
        }
    }
}
