using System;
using System.Diagnostics;
using Vivarium.Application.Commands;
using Vivarium.Domain.Content;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Logging;
using Vivarium.Infrastructure.Persistence;

namespace Vivarium.SimRunner
{
    /// <summary>
    /// Headless simulation runner (§52).
    /// <para>
    /// The same Core the game uses, with no Unity process anywhere. Beyond performance testing, balance
    /// tuning, and regression runs, its existence continually verifies that Unity has not leaked into the
    /// simulation core — if it ever does, this stops compiling.
    /// </para>
    /// </summary>
    public static class Program
    {
        private const long DefaultSeed = 827119;

        public static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0].ToLowerInvariant() : "demo";

            switch (command)
            {
                case "demo":
                    return RunDemo();

                case "determinism":
                    return RunDeterminismCheck();

                case "bench":
                    return RunBenchmark(args.Length > 1 ? int.Parse(args[1]) : 1000, args.Length > 2 ? int.Parse(args[2]) : 1);

                case "saveload":
                    return RunSaveLoadCheck();

                default:
                    Console.WriteLine("usage: SimRunner [demo|determinism|saveload|bench <population> <days>]");
                    return 1;
            }
        }

        /// <summary>
        /// Walks the §55 vertical slice: routines drive travel and work, a decision forms with partly
        /// hidden influences, the world changes it while it is still open, the player intervenes, and it
        /// resolves deterministically.
        /// </summary>
        private static int RunDemo()
        {
            var trace = new InMemorySimulationTrace();
            DefinitionCatalog catalog = SampleContent.Build();

            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                DefaultSeed,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                simulationRulesVersion: 1,
                trace: trace,
                saveStore: new InMemorySaveGameStore(),
                realWorldClock: new SystemRealWorldClock());

            trace.RecordHeader(DefaultSeed, catalog.ContentVersion, 1, host.Simulation.RandomAlgorithmVersion);

            SampleWorldLayout layout = SampleWorld.Populate(host);
            Console.WriteLine($"World built at {host.World.Clock.Now}: {host.World.Characters.Count} characters, {host.World.Locations.Count} locations, {host.World.Scheduler.PendingCount} pending events.");

            // Run to the middle of the working day. Mina travels and starts her shift on the way.
            host.Session.Advance(SimDuration.FromHours(5));
            Console.WriteLine($"\n-- {host.World.Clock.Now} --");
            PrintCharacter(host, layout.Mina);

            // The player begins watching her: one canonical watch signal feeding observation (§20.1, §25).
            host.Session.Enqueue(new BeginObservingCharacterCommand(layout.Mina));
            host.Session.Enqueue(new InspectCharacterCommand(layout.Mina));
            host.Session.Pump();
            Console.WriteLine($"\nAfter observing, the player knows {host.World.Knowledge.Count} fact(s).");

            // A decision forms with a true influence set the player only partly sees (§17, §26).
            Decision decision = SampleWorld.CreateJobOfferDecision(host, layout.Mina, layout.Cafe);
            host.Session.Advance(SimDuration.Zero);

            var projector = new DecisionProjector(catalog.Interventions);
            Console.WriteLine("\n-- decision as the player sees it --");
            PrintDecision(projector.Project(host.World, decision));

            // Hold it, so it does not auto-resolve while the player thinks (§20).
            Console.WriteLine("\nHold: " + host.Session.Execute(new HoldDecisionCommand(decision.Id)));

            // The world changes while the decision is open: a better opportunity appears (§17.2).
            decision.ChangeInfluenceDie(FindInfluence(decision, "influence.good_location"), Die.D10);
            host.World.BumpRevision(decision.InfluenceRevisionKey);
            Console.WriteLine($"A better location appears — influence revision is now {decision.InfluenceRevision}.");

            // The player spends an intervention on a stable influence id (§19).
            DecisionInfluenceId ambition = FindInfluence(decision, SampleContent.TraitAmbitious.Value);
            Result intervention = host.Session.Execute(new ApplyDecisionInterventionCommand(decision.Id, SampleContent.InterventionStepUp, ambition));
            Console.WriteLine($"Intervention: {intervention}");

            // Release and let it resolve.
            Console.WriteLine("Release: " + host.Session.Execute(new ReleaseDecisionCommand(decision.Id)));
            host.Session.Advance(SimDuration.FromHours(12));

            Console.WriteLine("\n-- resolved --");
            PrintDecision(projector.Project(host.World, decision));

            Console.WriteLine($"\n{host.Session.PerformanceSummary()}");
            Console.WriteLine($"trace entries: {trace.Entries.Count}");
            return 0;
        }

        /// <summary>
        /// Same seed, same ordered commands, same content and rules versions — same world (§15, §51).
        /// </summary>
        private static int RunDeterminismCheck()
        {
            string first = RunScenarioSignature();
            string second = RunScenarioSignature();

            Console.WriteLine("run 1: " + first);
            Console.WriteLine("run 2: " + second);

            if (first == second)
            {
                Console.WriteLine("PASS — identical authoritative outcome.");
                return 0;
            }

            Console.WriteLine("FAIL — the two runs diverged.");
            return 1;
        }

        /// <summary>
        /// Saving before resolution and reloading must produce precisely the same resulting world (§56).
        /// </summary>
        private static int RunSaveLoadCheck()
        {
            DefinitionCatalog catalog = SampleContent.Build();
            var store = new InMemorySaveGameStore();
            var clock = new FixedRealWorldClock(0);

            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                DefaultSeed, SimTime.FromClockTime(0, 7, 0), catalog, 1, null, store, clock);

            SampleWorldLayout layout = SampleWorld.Populate(host);
            host.Session.Advance(SimDuration.FromHours(3));
            Decision decision = SampleWorld.CreateJobOfferDecision(host, layout.Mina, layout.Cafe);
            host.Session.Advance(SimDuration.Zero);

            // Save with the decision still open, then finish it in the original session.
            host.Session.Save("checkpoint");
            host.Session.Advance(SimDuration.FromHours(12));
            string original = Signature(host);

            // Reload and run the identical remaining advance.
            store.TryLoad("checkpoint", out SaveGameData saved);
            WorldState restoredWorld = host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld, catalog, saved.LastCommandSequence, 1, null, store, clock);

            restored.Session.Advance(SimDuration.FromHours(12));
            string reloaded = Signature(restored);

            Console.WriteLine("original: " + original);
            Console.WriteLine("reloaded: " + reloaded);

            if (original == reloaded)
            {
                Console.WriteLine("PASS — the reloaded world resolved identically.");
                return 0;
            }

            Console.WriteLine("FAIL — reload diverged from the original run.");
            return 1;
        }

        /// <summary>
        /// Synthetic population benchmark (§49). Reports the numbers the performance contract asks for
        /// before anyone starts optimizing.
        /// </summary>
        private static int RunBenchmark(int population, int days)
        {
            DefinitionCatalog catalog = SampleContent.Build();

            var buildWatch = Stopwatch.StartNew();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(DefaultSeed, SimTime.Epoch, catalog);
            SampleWorld.Populate(host, Math.Max(0, population - 3));
            buildWatch.Stop();

            long eventsBefore = host.World.Scheduler.PendingCount;

            var runWatch = Stopwatch.StartNew();
            host.Session.Advance(SimDuration.FromDays(days), SimulationMode.PlayerFastForward);
            runWatch.Stop();

            long managedBytes = GC.GetTotalMemory(false);

            Console.WriteLine($"population           : {host.World.Characters.Count}");
            Console.WriteLine($"build                : {buildWatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"simulated days       : {days}");
            Console.WriteLine($"run                  : {runWatch.ElapsedMilliseconds} ms ({(days == 0 ? 0 : runWatch.ElapsedMilliseconds / days)} ms/day)");
            Console.WriteLine($"pending events       : {eventsBefore} -> {host.World.Scheduler.PendingCount}");
            Console.WriteLine($"activities created   : {host.World.Activities.Count}");
            Console.WriteLine($"managed heap         : {managedBytes / (1024 * 1024)} MB");
            Console.WriteLine($"runner               : {host.Session.PerformanceSummary()}");
            return 0;
        }

        private static string RunScenarioSignature()
        {
            DefinitionCatalog catalog = SampleContent.Build();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(DefaultSeed, SimTime.FromClockTime(0, 7, 0), catalog);
            SampleWorldLayout layout = SampleWorld.Populate(host);

            host.Session.Advance(SimDuration.FromHours(3));
            host.Session.Enqueue(new BeginObservingCharacterCommand(layout.Mina));
            host.Session.Pump();

            Decision decision = SampleWorld.CreateJobOfferDecision(host, layout.Mina, layout.Cafe);
            host.Session.Advance(SimDuration.Zero);
            host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id,
                SampleContent.InterventionStepUp,
                FindInfluence(decision, SampleContent.TraitAmbitious.Value)));

            host.Session.Advance(SimDuration.FromHours(12));
            return Signature(host);
        }

        /// <summary>
        /// A compact fingerprint of authoritative state. Determinism means two runs agree on this.
        /// </summary>
        private static string Signature(SimulationHost host)
        {
            ulong hash = 1469598103934665603UL;
            WorldState world = host.World;

            hash = StableHash.Combine(hash, world.Clock.Now.TotalMinutes);
            hash = StableHash.Combine(hash, world.Characters.Count);
            hash = StableHash.Combine(hash, world.Activities.Count);
            hash = StableHash.Combine(hash, world.Scheduler.PendingCount);
            hash = StableHash.Combine(hash, world.Knowledge.Count);

            foreach (Decision decision in world.Decisions.All)
            {
                hash = StableHash.Combine(hash, decision.Id.Value);
                hash = StableHash.Combine(hash, (int)decision.Status);
                hash = StableHash.Combine(hash, decision.InfluenceRevision);

                if (decision.Resolution != null)
                {
                    hash = StableHash.Combine(hash, decision.Resolution.ChosenOptionId);
                    hash = StableHash.Combine(hash, (int)decision.Resolution.Degree);

                    for (int i = 0; i < decision.Resolution.Rolls.Count; i++)
                    {
                        hash = StableHash.Combine(hash, decision.Resolution.Rolls[i].Rolled);
                    }
                }
            }

            foreach (Domain.Activities.ActivityInstance activity in world.Activities.All)
            {
                hash = StableHash.Combine(hash, activity.Id.Value);
                hash = StableHash.Combine(hash, activity.DefinitionId);
                hash = StableHash.Combine(hash, (int)activity.Status);
            }

            return hash.ToString("X16");
        }

        private static DecisionInfluenceId FindInfluence(Decision decision, string labelId)
        {
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                if (decision.Influences[i].LabelId.Value == labelId)
                {
                    return decision.Influences[i].Id;
                }
            }

            throw new InvalidOperationException($"No influence labelled '{labelId}' on decision {decision.Id}.");
        }

        private static void PrintCharacter(SimulationHost host, CharacterId characterId)
        {
            var projector = new CharacterProfileProjector();
            if (!projector.TryProject(host.World, characterId, out CharacterProfileView view))
            {
                Console.WriteLine("  (unknown character)");
                return;
            }

            Console.WriteLine($"  {view.DisplayName}: {view.CurrentActivityLabel} @ {view.LocationLabel}");
            Console.WriteLine($"  known traits: {view.KnownTraits.Count}, known needs: {view.KnownNeeds.Count}");
        }

        private static void PrintDecision(DecisionView view)
        {
            Console.WriteLine($"  {view.CharacterName} — {view.DefinitionId} [{view.StatusLabel}, revision {view.InfluenceRevision}]");

            for (int o = 0; o < view.Options.Count; o++)
            {
                DecisionOptionView option = view.Options[o];
                Console.WriteLine($"    {option.Label}");

                for (int i = 0; i < option.Influences.Count; i++)
                {
                    InfluenceView influence = option.Influences[i];
                    string label = influence.Label ?? influence.Category ?? "???";
                    string die = influence.DieSides.HasValue ? "d" + influence.DieSides.Value : string.Empty;
                    Console.WriteLine($"      {label,-34} {die}");
                }
            }

            if (view.Resolution != null)
            {
                Console.WriteLine($"    => {view.Resolution.ChosenOptionId} ({view.Resolution.DegreeLabel}, {view.Resolution.OutcomeSourceLabel})");
            }
        }
    }
}
