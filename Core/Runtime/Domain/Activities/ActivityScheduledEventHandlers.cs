using System;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Starts the Activity that fulfils a Commitment, travelling first if the character is elsewhere
    /// (§29.5).
    /// </summary>
    public sealed class ActivityStartHandler : ScheduledEventHandler<ActivityStartPayload>
    {
        private readonly ActivityTransitionService _transitions;
        private readonly CommitmentLifecycleService _commitments;

        public ActivityStartHandler(ActivityTransitionService transitions, CommitmentLifecycleService commitments = null)
            : base(ScheduledEventTypes.ActivityStart)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _commitments = commitments ?? new CommitmentLifecycleService();
        }

        protected override bool CanExecute(WorldState world, ActivityStartPayload payload)
        {
            if (!world.Characters.TryGet(payload.CharacterId, out Character character) || !character.IsActive)
            {
                return false;
            }

            if (!payload.CommitmentId.IsSet)
            {
                return true;
            }

            // Semantic validation, not a revision check: the commitment must still be waiting to start
            // and its window must not have closed (§11.1).
            return world.Commitments.TryGet(payload.CommitmentId, out Commitment commitment)
                && commitment.Status == CommitmentStatus.Planned
                && world.Clock.Now <= commitment.LatestStart;
        }

        protected override void Execute(WorldState world, ActivityStartPayload payload, SimulationContext context)
        {
            world.Commitments.TryGet(payload.CommitmentId, out Commitment commitment);

            bool atDestination = world.TryGetSpatialContext(payload.CharacterId, out ActivitySpatialContext current)
                && current.IsLocated
                && current.LocationId == payload.LocationId;

            if (!atDestination)
            {
                if (_transitions.TryBeginTravel(context, payload.CharacterId, payload.LocationId, out ActivityInstance _, payload.CommitmentId))
                {
                    return;
                }

                // Unreachable destination: nothing to do but let the commitment lapse.
                if (commitment != null)
                {
                    _commitments.Cancel(world, commitment, CommitmentOutcomeCauseKind.ExternalCancellation);
                }
                return;
            }

            SimDuration duration = commitment?.ExpectedDuration ?? SimDuration.FromHours(1);
            ActivityInstance activity = _transitions.BeginActivity(
                context,
                payload.CharacterId,
                payload.ActivityDefinitionId,
                payload.LocationId,
                duration,
                0,
                payload.CommitmentId);

            if (commitment != null)
            {
                _commitments.Start(world, commitment, activity.Id);
            }
        }
    }

    /// <summary>
    /// Handles arrival: the Traveling Activity ends and the next Activity begins at the destination
    /// (§29.2).
    /// </summary>
    public sealed class TravelArrivalHandler : ScheduledEventHandler<TravelArrivalPayload>
    {
        private readonly ActivityTransitionService _transitions;
        private readonly CommitmentLifecycleService _commitments;

        public TravelArrivalHandler(ActivityTransitionService transitions, CommitmentLifecycleService commitments = null)
            : base(ScheduledEventTypes.TravelArrival)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _commitments = commitments ?? new CommitmentLifecycleService();
        }

        protected override bool CanExecute(WorldState world, TravelArrivalPayload payload)
        {
            return world.Activities.TryGet(payload.ActivityInstanceId, out ActivityInstance activity)
                && activity.Status == ActivityStatus.Active
                && activity.SpatialContext.IsTraveling
                && world.Characters.TryGet(payload.CharacterId, out Character character)
                && character.CurrentActivityId == payload.ActivityInstanceId;
        }

        protected override void Execute(WorldState world, TravelArrivalPayload payload, SimulationContext context)
        {
            ActivityInstance travel = world.Activities.Get(payload.ActivityInstanceId);

            AuthoredId nextDefinition = WellKnownActivities.Waiting;
            SimDuration duration = SimDuration.FromHours(1);
            Commitment commitment = null;

            if (travel.SourceCommitmentId.IsSet &&
                world.Commitments.TryGet(travel.SourceCommitmentId, out commitment) &&
                commitment.Status == CommitmentStatus.Planned)
            {
                nextDefinition = commitment.ActivityDefinitionId;
                duration = commitment.ExpectedDuration;
            }
            else if (payload.ContinuationActivityDefinitionId.IsSet)
            {
                nextDefinition = payload.ContinuationActivityDefinitionId;
                duration = payload.ContinuationDuration;
            }

            ActivityInstance next = _transitions.BeginActivity(
                context,
                payload.CharacterId,
                nextDefinition,
                payload.DestinationLocationId,
                duration,
                0,
                travel.SourceCommitmentId,
                payload.ContinuationCommittedParameters);

            if (commitment != null)
            {
                _commitments.Start(world, commitment, next.Id);
            }
        }
    }

    /// <summary>
    /// Completes an Activity, resolving it automatically if no player-provided result was accepted
    /// (§29.6).
    /// <para>
    /// Automatic resolution runs here unconditionally when nothing else supplied a result. That is the
    /// invariant that lets ten thousand characters keep working while the player watches one of them.
    /// </para>
    /// </summary>
    public sealed class ActivityCompletionHandler : ScheduledEventHandler<ActivityCompletionPayload>
    {
        private readonly ActivityResolutionRegistry _resolution;
        private readonly ActivityTransitionService _transitions;
        private readonly CommitmentLifecycleService _commitments;

        public ActivityCompletionHandler(
            ActivityResolutionRegistry resolution,
            ActivityTransitionService transitions,
            CommitmentLifecycleService commitments = null)
            : base(ScheduledEventTypes.ActivityComplete)
        {
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            _commitments = commitments ?? new CommitmentLifecycleService();
        }

        protected override bool CanExecute(WorldState world, ActivityCompletionPayload payload)
        {
            return world.Activities.TryGet(payload.ActivityInstanceId, out ActivityInstance activity)
                && activity.Status == ActivityStatus.Active
                && world.Characters.TryGet(payload.CharacterId, out Character character)
                && character.CurrentActivityId == payload.ActivityInstanceId
                && activity.IsCompleteAt(world.Clock.Now);
        }

        protected override void Execute(WorldState world, ActivityCompletionPayload payload, SimulationContext context)
        {
            ActivityInstance activity = world.Activities.Get(payload.ActivityInstanceId);

            ActivityPerformanceResult result = activity.AcceptedResult
                ?? ResolveAutomatically(world, activity, context);

            _resolution.AcceptResult(world, activity, result, context);

            if (activity.SourceCommitmentId.IsSet && world.Commitments.TryGet(activity.SourceCommitmentId, out Commitment commitment))
            {
                _commitments.Fulfill(world, commitment);
            }

            // A character always has exactly one primary Activity (invariant 39), so idling is an
            // Activity too rather than an absence of one.
            if (activity.SpatialContext.IsLocated)
            {
                _transitions.BeginActivity(
                    context,
                    payload.CharacterId,
                    WellKnownActivities.Waiting,
                    activity.SpatialContext.LocationId,
                    SimDuration.FromHours(1));
            }
        }

        private ActivityPerformanceResult ResolveAutomatically(WorldState world, ActivityInstance activity, SimulationContext context)
        {
            if (_resolution.TryGetStrategy(activity.DefinitionId, out IActivityResolutionStrategy strategy))
            {
                return strategy.ResolveAutomatic(world, activity, context);
            }

            // No strategy registered: the Activity simply happened. Adequate, no magnitude.
            return ActivityPerformanceResult.Automatic(PerformanceGrade.Adequate, 0);
        }
    }
}
