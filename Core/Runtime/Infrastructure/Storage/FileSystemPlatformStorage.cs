using System;
using System.Collections.Generic;
using System.IO;
using Vivarium.Application.Ports;

namespace Vivarium.Infrastructure.Storage
{
    /// <summary>
    /// Platform storage over an ordinary directory, for headless runs and tests (§48, §52).
    /// <para>
    /// All path handling lives here. Nothing in Domain or Application knows a file system exists — they
    /// see <see cref="IPlatformStorage"/>. Unity supplies its own implementation over persistent data
    /// paths without either layer changing.
    /// </para>
    /// </summary>
    public sealed class FileSystemPlatformStorage : IPlatformStorage
    {
        private readonly string _rootDirectory;

        public FileSystemPlatformStorage(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentException("A storage root is required.", nameof(rootDirectory));
            }

            _rootDirectory = rootDirectory;
        }

        public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

        public byte[] Read(string relativePath) => File.ReadAllBytes(Resolve(relativePath));

        public void Write(string relativePath, byte[] contents)
        {
            string path = Resolve(relativePath);
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (contents == null) throw new ArgumentNullException(nameof(contents));
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, contents);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // Some Unity targets do not implement File.Replace. Preserve functional
                        // persistence there even though that fallback cannot promise atomic replacement.
                        File.Delete(path);
                        File.Move(temporary, path);
                    }
                }
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public bool Delete(string relativePath)
        {
            string path = Resolve(relativePath);

            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        public IReadOnlyList<string> List(string relativeDirectory)
        {
            string path = Resolve(relativeDirectory);

            if (!Directory.Exists(path))
            {
                return new string[0];
            }

            string[] files = Directory.GetFiles(path);
            var names = new List<string>(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                names.Add(Path.GetFileName(files[i]));
            }

            // Deterministic listing order, so headless tooling behaves identically across runs (§15).
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private string Resolve(string relativePath)
        {
            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath) || relativePath.Contains(".."))
            {
                throw new ArgumentException($"Storage paths must be relative and must not escape the root: '{relativePath}'.", nameof(relativePath));
            }

            return Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
