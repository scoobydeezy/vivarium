using System;
using System.Diagnostics;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;

namespace Vivarium.SimRunner
{
    public sealed class ScaleBudget
    {
        public static readonly ScaleBudget StandardOneDay = new ScaleBudget(
            population: 1000,
            duration: SimDuration.FromDays(1),
            maximumBuildMilliseconds: 2000,
            maximumRunMilliseconds: 15000,
            maximumManagedMegabytes: 128,
            maximumWorkPerCharacter: 320,
            maximumActivitiesPerCharacter: 30,
            maximumPendingEventsPerCharacter: 2);

        public ScaleBudget(
            int population,
            SimDuration duration,
            long maximumBuildMilliseconds,
            long maximumRunMilliseconds,
            long maximumManagedMegabytes,
            long maximumWorkPerCharacter,
            long maximumActivitiesPerCharacter,
            long maximumPendingEventsPerCharacter)
        {
            Population = population;
            Duration = duration;
            MaximumBuildMilliseconds = maximumBuildMilliseconds;
            MaximumRunMilliseconds = maximumRunMilliseconds;
            MaximumManagedMegabytes = maximumManagedMegabytes;
            MaximumWorkPerCharacter = maximumWorkPerCharacter;
            MaximumActivitiesPerCharacter = maximumActivitiesPerCharacter;
            MaximumPendingEventsPerCharacter = maximumPendingEventsPerCharacter;
        }

        public int Population { get; }
        public SimDuration Duration { get; }
        public long MaximumBuildMilliseconds { get; }
        public long MaximumRunMilliseconds { get; }
        public long MaximumManagedMegabytes { get; }
        public long MaximumWorkPerCharacter { get; }
        public long MaximumActivitiesPerCharacter { get; }
        public long MaximumPendingEventsPerCharacter { get; }
    }

    public sealed class ScaleBenchmarkResult
    {
        public int Population { get; set; }
        public SimDuration Duration { get; set; }
        public long BuildMilliseconds { get; set; }
        public long RunMilliseconds { get; set; }
        public long ManagedBytes { get; set; }
        public long InstantsSettled { get; set; }
        public long WorkProcessed { get; set; }
        public int PendingEvents { get; set; }
        public int ActivitiesCreated { get; set; }
        public string Signature { get; set; }

        public long ManagedMegabytes => ManagedBytes / (1024 * 1024);
        public long WorkPerCharacter => DivideCeiling(WorkProcessed, Population);
        public long ActivitiesPerCharacter => DivideCeiling(ActivitiesCreated, Population);
        public long PendingEventsPerCharacter => DivideCeiling(PendingEvents, Population);

        private static long DivideCeiling(long value, int divisor) =>
            divisor <= 0 ? 0 : (value + divisor - 1) / divisor;
    }

    public static class ScaleBenchmark
    {
        public const long DefaultSeed = 827119;

        public static ScaleBenchmarkResult Run(int population, SimDuration duration, long seed = DefaultSeed)
        {
            if (population < 3) throw new ArgumentOutOfRangeException(nameof(population), "The sample world has three named characters.");
            if (duration.IsNegative) throw new ArgumentOutOfRangeException(nameof(duration));

            var buildWatch = Stopwatch.StartNew();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                seed,
                SimTime.Epoch,
                SampleContent.Build());
            SampleWorld.Populate(host, population - 3);
            buildWatch.Stop();

            var runWatch = Stopwatch.StartNew();
            host.Session.Advance(duration, SimulationMode.PlayerFastForward);
            runWatch.Stop();

            return new ScaleBenchmarkResult
            {
                Population = host.World.Characters.Count,
                Duration = duration,
                BuildMilliseconds = buildWatch.ElapsedMilliseconds,
                RunMilliseconds = runWatch.ElapsedMilliseconds,
                ManagedBytes = GC.GetTotalMemory(false),
                InstantsSettled = host.Session.InstantsSettled,
                WorkProcessed = host.Session.WorkProcessed,
                PendingEvents = host.World.Scheduler.PendingCount,
                ActivitiesCreated = host.World.Activities.Count,
                Signature = Signature(host.World),
            };
        }

        public static bool MeetsStructuralBudget(ScaleBenchmarkResult result, ScaleBudget budget) =>
            result.WorkPerCharacter <= budget.MaximumWorkPerCharacter &&
            result.ActivitiesPerCharacter <= budget.MaximumActivitiesPerCharacter &&
            result.PendingEventsPerCharacter <= budget.MaximumPendingEventsPerCharacter;

        public static bool MeetsMeasuredBudget(ScaleBenchmarkResult result, ScaleBudget budget) =>
            result.BuildMilliseconds <= budget.MaximumBuildMilliseconds &&
            result.RunMilliseconds <= budget.MaximumRunMilliseconds &&
            result.ManagedMegabytes <= budget.MaximumManagedMegabytes;

        private static string Signature(WorldState world)
        {
            ulong hash = 1469598103934665603UL;
            RuntimeIdCounters ids = world.RuntimeIds.Snapshot();
            hash = StableHash.Combine(hash, world.Clock.Now.TotalMinutes);
            hash = StableHash.Combine(hash, ids.Characters);
            hash = StableHash.Combine(hash, ids.Activities);
            hash = StableHash.Combine(hash, ids.Commitments);
            hash = StableHash.Combine(hash, ids.CommitmentOutcomes);
            hash = StableHash.Combine(hash, ids.Relationships);
            hash = StableHash.Combine(hash, ids.Decisions);
            hash = StableHash.Combine(hash, ids.ScheduledEvents);
            hash = StableHash.Combine(hash, ids.HistoryEntries);
            hash = StableHash.Combine(hash, ids.EventSequence);

            foreach (ScheduledEvent scheduled in world.Scheduler.PendingEvents)
            {
                hash = StableHash.Combine(hash, scheduled.Id.Value);
                hash = StableHash.Combine(hash, scheduled.DueAt.TotalMinutes);
                hash = StableHash.Combine(hash, (int)scheduled.Phase);
                hash = StableHash.Combine(hash, scheduled.EventSequence);
                hash = StableHash.Combine(hash, scheduled.EventType.StableHashCode);
            }

            foreach (Character character in world.Characters.All)
            {
                hash = StableHash.Combine(hash, character.Id.Value);
                hash = StableHash.Combine(hash, character.CurrentActivityId.Value);
                foreach (var pair in character.Needs)
                {
                    NeedState need = pair.Value;
                    hash = StableHash.Combine(hash, need.NeedId.StableHashCode);
                    hash = StableHash.Combine(hash, need.ValueAt(world.Clock.Now));
                    hash = StableHash.Combine(hash, need.BehaviouralThreshold);
                }
            }

            foreach (ActivityInstance activity in world.Activities.All)
            {
                hash = StableHash.Combine(hash, activity.Id.Value);
                hash = StableHash.Combine(hash, activity.CharacterId.Value);
                hash = StableHash.Combine(hash, activity.DefinitionId.StableHashCode);
                hash = StableHash.Combine(hash, (int)activity.Status);
                hash = StableHash.Combine(hash, activity.SpatialContext.DirectOccupancy.Value);
            }

            foreach (Decision decision in world.Decisions.All)
            {
                hash = StableHash.Combine(hash, decision.Id.Value);
                hash = StableHash.Combine(hash, decision.CharacterId.Value);
                hash = StableHash.Combine(hash, (int)decision.Status);
                hash = StableHash.Combine(hash, decision.InfluenceRevision);
                if (decision.Resolution != null)
                {
                    hash = StableHash.Combine(hash, decision.Resolution.ChosenOptionId.StableHashCode);
                    for (int i = 0; i < decision.Resolution.Rolls.Count; i++)
                    {
                        hash = StableHash.Combine(hash, decision.Resolution.Rolls[i].Rolled);
                    }
                }
            }

            foreach (Relationship relationship in world.Relationships.All)
            {
                hash = StableHash.Combine(hash, relationship.Id.Value);
                hash = StableHash.Combine(hash, relationship.LowToHigh.ChannelAt(RelationshipChannels.Affection, world.Clock.Now));
                hash = StableHash.Combine(hash, relationship.LowToHigh.FamiliarityAt(world.Clock.Now));
                hash = StableHash.Combine(hash, relationship.HighToLow.ChannelAt(RelationshipChannels.Affection, world.Clock.Now));
                hash = StableHash.Combine(hash, relationship.HighToLow.FamiliarityAt(world.Clock.Now));
                hash = StableHash.Combine(hash, relationship.LastInteractionAt?.TotalMinutes ?? -1);
            }

            return hash.ToString("X16");
        }
    }
}
