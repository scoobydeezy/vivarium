using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    /// <summary>
    /// Scheduler and settlement tests (§51): tie ordering, cancellation, aspect-scoped staleness,
    /// same-instant cascades, explicit Domain Event handler order, and the runaway guard.
    /// </summary>
    public sealed class SchedulerAndSettlementTests
    {
        private static readonly AuthoredId TestEvent = new AuthoredId("test.event");
        private static readonly AuthoredId CascadeEvent = new AuthoredId("test.cascade");
        private static readonly AuthoredId TestDomainEvent = new AuthoredId("test.domain_event");

        private sealed class Payload : IScheduledEventPayload
        {
            public Payload(string tag) => Tag = tag;

            public string Tag { get; }
        }

        private sealed class RecordingHandler : ScheduledEventHandler<Payload>
        {
            private readonly List<string> _log;
            private readonly bool _canExecute;

            public RecordingHandler(List<string> log, bool canExecute = true)
                : base(TestEvent)
            {
                _log = log;
                _canExecute = canExecute;
            }

            protected override bool CanExecute(WorldState world, Payload payload) => _canExecute;

            protected override void Execute(WorldState world, Payload payload, SimulationContext context) => _log.Add(payload.Tag);
        }

        /// <summary>Schedules one more event at the same instant each time, up to a limit.</summary>
        private sealed class CascadingHandler : ScheduledEventHandler<Payload>
        {
            private readonly List<string> _log;
            private readonly int _limit;
            private int _fired;

            public CascadingHandler(List<string> log, int limit)
                : base(CascadeEvent)
            {
                _log = log;
                _limit = limit;
            }

            protected override bool CanExecute(WorldState world, Payload payload) => true;

            protected override void Execute(WorldState world, Payload payload, SimulationContext context)
            {
                _log.Add(payload.Tag);

                if (++_fired < _limit)
                {
                    world.Scheduler.Schedule(world.Clock.Now, SchedulePhase.Consequence, CascadeEvent, new Payload("cascade" + _fired));
                }
            }
        }

        private sealed class TestDomainEventInstance : IDomainEvent
        {
            public AuthoredId EventType => TestDomainEvent;
        }

        private sealed class NamedDomainHandler : IDomainEventHandler
        {
            private readonly List<string> _log;
            private readonly string _name;
            private readonly bool _reemit;

            public NamedDomainHandler(List<string> log, string name, bool reemit = false)
            {
                _log = log;
                _name = name;
                _reemit = reemit;
            }

            public AuthoredId EventType => TestDomainEvent;

            public void Handle(IDomainEvent domainEvent, WorldState world, SimulationContext context)
            {
                _log.Add(_name);

                if (_reemit)
                {
                    world.Publish(new TestDomainEventInstance());
                }
            }
        }

        private static SimulationContext Context(WorldState world, int maxSettlementWork = 100000) => new SimulationContext(
            world,
            new DeterministicRandomOracle(1),
            SimulationMode.Live,
            contentVersion: 1,
            simulationRulesVersion: 1,
            trace: null,
            maxSettlementWorkPerInstant: maxSettlementWork);

        [Fact]
        public void SameInstantEventsRunInPhaseThenSequenceOrder()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();
            SimTime now = SimTime.Epoch;

            // Deliberately scheduled out of execution order.
            world.Scheduler.Schedule(now, SchedulePhase.Consequence, TestEvent, new Payload("consequence"));
            world.Scheduler.Schedule(now, SchedulePhase.Progression, TestEvent, new Payload("progression-first"));
            world.Scheduler.Schedule(now, SchedulePhase.Progression, TestEvent, new Payload("progression-second"));
            world.Scheduler.Schedule(now, SchedulePhase.Expiration, TestEvent, new Payload("expiration"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            var settlement = new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry());

            settlement.SettleCurrentInstant(world, Context(world));

            Assert.Equal(new[] { "expiration", "progression-first", "progression-second", "consequence" }, log);
        }

        [Fact]
        public void CancelledEventNeverExecutes()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            ScheduledEvent doomed = world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Activity, TestEvent, new Payload("doomed"));
            world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Activity, TestEvent, new Payload("survivor"));

            Assert.True(world.Scheduler.Cancel(doomed.Id));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry()).SettleCurrentInstant(world, Context(world));

            Assert.Equal(new[] { "survivor" }, log);
        }

        [Fact]
        public void ReschedulingTakesALaterSequenceSoItCannotPrecedeItsCause()
        {
            var world = new WorldState(1, SimTime.Epoch);

            ScheduledEvent first = world.Scheduler.Schedule(new SimTime(10), SchedulePhase.Activity, TestEvent, new Payload("a"));
            ScheduledEvent second = world.Scheduler.Schedule(new SimTime(20), SchedulePhase.Activity, TestEvent, new Payload("b"));

            ScheduledEvent moved = world.Scheduler.Reschedule(first.Id, new SimTime(20));

            Assert.Equal(first.Id, moved.Id);
            Assert.True(moved.EventSequence > second.EventSequence);
            Assert.Equal(second, world.Scheduler.PeekNext());
        }

        [Fact]
        public void StaleEventIsDiscardedWhenItsAspectRevisionMoved()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();
            var key = new RevisionKey(EntityKind.Character, 42, RevisionAspects.Schedule);

            world.Scheduler.Schedule(
                SimTime.Epoch,
                SchedulePhase.Activity,
                TestEvent,
                new Payload("stale"),
                new[] { EventDependency.Capture(world.Revisions, key) });

            // Something changed the schedule this event depended on.
            world.BumpRevision(key);

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            var settlement = new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry());
            settlement.SettleCurrentInstant(world, Context(world));

            Assert.Empty(log);
            Assert.Equal(1, settlement.StaleEventsDiscarded);
        }

        [Fact]
        public void UnrelatedAspectChangeDoesNotInvalidateTheEvent()
        {
            // §11.2.1: MinaLeavesWork must not care that Mina got hungrier.
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();
            var scheduleKey = new RevisionKey(EntityKind.Character, 42, RevisionAspects.Schedule);
            var hungerKey = new RevisionKey(EntityKind.Character, 42, RevisionAspects.Scoped(RevisionAspects.Need, new AuthoredId("need.hunger")));

            world.Scheduler.Schedule(
                SimTime.Epoch,
                SchedulePhase.Activity,
                TestEvent,
                new Payload("leaves-work"),
                new[] { EventDependency.Capture(world.Revisions, scheduleKey) });

            world.BumpRevision(hungerKey);

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry()).SettleCurrentInstant(world, Context(world));

            Assert.Equal(new[] { "leaves-work" }, log);
        }

        [Fact]
        public void SemanticValidationIsAuthoritativeEvenWithMatchingRevisions()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Activity, TestEvent, new Payload("invalid"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log, canExecute: false));
            var settlement = new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry());
            settlement.SettleCurrentInstant(world, Context(world));

            Assert.Empty(log);
            Assert.Equal(1, settlement.StaleEventsDiscarded);
        }

        [Fact]
        public void SameInstantWorkSettlesToQuiescence()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Activity, CascadeEvent, new Payload("cascade0"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new CascadingHandler(log, limit: 5));
            int work = new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry()).SettleCurrentInstant(world, Context(world));

            Assert.Equal(5, log.Count);
            Assert.Equal(5, work);
            Assert.Equal(0, world.Scheduler.PendingCount);
        }

        [Fact]
        public void SchedulingSameInstantWorkIntoAnEarlierPhaseIsRejected()
        {
            var world = new WorldState(1, SimTime.Epoch);
            ScheduledEvent executing = world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Decision, TestEvent, new Payload("x"));

            world.Scheduler.EnterExecution(executing);

            // Later phase at the same instant: fine. Earlier phase: retroactive, and refused (§11.4).
            world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Consequence, TestEvent, new Payload("later"));
            Assert.Throws<System.InvalidOperationException>(() =>
                world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Progression, TestEvent, new Payload("earlier")));

            // A future instant may use any phase.
            world.Scheduler.Schedule(new SimTime(1), SchedulePhase.Preparation, TestEvent, new Payload("tomorrow"));
            world.Scheduler.ExitExecution();
        }

        [Fact]
        public void RunawayCascadeFailsLoudlyRatherThanSilentlyDeferring()
        {
            var world = new WorldState(1, SimTime.Epoch);
            world.Scheduler.Schedule(SimTime.Epoch, SchedulePhase.Activity, CascadeEvent, new Payload("cascade0"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new CascadingHandler(new List<string>(), limit: int.MaxValue));
            var settlement = new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry());

            SimulationCascadeLimitExceeded thrown = Assert.Throws<SimulationCascadeLimitExceeded>(
                () => settlement.SettleCurrentInstant(world, Context(world, maxSettlementWork: 50)));

            Assert.Equal(50, thrown.Limit);
            Assert.Contains("test.cascade", thrown.LastWorkDescription);
        }

        [Fact]
        public void DomainEventHandlersRunInExplicitlyRegisteredOrder()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            var domainHandlers = new OrderedDomainEventHandlerRegistry();

            // Registered out of order on purpose: execution follows the declared order, not this one.
            domainHandlers.Register(new NamedDomainHandler(log, "third"), 300);
            domainHandlers.Register(new NamedDomainHandler(log, "first"), 100);
            domainHandlers.Register(new NamedDomainHandler(log, "second"), 200);

            world.Publish(new TestDomainEventInstance());
            new SettlementLoop(new ScheduledEventHandlerRegistry(), domainHandlers).SettleCurrentInstant(world, Context(world));

            Assert.Equal(new[] { "first", "second", "third" }, log);
        }

        [Fact]
        public void DuplicateHandlerOrderIsRejectedSoChainsStayUnambiguous()
        {
            var domainHandlers = new OrderedDomainEventHandlerRegistry();
            domainHandlers.Register(new NamedDomainHandler(new List<string>(), "a"), 100);

            Assert.Throws<System.InvalidOperationException>(
                () => domainHandlers.Register(new NamedDomainHandler(new List<string>(), "b"), 100));
        }

        [Fact]
        public void DomainEventReactionLoopHitsTheSameRunawayGuard()
        {
            // §12.1: the guard covers Domain Event reactions too, so A → B → A fails rather than hangs.
            var world = new WorldState(1, SimTime.Epoch);
            var domainHandlers = new OrderedDomainEventHandlerRegistry();
            domainHandlers.Register(new NamedDomainHandler(new List<string>(), "reemitter", reemit: true), 100);

            world.Publish(new TestDomainEventInstance());
            var settlement = new SettlementLoop(new ScheduledEventHandlerRegistry(), domainHandlers);

            Assert.Throws<SimulationCascadeLimitExceeded>(
                () => settlement.SettleCurrentInstant(world, Context(world, maxSettlementWork: 100)));
        }

        [Fact]
        public void RunnerOnlyStopsAtInstantsThatHaveWork()
        {
            // Analytical progressions mean an empty stretch of time costs one hop, not 1,440 ticks (§10).
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            world.Scheduler.Schedule(new SimTime(600), SchedulePhase.Activity, TestEvent, new Payload("morning"));
            world.Scheduler.Schedule(new SimTime(1200), SchedulePhase.Activity, TestEvent, new Payload("evening"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            var runner = new SimulationRunner(new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry()));

            runner.AdvanceUntil(new SimTime(1440), Context(world));

            Assert.Equal(new[] { "morning", "evening" }, log);
            Assert.Equal(new SimTime(1440), world.Clock.Now);

            // Current instant, the two events, and the final target — not one per simulated minute.
            Assert.Equal(4, runner.InstantsSettled);
        }

        [Fact]
        public void EventsBeyondTheTargetStayQueued()
        {
            var world = new WorldState(1, SimTime.Epoch);
            var log = new List<string>();

            world.Scheduler.Schedule(new SimTime(100), SchedulePhase.Activity, TestEvent, new Payload("soon"));
            world.Scheduler.Schedule(new SimTime(5000), SchedulePhase.Activity, TestEvent, new Payload("later"));

            var registry = new ScheduledEventHandlerRegistry();
            registry.Register(new RecordingHandler(log));
            new SimulationRunner(new SettlementLoop(registry, new OrderedDomainEventHandlerRegistry()))
                .AdvanceUntil(new SimTime(1000), Context(world));

            Assert.Equal(new[] { "soon" }, log);
            Assert.Equal(1, world.Scheduler.PendingCount);
        }
    }
}
