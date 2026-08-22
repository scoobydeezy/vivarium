using System;
using Vivarium.Application.Observation;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;

namespace Vivarium.Application.Commands.Handlers
{
    /// <summary>Advances authoritative time through the runner (§33).</summary>
    public sealed class AdvanceSimulationHandler : CommandHandler<AdvanceSimulationCommand, Result>
    {
        public static readonly AuthoredId ReasonNegativeDuration = new AuthoredId("command.advance.negative_duration");

        private readonly SimulationRunner _runner;

        public AdvanceSimulationHandler(SimulationRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public override Result Handle(AdvanceSimulationCommand command, CommandContext context)
        {
            if (command.Duration.IsNegative)
            {
                return Result.Fail(ReasonNegativeDuration, command.Duration.ToString());
            }

            // The mode belongs to the advance, not the session: fast-forwarding one stretch does not
            // make the whole session a fast-forward (§21).
            SimulationContext scoped = context.Simulation.Mode == command.Mode
                ? context.Simulation
                : context.Simulation.WithMode(command.Mode);

            _runner.AdvanceBy(command.Duration, scoped, command.PublishEveryInstants);
            return Result.Ok();
        }
    }

    /// <summary>Sets the durable follow flag through the canonical watch signal (§20.1).</summary>
    public sealed class FollowCharacterHandler : CommandHandler<FollowCharacterCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownCharacter = new AuthoredId("command.follow.unknown_character");

        private readonly WatchSignalService _watch;

        public FollowCharacterHandler(WatchSignalService watch)
        {
            _watch = watch ?? throw new ArgumentNullException(nameof(watch));
        }

        public override Result Handle(FollowCharacterCommand command, CommandContext context)
        {
            if (!context.World.Characters.Contains(command.CharacterId))
            {
                return Result.Fail(ReasonUnknownCharacter, command.CharacterId.ToString());
            }

            _watch.SetFollowed(context.World, command.CharacterId, command.Follow);
            return Result.Ok();
        }
    }

    /// <summary>
    /// A character became observable. Feeds the canonical watch signal, which in turn creates a
    /// knowledge-discovery opportunity (§25).
    /// </summary>
    public sealed class BeginObservingCharacterHandler : CommandHandler<BeginObservingCharacterCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownCharacter = new AuthoredId("command.observe.unknown_character");

        private readonly WatchSignalService _watch;

        public BeginObservingCharacterHandler(WatchSignalService watch)
        {
            _watch = watch ?? throw new ArgumentNullException(nameof(watch));
        }

        public override Result Handle(BeginObservingCharacterCommand command, CommandContext context)
        {
            if (!context.World.Characters.Contains(command.CharacterId))
            {
                return Result.Fail(ReasonUnknownCharacter, command.CharacterId.ToString());
            }

            _watch.SetVisible(context.Simulation, command.CharacterId, true);
            return Result.Ok();
        }
    }

    /// <summary>A character stopped being observable (§25).</summary>
    public sealed class EndObservingCharacterHandler : CommandHandler<EndObservingCharacterCommand, Result>
    {
        private readonly WatchSignalService _watch;

        public EndObservingCharacterHandler(WatchSignalService watch)
        {
            _watch = watch ?? throw new ArgumentNullException(nameof(watch));
        }

        public override Result Handle(EndObservingCharacterCommand command, CommandContext context)
        {
            _watch.SetVisible(context.Simulation, command.CharacterId, false);
            return Result.Ok();
        }
    }

    /// <summary>The player opened or closed a character profile (§24, §25).</summary>
    public sealed class InspectCharacterHandler : CommandHandler<InspectCharacterCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownCharacter = new AuthoredId("command.inspect.unknown_character");

        private readonly WatchSignalService _watch;

        public InspectCharacterHandler(WatchSignalService watch)
        {
            _watch = watch ?? throw new ArgumentNullException(nameof(watch));
        }

        public override Result Handle(InspectCharacterCommand command, CommandContext context)
        {
            if (!context.World.Characters.Contains(command.CharacterId))
            {
                return Result.Fail(ReasonUnknownCharacter, command.CharacterId.ToString());
            }

            _watch.SetProfileOpen(context.Simulation, command.CharacterId, command.Open);
            return Result.Ok();
        }
    }

    public sealed class TravelCharacterHandler : CommandHandler<TravelCharacterCommand, Result>
    {
        public static readonly AuthoredId ReasonTravelUnavailable = new AuthoredId("command.travel.unavailable");

        private readonly ActivityTransitionService _transitions;

        public TravelCharacterHandler(ActivityTransitionService transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public override Result Handle(TravelCharacterCommand command, CommandContext context) =>
            _transitions.TryBeginTravel(
                context.Simulation,
                command.CharacterId,
                command.DestinationLocationId,
                out ActivityInstance _)
                ? Result.Ok()
                : Result.Fail(ReasonTravelUnavailable, command.DestinationLocationId.ToString());
    }

    /// <summary>
    /// Accepts a normalized result from interactive play (§29.6).
    /// <para>
    /// Validation matters here precisely because this is player-authored data crossing into
    /// authoritative state: the Activity must exist, be active, belong to the character's current
    /// primary Activity, and its definition must permit interactive resolution. The result then feeds
    /// the same consequence pipeline as automatic resolution (invariant 46).
    /// </para>
    /// </summary>
    public sealed class SubmitActivityPerformanceHandler : CommandHandler<SubmitActivityPerformanceCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownActivity = new AuthoredId("command.activity_performance.unknown_activity");
        public static readonly AuthoredId ReasonNotActive = new AuthoredId("command.activity_performance.not_active");
        public static readonly AuthoredId ReasonAlreadyResolved = new AuthoredId("command.activity_performance.already_resolved");
        public static readonly AuthoredId ReasonNotInteractive = new AuthoredId("command.activity_performance.not_interactive");
        public static readonly AuthoredId ReasonNotPlayerProvided = new AuthoredId("command.activity_performance.not_player_provided");

        private readonly ActivityResolutionRegistry _resolution;

        public SubmitActivityPerformanceHandler(ActivityResolutionRegistry resolution)
        {
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        }

        public override Result Handle(SubmitActivityPerformanceCommand command, CommandContext context)
        {
            if (!context.World.Activities.TryGet(command.ActivityInstanceId, out ActivityInstance activity))
            {
                return Result.Fail(ReasonUnknownActivity, command.ActivityInstanceId.ToString());
            }

            if (activity.Status != ActivityStatus.Active)
            {
                return Result.Fail(ReasonNotActive, activity.Status.ToString());
            }

            if (activity.AcceptedResult.HasValue)
            {
                return Result.Fail(ReasonAlreadyResolved, command.ActivityInstanceId.ToString());
            }

            if (command.Result.Source != OutcomeSource.PlayerProvided)
            {
                return Result.Fail(ReasonNotPlayerProvided, "Interactive submissions must be marked PlayerProvided so traces stay honest (§53).");
            }

            if (_resolution.TryGetStrategy(activity.DefinitionId, out IActivityResolutionStrategy strategy) &&
                !strategy.SupportsInteractiveResolution)
            {
                return Result.Fail(ReasonNotInteractive, activity.DefinitionId.ToString());
            }

            _resolution.AcceptResult(context.World, activity, command.Result, context.Simulation);

            if (context.Simulation.Trace.IsEnabled)
            {
                context.Simulation.Trace.Record(
                    "command",
                    $"cmd #{context.CommandSequence} SubmitActivityPerformance activity {activity.Id} outcome {command.Result.Grade} source PlayerProvided");
            }

            return Result.Ok();
        }
    }

    /// <summary>Adds a location to the containment hierarchy (§27).</summary>
    public sealed class BuildLocationHandler : CommandHandler<BuildLocationCommand, Result<LocationId>>
    {
        public static readonly AuthoredId ReasonUnknownParent = new AuthoredId("command.build_location.unknown_parent");

        public override Result<LocationId> Handle(BuildLocationCommand command, CommandContext context)
        {
            if (command.ParentLocationId.IsSet && !context.World.Locations.Nodes.Contains(command.ParentLocationId))
            {
                return Result<LocationId>.Fail(ReasonUnknownParent, command.ParentLocationId.ToString());
            }

            var node = new LocationNode(
                context.World.RuntimeIds.Locations.Next(),
                command.ParentLocationId,
                command.LocationKindId,
                command.DisplayName,
                command.Occupiable);

            context.World.Locations.Add(node);
            return Result<LocationId>.Ok(node.Id);
        }
    }

    /// <summary>Sets a character's attention policy (§20).</summary>
    public sealed class SetAttentionPolicyHandler : CommandHandler<SetAttentionPolicyCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownCharacter = new AuthoredId("command.attention.unknown_character");

        public override Result Handle(SetAttentionPolicyCommand command, CommandContext context)
        {
            if (!context.World.Characters.Contains(command.CharacterId))
            {
                return Result.Fail(ReasonUnknownCharacter, command.CharacterId.ToString());
            }

            context.World.Attention.SetPolicy(command.CharacterId, command.Policy);
            return Result.Ok();
        }
    }
}
