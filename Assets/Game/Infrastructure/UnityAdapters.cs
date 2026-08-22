using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vivarium.Application.Ports;

namespace Vivarium.Unity.Infrastructure
{
    /// <summary>
    /// Platform storage over Unity's persistent data path (§48).
    /// <para>
    /// The only place in the Unity side that knows where saves live. Domain and Application see
    /// <see cref="IPlatformStorage"/> and nothing more.
    /// </para>
    /// </summary>
    public sealed class UnityPersistentDataStorage : IPlatformStorage
    {
        private readonly string _root;

        public UnityPersistentDataStorage(string subdirectory = "vivarium")
        {
            _root = Path.Combine(Application.persistentDataPath, subdirectory);
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

            File.WriteAllBytes(path, contents);
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
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(path);
            var names = new List<string>(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                names.Add(Path.GetFileName(files[i]));
            }

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
                throw new ArgumentException($"Storage paths must stay inside the root: '{relativePath}'.", nameof(relativePath));
            }

            return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>Routes Application and Domain logging into Unity's console (§48).</summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Log(LogLevel level, string category, string message)
        {
            string line = $"[{category}] {message}";

            switch (level)
            {
                case LogLevel.Error:
                    Debug.LogError(line);
                    break;

                case LogLevel.Warning:
                    Debug.LogWarning(line);
                    break;

                default:
                    Debug.Log(line);
                    break;
            }
        }
    }

    /// <summary>
    /// Wall clock for offline progression (§21).
    /// <para>
    /// Uses <see cref="DateTime.UtcNow"/> rather than <c>Time.time</c>: this answers "how long was the
    /// player away?", which has nothing to do with frame time (§9).
    /// </para>
    /// </summary>
    public sealed class UnityRealWorldClock : IRealWorldClock
    {
        public long UtcNowTicks => DateTime.UtcNow.Ticks;
    }
}
