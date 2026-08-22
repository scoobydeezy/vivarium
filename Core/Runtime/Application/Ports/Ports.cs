using System.Collections.Generic;
using Vivarium.Domain.Content;
using Vivarium.Application.Persistence;

namespace Vivarium.Application.Ports
{
    /// <summary>
    /// Wall-clock access (§21, §38).
    /// <para>
    /// Consumed by Application/Infrastructure only, to compute offline elapsed duration from the
    /// persisted anchor. <b>Domain rules never read wall-clock time</b> (invariant 32) — that is what
    /// keeps a saved world's future a function of its own state rather than of when you happened to
    /// load it.
    /// </para>
    /// </summary>
    public interface IRealWorldClock
    {
        /// <summary>Current UTC instant, as ticks, so the port carries no framework opinion.</summary>
        long UtcNowTicks { get; }
    }

    /// <summary>Persistence port (§48). Infrastructure supplies the format and the storage location.</summary>
    public interface ISaveGameStore
    {
        void Save(string slot, SaveGameData data);

        bool TryLoad(string slot, out SaveGameData data);

        bool Delete(string slot);

        IReadOnlyList<string> ListSlots();
    }

    /// <summary>Log severity.</summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// Logging port (§48). Domain and Application write here; Infrastructure decides whether that means
    /// a console, a file, or Unity's log.
    /// </summary>
    public interface ILogSink
    {
        void Log(LogLevel level, string category, string message);
    }

    /// <summary>
    /// Content port (§41).
    /// <para>
    /// Whatever authored the content — ScriptableObjects, JSON, a test fixture — the simulation only
    /// ever sees an immutable definition catalog of Unity-free types.
    /// </para>
    /// </summary>
    public interface IContentSource
    {
        /// <summary>Version of the loaded content, recorded in saves and traces (§38, §53).</summary>
        int ContentVersion { get; }

        DefinitionCatalog Load();
    }

    /// <summary>Platform storage port (§48). Domain code never touches file paths.</summary>
    public interface IPlatformStorage
    {
        bool Exists(string relativePath);

        byte[] Read(string relativePath);

        void Write(string relativePath, byte[] contents);

        bool Delete(string relativePath);

        IReadOnlyList<string> List(string relativeDirectory);
    }
}
