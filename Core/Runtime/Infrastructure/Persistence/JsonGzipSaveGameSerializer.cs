using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// Serializes SaveGameData to gzipped JSON and back (Phase 6 §3).
    /// <para>
    /// Format choice: JSON for human debuggability during development; gzip for 85-90% compression
    /// (typical saves: 50-200KB → 5-30KB). Not binary, not protobuf — transparency when debugging
    /// save failures outweighs marginal additional size savings.
    /// </para>
    /// </summary>
    public sealed class JsonGzipSaveGameSerializer : ISaveGameSerializer
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(SaveGameData));

        public byte[] Serialize(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                // Serialize to JSON
                byte[] json;
                using (var jsonStream = new MemoryStream())
                {
                    Serializer.WriteObject(jsonStream, data);
                    json = jsonStream.ToArray();
                }

                // Compress with gzip
                using (var ms = new MemoryStream())
                {
                    using (var gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
                    {
                        gzip.Write(json, 0, json.Length);
                    }

                    return ms.ToArray();
                }
            }
            catch (SerializationException ex)
            {
                throw new SaveSerializationException($"Failed to serialize SaveGameData to JSON: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new SaveSerializationException($"Failed to compress serialized data: {ex.Message}", ex);
            }
        }

        public SaveGameData Deserialize(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0)
            {
                throw new SaveDeserializationException("Save file is empty (0 bytes).");
            }

            try
            {
                // Decompress gzip
                byte[] json;
                using (var ms = new MemoryStream(bytes))
                {
                    using (var gzip = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
                    {
                        using (var decompressed = new MemoryStream())
                        {
                            gzip.CopyTo(decompressed);
                            json = decompressed.ToArray();
                        }
                    }
                }

                if (json.Length == 0)
                {
                    throw new SaveDeserializationException("Save file is corrupted: decompressed data is empty.");
                }

                // Deserialize from JSON
                using (var jsonStream = new MemoryStream(json))
                {
                    return Serializer.ReadObject(jsonStream) as SaveGameData
                        ?? throw new SaveDeserializationException("Deserialized SaveGameData is null.");
                }
            }
            catch (InvalidOperationException ex) when (ex.InnerException is System.IO.InvalidDataException)
            {
                throw new SaveDeserializationException("Save file is corrupted: gzip decompression failed. The file may be truncated or invalid.", ex);
            }
            catch (System.IO.InvalidDataException ex)
            {
                throw new SaveDeserializationException("Save file is corrupted: gzip header is invalid or data is truncated.", ex);
            }
            catch (SerializationException ex)
            {
                throw new SaveDeserializationException($"Save file is corrupted: JSON deserialization failed. {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new SaveDeserializationException($"Save file is corrupted: I/O error during decompression. {ex.Message}", ex);
            }
        }
    }

    /// <summary>Thrown when SaveGameData serialization fails.</summary>
    public sealed class SaveSerializationException : Exception
    {
        public SaveSerializationException(string message) : base(message) { }

        public SaveSerializationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Thrown when SaveGameData deserialization fails (corrupted/incompatible save file).</summary>
    public sealed class SaveDeserializationException : Exception
    {
        public SaveDeserializationException(string message) : base(message) { }

        public SaveDeserializationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
