using System;
using System.Collections.Generic;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// Turns save data into bytes and back (§48).
    /// <para>
    /// Phase 6 selects gzipped JSON for the shipping-facing implementation. The port remains format-
    /// independent: saves are versioned DTOs (§38) and migration is explicit (§39), regardless of the
    /// encoder composed at the application boundary.
    /// </para>
    /// </summary>
    public interface ISaveGameSerializer
    {
        byte[] Serialize(SaveGameData data);

        SaveGameData Deserialize(byte[] bytes);
    }

    /// <summary>
    /// In-memory save store for tests and headless runs (§52).
    /// <para>
    /// Holds the DTO graph directly rather than encoding it, which is enough to exercise the part that
    /// can actually be wrong: <see cref="SaveGameMapper.Restore"/> builds a brand-new
    /// <c>WorldState</c> from the DTOs, so a round-trip through this store still proves the world was
    /// genuinely reconstructed rather than handed back by reference. It does <b>not</b> exercise
    /// serialization — that needs a real <see cref="ISaveGameSerializer"/>.
    /// </para>
    /// </summary>
    public sealed class InMemorySaveGameStore : ISaveGameStore
    {
        private readonly SortedDictionary<string, SaveGameData> _slots = new SortedDictionary<string, SaveGameData>(StringComparer.Ordinal);

        public void Save(string slot, SaveGameData data)
        {
            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException("A save slot needs a name.", nameof(slot));
            }

            _slots[slot] = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool TryLoad(string slot, out SaveGameData data) => _slots.TryGetValue(slot, out data);

        public bool Delete(string slot) => _slots.Remove(slot);

        public IReadOnlyList<string> ListSlots() => new List<string>(_slots.Keys);
    }

    /// <summary>
    /// Save store over a platform storage port (§48).
    /// <para>
    /// Composes a serializer with storage so neither knows about the other, and so Domain code stays
    /// entirely unaware that file paths exist. Unity supplies persistent-data storage; headless runs
    /// supply a plain directory.
    /// </para>
    /// </summary>
    public sealed class PlatformSaveGameStore : ISaveGameStore
    {
        private const string SaveDirectory = "saves";
        private const string SaveExtension = ".sav";

        private readonly IPlatformStorage _storage;
        private readonly ISaveGameSerializer _serializer;

        public PlatformSaveGameStore(IPlatformStorage storage, ISaveGameSerializer serializer)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public void Save(string slot, SaveGameData data) => _storage.Write(PathFor(slot), _serializer.Serialize(data));

        public bool TryLoad(string slot, out SaveGameData data)
        {
            string path = PathFor(slot);

            if (!_storage.Exists(path))
            {
                data = null;
                return false;
            }

            data = _serializer.Deserialize(_storage.Read(path));
            return true;
        }

        public bool Delete(string slot) => _storage.Delete(PathFor(slot));

        public IReadOnlyList<string> ListSlots()
        {
            IReadOnlyList<string> files = _storage.List(SaveDirectory);
            var slots = new List<string>(files.Count);

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                if (file.EndsWith(SaveExtension, StringComparison.Ordinal))
                {
                    slots.Add(file.Substring(0, file.Length - SaveExtension.Length));
                }
            }

            return slots;
        }

        private static string PathFor(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A save slot needs a name.", nameof(slot));
            for (int i = 0; i < slot.Length; i++)
            {
                char c = slot[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    throw new ArgumentException(
                        "Save slot names may contain only letters, numbers, underscores, and hyphens.",
                        nameof(slot));
            }
            return SaveDirectory + "/" + slot + SaveExtension;
        }
    }
}
