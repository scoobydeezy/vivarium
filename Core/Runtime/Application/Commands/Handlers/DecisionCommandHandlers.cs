using System;
using System.Collections.Generic;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Commands.Handlers
{
    /// <summary>
    /// Applies a player intervention to an open decision (§19).
    /// <para>
    /// Calls <see cref="DecisionInterventionRules.Evaluate"/> — the <b>same</b> evaluation the UI uses
    /// to decide whether the control is enabled. There is no second copy of the rule here, which is the
    /// whole point of invariant 57.
    /// </para>
    /// </summary>
    public sealed class ApplyDecisionInterventionHandler : CommandHandler<ApplyDecisionInterventionCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownDecision = new AuthoredId("command.intervention.unknown_decision");
        public static readonly AuthoredId ReasonUnknownIntervention = new AuthoredId("command.intervention.unknown_definition");

        private readonly IReadOnlyDictionary<AuthoredId, InterventionDefinition> _interventions;
        private readonly DecisionResolutionService _resolution;

        public ApplyDecisionInterventionHandler(
            IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions,
            DecisionResolutionService resolution)
        {
            _interventions = interventions ?? throw new ArgumentNullException(nameof(interventions));
            _resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        }

        public override Result Handle(ApplyDecisionInterventionCommand command, CommandContext context)
        {
            if (!context.World.Decisions.TryGet(command.DecisionId, out Decision decision))
            {
                return Result.Fail(ReasonUnknownDecision, command.DecisionId.ToString());
            }

            if (!_interventions.TryGetValue(command.InterventionDefinitionId, out InterventionDefinition intervention))
            {
                return Result.Fail(ReasonUnknownIntervention, command.InterventionDefinitionId.ToString());
            }

            Result eligibility = DecisionInterventionRules.Evaluate(
                decision,
                intervention,
                command.TargetInfluenceId,
                context.World.Nudges,
                context.World.InterventionResources);
            if (eligibility.IsFailure)
            {
                return eligibility;
            }

            if (intervention.ResourceKind == InterventionResourceKind.Nudge &&
                !context.World.Nudges.TrySpend(intervention.Cost))
            {
                throw new InvalidOperationException("Authoritative Nudge eligibility and spend diverged.");
            }
            if ((intervention.ResourceKind == InterventionResourceKind.ReRoll ||
                 intervention.ResourceKind == InterventionResourceKind.ReplacementDie) &&
                !context.World.InterventionResources.TrySpend(intervention.ResourceKind, intervention.Cost))
                throw new InvalidOperationException("Authoritative intervention eligibility and spend diverged.");

            InfluenceRoll rerolled = default;
            if (intervention.Kind == InterventionKind.Reroll)
                rerolled = _resolution.Reroll(decision, command.TargetInfluenceId, context.Simulation);

            DecisionInterventionRules.Apply(decision, intervention, command.TargetInfluenceId, context.CommandSequence);
            if (intervention.Kind == InterventionKind.Reroll)
                decision.PendingResolution.Replace(rerolled);
            if (intervention.ResourceKind == InterventionResourceKind.Nudge && intervention.Cost > 0)
            {
                context.World.Publish(new NudgeBalanceChangedEvent(
                    NudgeBalanceChangeKind.Spent,
                    intervention.Cost,
                    intervention.Cost,
                    context.World.Nudges.Balance,
                    decision.Id,
                    decision.CharacterId,
                    intervention.Id));
            }
            context.World.BumpRevision(decision.InfluenceRevisionKey);
            context.World.Publish(new DecisionInfluencesChangedEvent(decision.Id, decision.InfluenceRevision));
            context.World.Publish(new DecisionInterventionAppliedEvent(
                decision.Id,
                decision.CharacterId,
                intervention.Id,
                command.TargetInfluenceId));

            if (context.Simulation.Trace.IsEnabled)
            {
                context.Simulation.Trace.Record(
                    "command",
                    $"cmd #{context.CommandSequence} ApplyIntervention {intervention.Id} → decision {decision.Id} influence {command.TargetInfluenceId}");
            }

            return Result.Ok();
        }
    }

    public sealed class DecisionResolutionWindowPolicy
    {
        public DecisionResolutionWindowPolicy(SimDuration duration)
        {
            if (duration.TotalMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            Duration = duration;
        }
        public SimDuration Duration { get; }
    }

    public sealed class BeginDecisionResolutionHandler : CommandHandler<BeginDecisionResolutionCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownDecision = new AuthoredId("command.decision_resolution.unknown_decision");
        public static readonly AuthoredId ReasonNotHeld = new AuthoredId("command.decision_resolution.not_held");
        public static readonly AuthoredId ReasonUnavailable = new AuthoredId("command.decision_resolution.unavailable");
        private readonly DecisionResolutionService _resolution;
        private readonly DecisionResolutionWindowPolicy _window;
        public BeginDecisionResolutionHandler(DecisionResolutionService resolution, DecisionResolutionWindowPolicy window)
        { _resolution = resolution; _window = window; }
        public override Result Handle(BeginDecisionResolutionCommand command, CommandContext context)
        {
            if (!context.World.Decisions.TryGet(command.DecisionId, out Decision decision))
                return Result.Fail(ReasonUnknownDecision, command.DecisionId.ToString());
            if (!decision.IsActive || decision.IsAwaitingCommit || !context.Simulation.AllowsHeldDecisions)
                return Result.Fail(ReasonUnavailable, decision.Status.ToString());
            if (!context.World.Attention.IsHeld(decision.Id))
                return Result.Fail(ReasonNotHeld, decision.Id.ToString());

            SimTime expiresAt = context.World.Clock.Now + _window.Duration;
            if (decision.CommitmentConflictKey != null && expiresAt > decision.LatestResolutionAt)
                expiresAt = decision.LatestResolutionAt;
            ScheduledEvent scheduled = context.World.Scheduler.Schedule(expiresAt, SchedulePhase.Decision,
                ScheduledEventTypes.DecisionPendingCommit,
                new DecisionPendingCommitPayload(decision.Id, decision.CharacterId));
            decision.BeginPendingResolution(new PendingDecisionResolution(
                context.World.Clock.Now, expiresAt, scheduled.Id, _resolution.ProduceRolls(decision, context.Simulation)));
            context.World.DecisionDependencies.Unregister(decision.Id);
            decision.SetPendingResolveEvent(scheduled.Id);
            return Result.Ok();
        }
    }

    public sealed class CommitDecisionResolutionHandler : CommandHandler<CommitDecisionResolutionCommand, Result>
    {
        public static readonly AuthoredId ReasonNotPending = new AuthoredId("command.decision_resolution.not_pending");
        private readonly DecisionResolutionService _resolution;
        public CommitDecisionResolutionHandler(DecisionResolutionService resolution) => _resolution = resolution;
        public override Result Handle(CommitDecisionResolutionCommand command, CommandContext context)
        {
            if (!context.World.Decisions.TryGet(command.DecisionId, out Decision decision) || !decision.IsAwaitingCommit)
                return Result.Fail(ReasonNotPending, command.DecisionId.ToString());
            context.World.Scheduler.Cancel(decision.PendingResolution.ExpiryEventId);
            DecisionResolution result = _resolution.Complete(
                decision, decision.PendingResolution.AcceptedRolls, context.World.Clock.Now,
                decision.PendingResolution.SupersededRolls);
            DecisionResolutionCommitter.Commit(context.World, decision, result, context.Simulation);
            return Result.Ok();
        }
    }

    /// <summary>
    /// Holds a decision so the player can inspect or intervene before it resolves (§20).
    /// <para>
    /// Enforces the bound: held decisions never grow without limit, and the per-character cap counts
    /// every one of that character's concurrent decisions, not one per decision (§17.1, invariant 33).
    /// </para>
    /// </summary>
    public sealed class HoldDecisionHandler : CommandHandler<HoldDecisionCommand, Result>
    {
        public static readonly AuthoredId ReasonUnknownDecision = new AuthoredId("command.hold.unknown_decision");
        public static readonly AuthoredId ReasonNotActive = new AuthoredId("command.hold.decision_not_active");
        public static readonly AuthoredId ReasonGlobalCapacity = new AuthoredId("command.hold.global_capacity");
        public static readonly AuthoredId ReasonCharacterCapacity = new AuthoredId("command.hold.character_capacity");
        public static readonly AuthoredId ReasonModeDisallows = new AuthoredId("command.hold.mode_disallows");

        private readonly DecisionHoldPolicy _policy;

        public HoldDecisionHandler(DecisionHoldPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public override Result Handle(HoldDecisionCommand command, CommandContext context)
        {
            if (!context.World.Decisions.TryGet(command.DecisionId, out Decision decision))
            {
                return Result.Fail(ReasonUnknownDecision, command.DecisionId.ToString());
            }

            if (!decision.IsActive)
            {
                return Result.Fail(ReasonNotActive, decision.Status.ToString());
            }

            if (!context.Simulation.AllowsHeldDecisions)
            {
                return Result.Fail(ReasonModeDisallows, context.Simulation.Mode.ToString());
            }

            if (context.World.Attention.IsHeld(decision.Id))
            {
                return Result.Ok();
            }

            if (_policy.GlobalCapacityExceeded(context.World.Attention.HeldCount + 1))
            {
                return Result.Fail(ReasonGlobalCapacity, $"limit {_policy.MaxGlobalHeld}");
            }

            int heldForCharacter = 1;
            foreach (DecisionId heldId in context.World.Attention.HeldDecisions)
            {
                if (context.World.Decisions.TryGet(heldId, out Decision held) && held.CharacterId == decision.CharacterId)
                {
                    heldForCharacter++;
                }
            }

            if (_policy.CharacterCapacityExceeded(heldForCharacter))
            {
                return Result.Fail(ReasonCharacterCapacity, $"limit {_policy.MaxHeldPerCharacter} for {decision.CharacterId}");
            }

            context.World.Attention.Hold(decision.Id);
            context.World.Attention.SetPolicy(decision.Id, AttentionPolicy.Hold);
            return Result.Ok();
        }
    }

    /// <summary>Releases a held decision (§20).</summary>
    public sealed class ReleaseDecisionHandler : CommandHandler<ReleaseDecisionCommand, Result>
    {
        public static readonly AuthoredId ReasonNotHeld = new AuthoredId("command.release.not_held");

        public override Result Handle(ReleaseDecisionCommand command, CommandContext context)
        {
            if (!context.World.Attention.Release(command.DecisionId))
            {
                return Result.Fail(ReasonNotHeld, command.DecisionId.ToString());
            }

            context.World.Attention.SetPolicy(command.DecisionId, AttentionPolicy.Normal);

            // Re-arm resolution: a released decision resolves at its scheduled time, or immediately if
            // that time has already passed while it was held.
            if (context.World.Decisions.TryGet(command.DecisionId, out Decision decision) && decision.IsActive)
            {
                // A Held Decision may still own a hard-deadline event. Do not create a second generic
                // resolution event merely because the player released it before that deadline.
                if (decision.PendingResolveEventId.IsSet &&
                    context.World.Scheduler.Contains(decision.PendingResolveEventId))
                {
                    return Result.Ok();
                }
                Domain.Time.SimTime resolveAt = decision.ResolveAt < context.World.Clock.Now
                    ? context.World.Clock.Now
                    : decision.ResolveAt;

                Domain.Scheduling.ScheduledEvent scheduled = context.World.Scheduler.Schedule(
                    resolveAt,
                    Domain.Scheduling.SchedulePhase.Decision,
                    Domain.Activities.ScheduledEventTypes.DecisionResolve,
                    new DecisionResolvePayload(decision.Id, decision.CharacterId));

                decision.SetPendingResolveEvent(scheduled.Id);
            }

            return Result.Ok();
        }
    }
}
