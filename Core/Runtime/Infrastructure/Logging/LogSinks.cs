using System;
using System.Collections.Generic;
using Vivarium.Application.Ports;
using Vivarium.Domain.Simulation;

namespace Vivarium.Infrastructure.Logging
{
    /// <summary>Console log sink for headless runs and tests (§48).</summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        private readonly LogLevel _minimumLevel;

        public ConsoleLogSink(LogLevel minimumLevel = LogLevel.Info)
        {
            _minimumLevel = minimumLevel;
        }

        public void Log(LogLevel level, string category, string message)
        {
            if ((int)level < (int)_minimumLevel)
            {
                return;
            }

            Console.WriteLine($"[{level}] {category}: {message}");
        }
    }

    /// <summary>Discards everything. The release default.</summary>
    public sealed class NullLogSink : ILogSink
    {
        public static readonly NullLogSink Instance = new NullLogSink();

        private NullLogSink()
        {
        }

        public void Log(LogLevel level, string category, string message)
        {
        }
    }

    /// <summary>
    /// In-memory authoritative simulation trace (§53).
    /// <para>
    /// Retains command sequences, event ordering, stale discards, decision resolutions, and the version
    /// metadata that scopes reproduction. A trace without content/rules/random versions cannot tell an
    /// input divergence from an intentional rule change, so <see cref="RecordHeader"/> stamps them once
    /// up front.
    /// </para>
    /// <para>
    /// Tracing is opt-in precisely so release performance is unaffected (§53).
    /// </para>
    /// </summary>
    public sealed class InMemorySimulationTrace : ISimulationTrace
    {
        private readonly List<string> _entries = new List<string>();
        private readonly int _capacity;

        public InMemorySimulationTrace(bool enabled = true, int capacity = 100000)
        {
            IsEnabled = enabled;
            _capacity = capacity;
        }

        public bool IsEnabled { get; }

        public IReadOnlyList<string> Entries => _entries;

        /// <summary>Stamps the version metadata a reproduction depends on (§15, §53).</summary>
        public void RecordHeader(long worldSeed, int contentVersion, int simulationRulesVersion, int randomAlgorithmVersion, string buildVersion = null)
        {
            Record(
                "header",
                $"worldSeed={worldSeed} contentVersion={contentVersion} rulesVersion={simulationRulesVersion} randomAlgorithmVersion={randomAlgorithmVersion}{(buildVersion == null ? string.Empty : " build=" + buildVersion)}");
        }

        public void Record(string category, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (_entries.Count >= _capacity)
            {
                // Drop rather than grow without bound; a truncated trace is better than an OOM mid-run.
                return;
            }

            _entries.Add(category + " | " + message);
        }

        public void WriteTo(ILogSink sink, string category = "trace")
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                sink.Log(LogLevel.Debug, category, _entries[i]);
            }
        }
    }
}
