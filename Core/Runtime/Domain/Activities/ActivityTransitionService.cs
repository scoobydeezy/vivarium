using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// The one place primary Activities are switched (§29.1, §29.5).
    /// <para>
    /// Every transition does the same five things, in order: retire the previous instance, install the
    /// new one, move the occupancy indexes, bump the aspect-scoped Activity revision, and schedule the
    /// completion event. Doing them anywhere else is how "exactly one primary Activity" and
    /// "indexes agree with Activity state" quietly stop being true (§51).
    /// </para>
    /// <para>
    /// The revision bump is what retires the outgoing Activity's pending completion event: it was
    /// scheduled against the old revision, so the settlement loop discards it as stale (§11.2).
    /// </para>
    /// </summary>
    public sealed class ActivityTransitionService
    {
        /// <summary>Committed parameter key for the performance rate an Activity started with (§42.1).</summary>
        public static readonly AuthoredId PerformanceRateParameter = new AuthoredId("activity.param.performance_rate");

        /// <summary>
        /// Starts a stationary Activity at a location.
        /// </summary>
        public ActivityInstance BeginActivity(
            SimulationContext context,
            CharacterId characterId,
            AuthoredId definitionId,
            LocationId locationId,
            SimDuration duration,
            long performanceRatePerMinute = 0,
            CommitmentId sourceCommitmentId = default,
            IReadOnlyDictionary<AuthoredId, long> committedParameters = null)
        {
            return Begin(
                context,
                characterId,
                definitionId,
                ActivitySpatialContext.Located(locationId),
                duration,
                performanceRatePerMinute,
                sourceCommitmentId,
                ScheduledEventTypes.ActivityComplete,
                default,
                default,
                committedParameters);
        }

        /// <summary>
        /// Starts travel toward <paramref name="destination"/>, committing the route's timing (§29.2).
        /// <para>
        /// She is not simultaneously stationary at the origin and the destination: the Traveling
        /// Activity replaces the previous one, and direct occupancy drops until arrival (§30).
        /// </para>
        /// </summary>
        public bool TryBeginTravel(
            SimulationContext context,
            CharacterId characterId,
            LocationId destination,
            out ActivityInstance travelActivity,
            CommitmentId sourceCommitmentId = default,
            AuthoredId continuationActivityDefinitionId = default,
            SimDuration continuationDuration = default,
            IReadOnlyDictionary<AuthoredId, long> continuationCommittedParameters = null)
        {
            travelActivity = null;
            WorldState world = context.World;

            if (!world.TryGetSpatialContext(characterId, out ActivitySpatialContext current) || !current.IsLocated)
            {
                return false;
            }

            if (!world.TravelNetwork.TryPlanRoute(current.LocationId, destination, out TravelPlan plan))
            {
                return false;
            }

            SimTime now = world.Clock.Now;
            var transit = new TransitDetails(
                current.LocationId,
                destination,
                now,
                now.Plus(plan.TotalCost),
                plan.PrimaryTravelModeId,
                plan.IsTrivial ? 0 : plan.Legs.Count);

            travelActivity = Begin(
                context,
                characterId,
                WellKnownActivities.Traveling,
                ActivitySpatialContext.Traveling(transit),
                plan.TotalCost,
                0,
                sourceCommitmentId,
                ScheduledEventTypes.TravelArrival,
                continuationActivityDefinitionId,
                continuationDuration,
                null,
                continuationCommittedParameters);

            return true;
        }

        /// <summary>
        /// Applies a context change to an in-progress Activity and repairs its completion schedule
        /// (§29.7, §10.2).
        /// </summary>
        public void ApplyContextModifier(
            SimulationContext context,
            ActivityInstance activity,
            ActivityContextModifier modifier)
        {
            WorldState world = context.World;
            SimTime now = world.Clock.Now;

            // 1. Materialize what accumulated under the old context.
            activity.ApplyContextChange(now, modifier.PerformanceRateNumerator, modifier.PerformanceRateDenominator);
            activity.AddModifier(modifier);

            // 2. Invalidate the pending completion and 3. bump the revision it depended on.
            world.Scheduler.Cancel(activity.PendingCompletionEventId);
            world.BumpRevision(activity.ActivityRevisionKey);

            // 4-5. Recompute and reschedule the meaningful completion.
            ScheduleCompletion(context, activity, ScheduledEventTypes.ActivityComplete);
        }

        /// <summary>Removes a modifier, materializing its effect over the interval it applied for.</summary>
        public void RemoveContextModifier(
            SimulationContext context,
            ActivityInstance activity,
            AuthoredId modifierId,
            long restoredRateNumerator,
            long restoredRateDenominator = 1)
        {
            WorldState world = context.World;
            activity.ApplyContextChange(world.Clock.Now, restoredRateNumerator, restoredRateDenominator);
            activity.RemoveModifier(modifierId);

            world.Scheduler.Cancel(activity.PendingCompletionEventId);
            world.BumpRevision(activity.ActivityRevisionKey);
            ScheduleCompletion(context, activity, ScheduledEventTypes.ActivityComplete);
        }

        private ActivityInstance Begin(
            SimulationContext context,
            CharacterId characterId,
            AuthoredId definitionId,
            ActivitySpatialContext spatialContext,
            SimDuration duration,
            long performanceRatePerMinute,
            CommitmentId sourceCommitmentId,
            AuthoredId completionEventType,
            AuthoredId continuationActivityDefinitionId,
            SimDuration continuationDuration,
            IReadOnlyDictionary<AuthoredId, long> committedParameters = null,
            IReadOnlyDictionary<AuthoredId, long> continuationCommittedParameters = null)
        {
            WorldState world = context.World;
            SimTime now = world.Clock.Now;

            if (!world.Characters.TryGet(characterId, out Character character))
            {
                throw new InvalidOperationException($"{characterId} is not an active character.");
            }

            ActivitySpatialContext? previousContext = null;
            if (character.CurrentActivityId.IsSet && world.Activities.TryGet(character.CurrentActivityId, out ActivityInstance previous))
            {
                previousContext = previous.SpatialContext;
                if (previous.Status == ActivityStatus.Active)
                {
                    previous.Abandon(now);
                }

                world.Scheduler.Cancel(previous.PendingCompletionEventId);

                if (previousContext.Value.IsLocated)
                {
                    world.Publish(new CharacterDepartedEvent(characterId, previousContext.Value.LocationId));
                }
            }

            var activity = new ActivityInstance(
                world.RuntimeIds.Activities.Next(),
                characterId,
                definitionId,
                now,
                spatialContext,
                AnalyticalProgression.OverDuration(0, 10000, now, duration),
                AnalyticalProgression.Linear(0, now, performanceRatePerMinute),
                sourceCommitmentId);

            // Snapshot the outcome-affecting parameter this instance was constructed with, so a later
            // content reload cannot rewrite how it resolves (§42.1).
            activity.CommitParameter(PerformanceRateParameter, performanceRatePerMinute);
            if (committedParameters != null)
            {
                foreach (KeyValuePair<AuthoredId, long> parameter in committedParameters)
                    activity.CommitParameter(parameter.Key, parameter.Value);
            }

            world.Activities.Add(activity.Id, activity);
            character.SetCurrentActivity(activity.Id);
            world.Spatial.ApplyTransition(characterId, previousContext, spatialContext);

            // Bump before capturing the dependency, so the new completion event depends on the new
            // revision and the outgoing Activity's event is left stale (§11.2).
            world.BumpRevision(activity.ActivityRevisionKey);

            ScheduleCompletion(
                context,
                activity,
                completionEventType,
                continuationActivityDefinitionId,
                continuationDuration,
                continuationCommittedParameters);

            world.Publish(new ActivityStartedEvent(characterId, activity.Id, definitionId));

            if (spatialContext.IsLocated)
            {
                world.Publish(new CharacterArrivedEvent(characterId, spatialContext.LocationId));
            }

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record("activity", $"{now} {characterId} began {definitionId} {spatialContext}");
            }

            return activity;
        }

        private static void ScheduleCompletion(
            SimulationContext context,
            ActivityInstance activity,
            AuthoredId eventType,
            AuthoredId continuationActivityDefinitionId = default,
            SimDuration continuationDuration = default,
            IReadOnlyDictionary<AuthoredId, long> continuationCommittedParameters = null)
        {
            WorldState world = context.World;

            if (!activity.Progress.TryTimeOfCompletion(out SimTime completesAt))
            {
                // A progression with no rate never completes on its own; something else must end it.
                activity.SetPendingCompletionEvent(ScheduledEventId.None);
                return;
            }

            var dependency = EventDependency.Capture(world.Revisions, activity.ActivityRevisionKey);

            IScheduledEventPayload payload = eventType == ScheduledEventTypes.TravelArrival
                ? (IScheduledEventPayload)new TravelArrivalPayload(
                    activity.Id,
                    activity.CharacterId,
                    activity.SpatialContext.Transit.DestinationLocationId,
                    continuationActivityDefinitionId,
                    continuationDuration,
                    continuationCommittedParameters)
                : new ActivityCompletionPayload(activity.Id, activity.CharacterId);

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                completesAt,
                SchedulePhase.Activity,
                eventType,
                payload,
                new[] { dependency });

            activity.SetPendingCompletionEvent(scheduled.Id);
        }
    }
}
