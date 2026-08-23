using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    public enum CommitmentOutcomeKind
    {
        Fulfilled = 0,
        Relinquished = 1,
        Missed = 2,
        Cancelled = 3,
    }

    public enum CommitmentOutcomeCauseKind
    {
        None = 0,
        ConflictResolution = 1,
        WindowExpired = 2,
        ExternalCancellation = 3,
        ExplicitCancellation = 4,
    }

    public sealed class CommitmentOutcomeCause
    {
        public static readonly CommitmentOutcomeCause Fulfilled = new CommitmentOutcomeCause(CommitmentOutcomeCauseKind.None);

        public CommitmentOutcomeCause(
            CommitmentOutcomeCauseKind kind,
            CharacterId initiatingActor = default,
            CharacterId responsibleActor = default,
            DecisionId sourceDecisionId = default)
        {
            Kind = kind;
            InitiatingActor = initiatingActor;
            ResponsibleActor = responsibleActor;
            SourceDecisionId = sourceDecisionId;
        }

        public CommitmentOutcomeCauseKind Kind { get; }
        public CharacterId InitiatingActor { get; }
        public CharacterId ResponsibleActor { get; }
        public DecisionId SourceDecisionId { get; }
    }

    /// <summary>Immutable historical fact produced by one terminal Commitment transition.</summary>
    public sealed class CommitmentOutcome
    {
        public CommitmentOutcome(
            CommitmentOutcomeId id,
            CommitmentId commitmentId,
            CommitmentStatus previousStatus,
            CommitmentOutcomeKind outcome,
            SimTime occurredAt,
            CommitmentOutcomeCause cause)
        {
            if (!id.IsSet) throw new ArgumentException("A Commitment outcome needs an allocated id.", nameof(id));
            Id = id;
            CommitmentId = commitmentId;
            PreviousStatus = previousStatus;
            Outcome = outcome;
            OccurredAt = occurredAt;
            Cause = cause ?? throw new ArgumentNullException(nameof(cause));
        }

        public CommitmentOutcomeId Id { get; }
        public CommitmentId CommitmentId { get; }
        public CommitmentStatus PreviousStatus { get; }
        public CommitmentOutcomeKind Outcome { get; }
        public SimTime OccurredAt { get; }
        public CommitmentOutcomeCause Cause { get; }
        public CommitmentStatus NewStatus => (CommitmentStatus)(Outcome == CommitmentOutcomeKind.Fulfilled ? 2 :
            Outcome == CommitmentOutcomeKind.Missed ? 3 : Outcome == CommitmentOutcomeKind.Cancelled ? 4 : 5);
    }

    /// <summary>Session-retained ephemeral outcome records plus the same-cascade idempotency guard.</summary>
    public sealed class CommitmentOutcomeLedger
    {
        private readonly List<CommitmentOutcome> _outcomes = new List<CommitmentOutcome>();
        private readonly HashSet<CommitmentOutcomeId> _accountabilityApplied = new HashSet<CommitmentOutcomeId>();
        public IReadOnlyList<CommitmentOutcome> All => _outcomes;
        public void Record(CommitmentOutcome outcome) => _outcomes.Add(outcome);
        public bool TryMarkAccountabilityApplied(CommitmentOutcomeId id) => _accountabilityApplied.Add(id);
        public int PruneBefore(SimTime cutoff)
        {
            int removed = 0;
            for (int i = _outcomes.Count - 1; i >= 0; i--)
                if (_outcomes[i].OccurredAt < cutoff)
                {
                    _accountabilityApplied.Remove(_outcomes[i].Id);
                    _outcomes.RemoveAt(i);
                    removed++;
                }
            return removed;
        }
    }

    /// <summary>Sole runtime authority for Commitment status mutation.</summary>
    public sealed class CommitmentLifecycleService
    {
        public void Start(WorldState world, Commitment commitment, ActivityInstanceId activityId)
        {
            if (world == null || commitment == null) throw new ArgumentNullException("World and Commitment are required.");
            if (!activityId.IsSet) throw new ArgumentException("An active Commitment needs its fulfilling Activity id.", nameof(activityId));
            if (commitment.Status != CommitmentStatus.Planned)
                throw new InvalidOperationException($"Cannot start {commitment.Id} from {commitment.Status}.");
            commitment.TransitionToActive(activityId);
            world.Publish(new CommitmentStatusChangedEvent(
                commitment.Id, commitment.CharacterId, CommitmentStatus.Planned, CommitmentStatus.Active));
        }

        public CommitmentOutcome Fulfill(WorldState world, Commitment commitment) =>
            Transition(world, commitment, CommitmentOutcomeKind.Fulfilled, CommitmentOutcomeCause.Fulfilled);

        public CommitmentOutcome Relinquish(WorldState world, Commitment commitment, DecisionId sourceDecisionId) =>
            Transition(world, commitment, CommitmentOutcomeKind.Relinquished,
                new CommitmentOutcomeCause(CommitmentOutcomeCauseKind.ConflictResolution,
                    commitment.CharacterId, commitment.CharacterId, sourceDecisionId));

        public CommitmentOutcome MissWindow(WorldState world, Commitment commitment) =>
            Transition(world, commitment, CommitmentOutcomeKind.Missed,
                new CommitmentOutcomeCause(
                    CommitmentOutcomeCauseKind.WindowExpired,
                    responsibleActor: commitment.CharacterId));

        public CommitmentOutcome Cancel(
            WorldState world,
            Commitment commitment,
            CommitmentOutcomeCauseKind cause,
            CharacterId initiatingActor = default)
        {
            if (cause != CommitmentOutcomeCauseKind.ExternalCancellation &&
                cause != CommitmentOutcomeCauseKind.ExplicitCancellation)
                throw new ArgumentException("Cancellation requires an external or explicit cause.", nameof(cause));
            CharacterId responsible = cause == CommitmentOutcomeCauseKind.ExplicitCancellation
                ? commitment.CharacterId
                : CharacterId.None;
            return Transition(world, commitment, CommitmentOutcomeKind.Cancelled,
                new CommitmentOutcomeCause(cause, initiatingActor, responsible));
        }

        private static CommitmentOutcome Transition(
            WorldState world,
            Commitment commitment,
            CommitmentOutcomeKind outcomeKind,
            CommitmentOutcomeCause cause)
        {
            if (world == null || commitment == null) throw new ArgumentNullException("World and Commitment are required.");
            Validate(commitment.Status, outcomeKind, cause.Kind);
            CommitmentStatus previous = commitment.Status;
            var outcome = new CommitmentOutcome(
                world.RuntimeIds.CommitmentOutcomes.Next(), commitment.Id, previous,
                outcomeKind, world.Clock.Now, cause);
            commitment.TransitionTo(outcome.NewStatus);
            world.CommitmentOutcomes.Record(outcome);
            world.Publish(new CommitmentOutcomeOccurredEvent(commitment.CharacterId, outcome));
            return outcome;
        }

        private static void Validate(
            CommitmentStatus status,
            CommitmentOutcomeKind outcome,
            CommitmentOutcomeCauseKind cause)
        {
            bool statusValid = outcome == CommitmentOutcomeKind.Fulfilled
                ? status == CommitmentStatus.Active
                : status == CommitmentStatus.Planned ||
                  (outcome == CommitmentOutcomeKind.Cancelled && status == CommitmentStatus.Active);
            if (!statusValid) throw new InvalidOperationException($"Cannot produce {outcome} from {status}.");
            bool pairValid = (outcome == CommitmentOutcomeKind.Fulfilled && cause == CommitmentOutcomeCauseKind.None) ||
                (outcome == CommitmentOutcomeKind.Relinquished && cause == CommitmentOutcomeCauseKind.ConflictResolution) ||
                (outcome == CommitmentOutcomeKind.Missed && cause == CommitmentOutcomeCauseKind.WindowExpired) ||
                (outcome == CommitmentOutcomeKind.Cancelled &&
                 (cause == CommitmentOutcomeCauseKind.ExternalCancellation || cause == CommitmentOutcomeCauseKind.ExplicitCancellation));
            if (!pairValid) throw new InvalidOperationException($"{outcome} cannot pair with {cause}.");
        }
    }
}
