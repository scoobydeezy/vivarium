using Vivarium.Domain.Characters;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Activities
{
    /// <summary>Materializes authored future intent when it becomes known to the character.</summary>
    public sealed class CommitmentBecomesKnownHandler : ScheduledEventHandler<CommitmentBecomesKnownPayload>
    {
        public CommitmentBecomesKnownHandler() : base(ScheduledEventTypes.CommitmentBecomesKnown) { }

        protected override bool CanExecute(WorldState world, CommitmentBecomesKnownPayload payload) =>
            world.Characters.TryGet(payload.CharacterId, out Character character) &&
            character.IsActive &&
            payload.LatestStart >= payload.EarliestStart;

        protected override void Execute(
            WorldState world,
            CommitmentBecomesKnownPayload payload,
            SimulationContext context)
        {
            var commitment = new Commitment(
                world.RuntimeIds.Commitments.Next(),
                payload.CharacterId,
                payload.Kind,
                payload.EarliestStart,
                payload.LatestStart,
                payload.ExpectedDuration,
                payload.LocationId,
                payload.Priority,
                payload.ActivityDefinitionId,
                additionalParticipants: payload.AdditionalParticipants,
                stakeholders: payload.Stakeholders,
                accountabilityPolicy: payload.AccountabilityPolicy);

            world.Commitments.Add(commitment.Id, commitment);
            CommitmentScheduleChanges.Publish(world, commitment.CharacterId);
        }
    }

    /// <summary>Produces a Missed outcome only after the inclusive start window has elapsed.</summary>
    public sealed class CommitmentWindowExpiredHandler : ScheduledEventHandler<CommitmentWindowExpiredPayload>
    {
        private readonly CommitmentLifecycleService _commitments;

        public CommitmentWindowExpiredHandler(CommitmentLifecycleService commitments)
            : base(ScheduledEventTypes.CommitmentWindowExpired) => _commitments = commitments;

        protected override bool CanExecute(WorldState world, CommitmentWindowExpiredPayload payload) =>
            world.Commitments.TryGet(payload.CommitmentId, out Commitment commitment) &&
            commitment.CharacterId == payload.CharacterId &&
            commitment.Status == CommitmentStatus.Planned &&
            world.Clock.Now > commitment.LatestStart;

        protected override void Execute(
            WorldState world,
            CommitmentWindowExpiredPayload payload,
            SimulationContext context) =>
            _commitments.MissWindow(world, world.Commitments.Get(payload.CommitmentId));
    }
}
