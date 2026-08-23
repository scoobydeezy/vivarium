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
                additionalParticipants: payload.AdditionalParticipants);

            world.Commitments.Add(commitment.Id, commitment);
            CommitmentScheduleChanges.Publish(world, commitment.CharacterId);
        }
    }
}
