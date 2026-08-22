using System;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Characters
{
    /// <summary>A need crossed a behaviourally meaningful threshold (§10.2).</summary>
    public sealed class NeedThresholdReachedEvent : IDomainEvent
    {
        public static readonly AuthoredId Type = new AuthoredId("domain.need.threshold_reached");

        public NeedThresholdReachedEvent(CharacterId characterId, AuthoredId needId, long threshold, long value)
        {
            CharacterId = characterId;
            NeedId = needId;
            Threshold = threshold;
            Value = value;
        }

        public AuthoredId EventType => Type;

        public CharacterId CharacterId { get; }

        public AuthoredId NeedId { get; }

        public long Threshold { get; }

        public long Value { get; }
    }

    /// <summary>
    /// Keeps analytical needs and their scheduled threshold crossings in step (§10.1, §10.2).
    /// <para>
    /// The whole update sequence lives here so no caller can perform half of it: materialize the
    /// current value, invalidate the pending crossing, bump the aspect-scoped revision, compute the next
    /// crossing, schedule it. Skipping the last two steps is how an analytical value silently stops
    /// affecting behaviour (invariants 13–14).
    /// </para>
    /// </summary>
    public sealed class NeedProgressionService
    {
        /// <summary>Changes a need's rate — a fever starts, a job gets harder, a room gets colder.</summary>
        public void SetRate(
            SimulationContext context,
            Character character,
            AuthoredId needId,
            long ratePerMinuteNumerator,
            long ratePerMinuteDenominator = 1)
        {
            if (!character.TryGetNeed(needId, out NeedState need))
            {
                throw new InvalidOperationException($"{character.Id} has no need '{needId}'.");
            }

            SimTime now = context.World.Clock.Now;
            NeedState updated = need.WithProgression(need.Progression.WithRate(now, ratePerMinuteNumerator, ratePerMinuteDenominator));
            Rearm(context, character, updated);
        }

        /// <summary>Applies an instantaneous change — a meal, a nap, a conversation.</summary>
        public void ApplyOffset(SimulationContext context, Character character, AuthoredId needId, long offset)
        {
            if (!character.TryGetNeed(needId, out NeedState need))
            {
                throw new InvalidOperationException($"{character.Id} has no need '{needId}'.");
            }

            SimTime now = context.World.Clock.Now;
            NeedState updated = need.WithProgression(need.Progression.WithOffset(now, offset));
            Rearm(context, character, updated);
        }

        /// <summary>Moves the watched threshold, e.g. after the previous one was crossed.</summary>
        public void SetThreshold(SimulationContext context, Character character, AuthoredId needId, long threshold)
        {
            if (!character.TryGetNeed(needId, out NeedState need))
            {
                throw new InvalidOperationException($"{character.Id} has no need '{needId}'.");
            }

            Rearm(context, character, need.WithThreshold(threshold));
        }

        /// <summary>
        /// Cancels any pending crossing, bumps the need's revision, and schedules the next crossing.
        /// </summary>
        public void Rearm(SimulationContext context, Character character, NeedState need)
        {
            WorldState world = context.World;

            world.Scheduler.Cancel(need.PendingThresholdEventId);
            RevisionKey revisionKey = need.RevisionKeyFor(character.Id);
            world.BumpRevision(revisionKey);

            if (!need.Progression.TryTimeOfCrossing(need.BehaviouralThreshold, out SimTime crossesAt))
            {
                // Unreachable threshold: no event, and none is needed. The value still evolves
                // analytically; nothing behavioural depends on a crossing that cannot happen.
                character.SetNeed(need.WithPendingThresholdEvent(ScheduledEventId.None));
                return;
            }

            if (crossesAt < world.Clock.Now)
            {
                crossesAt = world.Clock.Now;
            }

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                crossesAt,
                SchedulePhase.Progression,
                ScheduledEventTypes.NeedThreshold,
                new NeedThresholdPayload(character.Id, need.NeedId, need.BehaviouralThreshold),
                new[] { new EventDependency(revisionKey, world.Revisions.Get(revisionKey)) });

            character.SetNeed(need.WithPendingThresholdEvent(scheduled.Id));
        }
    }

    /// <summary>
    /// Announces a need threshold crossing so other systems can react (§10.2).
    /// <para>
    /// This handler deliberately does not decide what to <i>do</i> about hunger. It publishes the fact;
    /// ordered Domain Event handlers decide whether that generates a decision, changes a routine, or
    /// interrupts an Activity.
    /// </para>
    /// </summary>
    public sealed class NeedThresholdHandler : ScheduledEventHandler<NeedThresholdPayload>
    {
        public NeedThresholdHandler()
            : base(ScheduledEventTypes.NeedThreshold)
        {
        }

        protected override bool CanExecute(WorldState world, NeedThresholdPayload payload)
        {
            if (!world.Characters.TryGet(payload.CharacterId, out Character character) || !character.IsActive)
            {
                return false;
            }

            if (!character.TryGetNeed(payload.NeedId, out NeedState need))
            {
                return false;
            }

            // Semantic validation: the value really must be past the threshold now (§11.2).
            long value = need.ValueAt(world.Clock.Now);
            return need.Progression.IsIncreasing ? value >= payload.Threshold : value <= payload.Threshold;
        }

        protected override void Execute(WorldState world, NeedThresholdPayload payload, SimulationContext context)
        {
            Character character = world.Characters.Get(payload.CharacterId);
            character.TryGetNeed(payload.NeedId, out NeedState need);
            long value = need.ValueAt(world.Clock.Now);

            world.Publish(new NeedThresholdReachedEvent(payload.CharacterId, payload.NeedId, payload.Threshold, value));

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record("need", $"{world.Clock.Now} {payload.CharacterId} {payload.NeedId} crossed {payload.Threshold} (now {value})");
            }
        }
    }
}
