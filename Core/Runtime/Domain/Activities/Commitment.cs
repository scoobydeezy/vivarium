using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>Lifecycle of a planned Commitment.</summary>
    public enum CommitmentStatus
    {
        /// <summary>Materialized into the planning horizon but not yet started.</summary>
        Planned = 0,

        /// <summary>Its Activity is currently running.</summary>
        Active = 1,

        Fulfilled = 2,

        /// <summary>The window passed without the character starting it.</summary>
        Missed = 3,

        Cancelled = 4,
    }

    /// <summary>
    /// Something a character intends, is obliged, or has agreed to do (§29.3).
    /// <para>
    /// <b>Commitment is pre-execution planning intent. ScheduledEvent is concrete simulation
    /// execution.</b> Keeping them apart is what lets the planner notice that a work shift and a
    /// birthday party overlap <i>before</i> either becomes a real Activity transition (invariant 43).
    /// </para>
    /// <para>
    /// Authoritative save state, not a rebuildable index (§40).
    /// </para>
    /// </summary>
    public sealed class Commitment
    {
        private static readonly CharacterId[] NoParticipants = new CharacterId[0];

        public Commitment(
            CommitmentId id,
            CharacterId characterId,
            AuthoredId kind,
            SimTime earliestStart,
            SimTime latestStart,
            SimDuration expectedDuration,
            LocationId locationId,
            int priority,
            AuthoredId activityDefinitionId = default,
            EntityRef source = default,
            IReadOnlyList<CharacterId> additionalParticipants = null,
            AuthoredId sourceTemplateId = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A Commitment needs an allocated runtime id (§7).", nameof(id));
            }

            if (latestStart < earliestStart)
            {
                throw new ArgumentException("latestStart must not precede earliestStart.", nameof(latestStart));
            }

            Id = id;
            CharacterId = characterId;
            Kind = kind;
            EarliestStart = earliestStart;
            LatestStart = latestStart;
            ExpectedDuration = expectedDuration;
            LocationId = locationId;
            Priority = priority;
            ActivityDefinitionId = activityDefinitionId;
            Source = source;
            AdditionalParticipants = additionalParticipants ?? NoParticipants;
            SourceTemplateId = sourceTemplateId;
            Status = CommitmentStatus.Planned;
        }

        public CommitmentId Id { get; }

        public CharacterId CharacterId { get; }

        /// <summary>Authored kind, e.g. <c>commitment.work_shift</c> or <c>commitment.birthday_party</c>.</summary>
        public AuthoredId Kind { get; }

        /// <summary>Start of the scheduled window.</summary>
        public SimTime EarliestStart { get; }

        /// <summary>End of the scheduled window; past this the commitment is missed.</summary>
        public SimTime LatestStart { get; }

        public SimDuration ExpectedDuration { get; }

        /// <summary>Where it must happen. Drives departure planning (§29.5).</summary>
        public LocationId LocationId { get; }

        /// <summary>Obligation weight, used for conflict resolution. Higher wins.</summary>
        public int Priority { get; }

        /// <summary>
        /// The Activity this commitment becomes when it starts. The planner reads it to turn planning
        /// intent into a concrete transition (§29.5).
        /// </summary>
        public AuthoredId ActivityDefinitionId { get; }

        /// <summary>What obliges it — an employment, a household, an event. Weak reference (§7.1).</summary>
        public EntityRef Source { get; }

        /// <summary>Other participants, e.g. dinner with Darius.</summary>
        public IReadOnlyList<CharacterId> AdditionalParticipants { get; }

        /// <summary>
        /// The recurring template that materialized this occurrence, if any. Lets the planner recognise
        /// what it has already planned without re-deriving the whole calendar (§29.4).
        /// </summary>
        public AuthoredId SourceTemplateId { get; }

        public CommitmentStatus Status { get; private set; }

        public ActivityInstanceId FulfillingActivityId { get; private set; }

        public SimTime ExpectedEnd => EarliestStart.Plus(ExpectedDuration);

        /// <summary>Revision key protecting this character's planned schedule (§11.2.1).</summary>
        public RevisionKey ScheduleRevisionKey => new RevisionKey(CharacterId.ToRef(), RevisionAspects.Schedule);

        /// <summary>Whether two commitments contend for the same character's time.</summary>
        public bool OverlapsWindowOf(Commitment other) =>
            EarliestStart < other.ExpectedEnd && other.EarliestStart < ExpectedEnd;

        public void MarkActive(ActivityInstanceId activityId)
        {
            Status = CommitmentStatus.Active;
            FulfillingActivityId = activityId;
        }

        /// <summary>Restores saved lifecycle state (§38). Commitments are authoritative, not rebuilt (§40).</summary>
        public void RestoreStatus(CommitmentStatus status, ActivityInstanceId fulfillingActivityId)
        {
            Status = status;
            FulfillingActivityId = fulfillingActivityId;
        }

        public void MarkFulfilled() => Status = CommitmentStatus.Fulfilled;

        public void MarkMissed() => Status = CommitmentStatus.Missed;

        public void Cancel() => Status = CommitmentStatus.Cancelled;

        public override string ToString() => $"{Kind} for {CharacterId} at {EarliestStart} ({Status})";
    }
}
