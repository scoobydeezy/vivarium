using System;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Domain.Common;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Session
{
    /// <summary>
    /// Owns one running world: its state, its command ingress, and its advancement (§2.2.1, §33).
    /// <para>
    /// This is the authoritative concurrency boundary. Exactly one owner mutates
    /// <see cref="WorldState"/>, commands execute one at a time, and each one settles to quiescence
    /// before the next begins:
    /// </para>
    /// <code>
    /// Command #501 → mutate → settle events and reactions → QUIESCENT → Command #502
    /// </code>
    /// <para>
    /// Presentation reads projections published at those quiescent points, never the world mid-cascade
    /// (§13.1).
    /// </para>
    /// </summary>
    public sealed class GameSession
    {
        private readonly CommandDispatcher _dispatcher;
        private readonly SimulationRunner _runner;
        private readonly SaveGameMapper _saveMapper;
        private readonly ISaveGameStore _saveStore;
        private readonly IRealWorldClock _realWorldClock;

        public GameSession(
            WorldState world,
            IRandomOracle random,
            CommandDispatcher dispatcher,
            SimulationRunner runner,
            int contentVersion,
            int simulationRulesVersion,
            SaveGameMapper saveMapper = null,
            ISaveGameStore saveStore = null,
            IRealWorldClock realWorldClock = null,
            ISimulationTrace trace = null,
            long restoredCommandSequence = 0)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _saveMapper = saveMapper;
            _saveStore = saveStore;
            _realWorldClock = realWorldClock;

            Commands = new CommandQueue(restoredCommandSequence);
            Simulation = new SimulationContext(world, random, SimulationMode.Live, contentVersion, simulationRulesVersion, trace);
        }

        public SimulationContext Simulation { get; }

        public WorldState World => Simulation.World;

        public CommandQueue Commands { get; }

        /// <summary>
        /// Accepts an external write request into the deterministic ingress queue (§2.2).
        /// <para>
        /// Enqueuing does not execute: nothing mutates until <see cref="Pump"/> reaches a quiescent
        /// boundary, which is what makes command ordering reproducible.
        /// </para>
        /// </summary>
        public CommandEnvelope Enqueue(ICommand command, string diagnostics = null) => Commands.Enqueue(command, diagnostics);

        /// <summary>
        /// Executes queued commands in <c>CommandSequence</c> order, settling after each.
        /// </summary>
        /// <returns>How many commands executed.</returns>
        public int Pump()
        {
            int executed = 0;

            while (Commands.TryDequeue(out CommandEnvelope envelope))
            {
                var context = new CommandContext(Simulation, envelope.CommandSequence);

                if (Simulation.Trace.IsEnabled)
                {
                    Simulation.Trace.Record(
                        "command",
                        $"{World.Clock.Now} cmd #{envelope.CommandSequence} {envelope.Command.GetType().Name}{(envelope.Diagnostics == null ? string.Empty : " [" + envelope.Diagnostics + "]")}");
                }

                _dispatcher.DispatchUntyped(envelope.Command, context);

                // Consequences of this command settle before the next one is even looked at (§2.2.1).
                _runner.SettleAndPublish(Simulation);
                executed++;
            }

            return executed;
        }

        /// <summary>Enqueues a command, runs the pump, and returns its typed result.</summary>
        public TResult Execute<TResult>(ICommand<TResult> command, string diagnostics = null)
        {
            CommandEnvelope envelope = Enqueue(command, diagnostics);

            // Drain anything queued ahead of this command first, so ordering still holds.
            while (Commands.TryDequeue(out CommandEnvelope next))
            {
                var context = new CommandContext(Simulation, next.CommandSequence);
                object result = _dispatcher.DispatchUntyped(next.Command, context);
                _runner.SettleAndPublish(Simulation);

                if (ReferenceEquals(next, envelope))
                {
                    return (TResult)result;
                }
            }

            throw new InvalidOperationException("The queued command was consumed without producing a result.");
        }

        /// <summary>
        /// Advances time directly, for hosts that drive the world without going through a command.
        /// </summary>
        public void Advance(SimDuration duration, SimulationMode mode = SimulationMode.Live, int publishEveryInstants = 0)
        {
            SimulationContext context = Simulation.Mode == mode ? Simulation : Simulation.WithMode(mode);
            _runner.AdvanceBy(duration, context, publishEveryInstants);
        }

        /// <summary>
        /// Captures the world to a save slot.
        /// <para>
        /// Only legal at a quiescent boundary, which is why this settles first: a save taken halfway
        /// through a settlement cascade would persist a world that never actually existed (§2.2.1).
        /// </para>
        /// </summary>
        public SaveGameData Save(string slot)
        {
            if (_saveMapper == null || _saveStore == null)
            {
                throw new InvalidOperationException("This session was constructed without a save mapper or store.");
            }

            _runner.SettleAndPublish(Simulation);

            SaveGameData data = _saveMapper.ToSave(
                World,
                Simulation.ContentVersion,
                Simulation.SimulationRulesVersion,
                _realWorldClock?.UtcNowTicks ?? 0,
                Commands.LastIssuedSequence);

            _saveStore.Save(slot, data);
            return data;
        }

        /// <summary>Reports how much work the runner has done, for benchmarking (§49).</summary>
        public string PerformanceSummary() =>
            $"instants={_runner.InstantsSettled} work={_runner.WorkProcessed} pendingEvents={World.Scheduler.PendingCount} characters={World.Characters.Count}";
    }
}
