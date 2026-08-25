using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Authored scheduled-event types. Persisted in saves, so these strings are schema (§39).
    /// </summary>
    public static class ScheduledEventTypes
    {
        public static readonly AuthoredId ActivityStart = new AuthoredId("event.activity.start");
        public static readonly AuthoredId ActivityComplete = new AuthoredId("event.activity.complete");
        public static readonly AuthoredId TravelArrival = new AuthoredId("event.travel.arrival");
        public static readonly AuthoredId NeedThreshold = new AuthoredId("event.need.threshold");
        public static readonly AuthoredId DecisionResolve = new AuthoredId("event.decision.resolve");
        public static readonly AuthoredId DecisionPendingCommit = new AuthoredId("event.decision.pending_commit");
        public static readonly AuthoredId AutoResolveCommitmentConflict = new AuthoredId("event.decision.commitment_conflict.auto_resolve");
        public static readonly AuthoredId CommitmentBecomesKnown = new AuthoredId("event.commitment.becomes_known");
        public static readonly AuthoredId CommitmentWindowExpired = new AuthoredId("event.commitment.window_expired");
        public static readonly AuthoredId InteractionOpportunity = new AuthoredId("event.social.interaction_opportunity");
    }

    /// <summary>
    /// Introduces a future obligation at the instant the character learns or accepts it. This keeps
    /// authored scenario timing separate from the commitment's own execution window.
    /// </summary>
    public sealed class CommitmentBecomesKnownPayload : IScheduledEventPayload
    {
        public CommitmentBecomesKnownPayload(
            CharacterId characterId,
            AuthoredId kind,
            SimTime earliestStart,
            SimTime latestStart,
            SimDuration expectedDuration,
            LocationId locationId,
            int priority,
            AuthoredId activityDefinitionId,
            IReadOnlyList<CharacterId> additionalParticipants = null,
            IReadOnlyList<StakeholderRef> stakeholders = null,
            CommitmentAccountabilityPolicy accountabilityPolicy = null)
        {
            CharacterId = characterId;
            Kind = kind;
            EarliestStart = earliestStart;
            LatestStart = latestStart;
            ExpectedDuration = expectedDuration;
            LocationId = locationId;
            Priority = priority;
            ActivityDefinitionId = activityDefinitionId;
            AdditionalParticipants = Copy(additionalParticipants);
            Stakeholders = stakeholders == null ? null : Copy(stakeholders);
            AccountabilityPolicy = accountabilityPolicy ?? CommitmentAccountabilityPolicy.None;
        }

        public CharacterId CharacterId { get; }
        public AuthoredId Kind { get; }
        public SimTime EarliestStart { get; }
        public SimTime LatestStart { get; }
        public SimDuration ExpectedDuration { get; }
        public LocationId LocationId { get; }
        public int Priority { get; }
        public AuthoredId ActivityDefinitionId { get; }
        public IReadOnlyList<CharacterId> AdditionalParticipants { get; }
        public IReadOnlyList<StakeholderRef> Stakeholders { get; }
        public CommitmentAccountabilityPolicy AccountabilityPolicy { get; }

        private static CharacterId[] Copy(IReadOnlyList<CharacterId> source)
        {
            if (source == null) return new CharacterId[0];
            var result = new CharacterId[source.Count];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }

        private static StakeholderRef[] Copy(IReadOnlyList<StakeholderRef> source)
        {
            var result = new StakeholderRef[source.Count];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            System.Array.Sort(result);
            return result;
        }
    }

    /// <summary>Begin the Activity that fulfils a planned Commitment (§29.5).</summary>
    public sealed class ActivityStartPayload : IScheduledEventPayload
    {
        public ActivityStartPayload(CharacterId characterId, CommitmentId commitmentId, AuthoredId activityDefinitionId, LocationId locationId)
        {
            CharacterId = characterId;
            CommitmentId = commitmentId;
            ActivityDefinitionId = activityDefinitionId;
            LocationId = locationId;
        }

        public CharacterId CharacterId { get; }

        public CommitmentId CommitmentId { get; }

        public AuthoredId ActivityDefinitionId { get; }

        public LocationId LocationId { get; }
    }

    /// <summary>
    /// An Activity's analytical progress reaches completion. Rescheduled whenever the progression rate
    /// changes (§10.2).
    /// </summary>
    public sealed class ActivityCompletionPayload : IScheduledEventPayload
    {
        public ActivityCompletionPayload(ActivityInstanceId activityInstanceId, CharacterId characterId)
        {
            ActivityInstanceId = activityInstanceId;
            CharacterId = characterId;
        }

        public ActivityInstanceId ActivityInstanceId { get; }

        public CharacterId CharacterId { get; }
    }

    /// <summary>A Traveling Activity reaches its destination (§29.2).</summary>
    public sealed class TravelArrivalPayload : IScheduledEventPayload
    {
        public TravelArrivalPayload(
            ActivityInstanceId activityInstanceId,
            CharacterId characterId,
            LocationId destinationLocationId,
            AuthoredId continuationActivityDefinitionId = default,
            SimDuration continuationDuration = default,
            IReadOnlyDictionary<AuthoredId, long> continuationCommittedParameters = null)
        {
            ActivityInstanceId = activityInstanceId;
            CharacterId = characterId;
            DestinationLocationId = destinationLocationId;
            ContinuationActivityDefinitionId = continuationActivityDefinitionId;
            ContinuationDuration = continuationDuration;
            ContinuationCommittedParameters = Copy(continuationCommittedParameters);
        }

        public ActivityInstanceId ActivityInstanceId { get; }

        public CharacterId CharacterId { get; }

        public LocationId DestinationLocationId { get; }

        /// <summary>Optional non-Commitment routine intent that begins when travel arrives.</summary>
        public AuthoredId ContinuationActivityDefinitionId { get; }

        public SimDuration ContinuationDuration { get; }

        /// <summary>Definition-derived continuation values carried through Travel and save/load.</summary>
        public IReadOnlyDictionary<AuthoredId, long> ContinuationCommittedParameters { get; }

        private static IReadOnlyDictionary<AuthoredId, long> Copy(
            IReadOnlyDictionary<AuthoredId, long> source)
        {
            var result = new SortedDictionary<AuthoredId, long>();
            if (source == null) return result;
            foreach (KeyValuePair<AuthoredId, long> pair in source) result.Add(pair.Key, pair.Value);
            return result;
        }
    }

    /// <summary>
    /// A need's analytical value crosses a behaviourally meaningful threshold (§10.2).
    /// Without this event nothing would notice the crossing at all.
    /// </summary>
    public sealed class NeedThresholdPayload : IScheduledEventPayload
    {
        public NeedThresholdPayload(CharacterId characterId, AuthoredId needId, long threshold)
        {
            CharacterId = characterId;
            NeedId = needId;
            Threshold = threshold;
        }

        public CharacterId CharacterId { get; }

        public AuthoredId NeedId { get; }

        public long Threshold { get; }
    }

    /// <summary>A commitment's start window elapsed without it beginning (§29.3).</summary>
    public sealed class CommitmentWindowExpiredPayload : IScheduledEventPayload
    {
        public CommitmentWindowExpiredPayload(CommitmentId commitmentId, CharacterId characterId)
        {
            CommitmentId = commitmentId;
            CharacterId = characterId;
        }

        public CommitmentId CommitmentId { get; }

        public CharacterId CharacterId { get; }
    }
}
