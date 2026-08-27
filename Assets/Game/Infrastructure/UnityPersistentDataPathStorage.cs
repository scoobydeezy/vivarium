using System;
using System.Collections.Generic;
using Vivarium.Application.Ports;
using Vivarium.Infrastructure.Storage;

namespace Vivarium.Unity.Infrastructure
{
    /// <summary>Unity platform-storage adapter rooted at the application's persistent data directory.</summary>
    public sealed class UnityPersistentDataPathStorage : IPlatformStorage
    {
        private readonly FileSystemPlatformStorage _files;

        public UnityPersistentDataPathStorage() : this(UnityEngine.Application.persistentDataPath) { }

        public UnityPersistentDataPathStorage(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("A persistent-data root is required.", nameof(rootPath));
            _files = new FileSystemPlatformStorage(rootPath);
        }

        public bool Exists(string relativePath) => _files.Exists(relativePath);
        public byte[] Read(string relativePath) => _files.Read(relativePath);
        public void Write(string relativePath, byte[] contents) => _files.Write(relativePath, contents);
        public bool Delete(string relativePath) => _files.Delete(relativePath);
        public IReadOnlyList<string> List(string relativeDirectory) => _files.List(relativeDirectory);
    }
}
