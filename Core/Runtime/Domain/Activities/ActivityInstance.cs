using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>Lifecycle of an <see cref="ActivityInstance"/>.</summary>
    public enum ActivityStatus
    {
        Active = 0,

        /// <summary>Finished and its result accepted into the consequence pipeline.</summary>
        Completed = 1,

        /// <summary>Ended early — interrupted, superseded, or invalidated.</summary>
        Abandoned = 2,
    }

    /// <summary>
    /// What a character is actually doing — the authoritative answer to "what is Mina doing?" and,
    /// through <see cref="SpatialContext"/>, to "where is Mina?" (§29.1).
    /// <para>
    /// Every active character has exactly one of these as their primary Activity (invariant 39).
    /// Multitasking, if it ever arrives, is a modifier or subordinate interaction — never a second
    /// competing primary Activity (§29.1, §29.7).
    /// </para>
    /// <para>
    /// Progress and performance are <see cref="AnalyticalProgression"/> values, so a six-hour shift
    /// costs two events (start, complete) rather than 360 (§10.1).
    /// </para>
    /// </summary>
    public sealed class ActivityInstance
    {
        private readonly SortedDictionary<AuthoredId, long> _committedParameters = new SortedDictionary<AuthoredId, long>();
        private readonly List<ActivityContextModifier> _activeModifiers = new List<ActivityContextModifier>();

        public ActivityInstance(
            ActivityInstanceId id,
            CharacterId characterId,
            AuthoredId definitionId,
            SimTime startedAt,
            ActivitySpatialContext spatialContext,
            AnalyticalProgression progress,
            AnalyticalProgression performance,
            CommitmentId sourceCommitmentId = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An Activity needs an allocated runtime id (§7).", nameof(id));
            }

            Id = id;
            CharacterId = characterId;
            DefinitionId = definitionId;
            StartedAt = startedAt;
            SpatialContext = spatialContext;
            Progress = progress;
            Performance = performance;
            SourceCommitmentId = sourceCommitmentId;
            Status = ActivityStatus.Active;
        }

        public ActivityInstanceId Id { get; }

        public CharacterId CharacterId { get; }

        /// <summary>Authored activity definition, e.g. <c>activity.working</c> or <c>activity.traveling</c>.</summary>
        public AuthoredId DefinitionId { get; }

        public SimTime StartedAt { get; }

        public ActivitySpatialContext SpatialContext { get; private set; }

        /// <summary>Completion progress in basis points (0–10,000), derived analytically (§10.1).</summary>
        public AnalyticalProgression Progress { get; private set; }

        /// <summary>
        /// Accumulated performance. Separate from <see cref="Progress"/> so a context change can alter
        /// how <i>well</i> the work is going without altering when it ends (§29.7).
        /// </summary>
        public AnalyticalProgression Performance { get; private set; }

        /// <summary>The Commitment that planned this Activity, when there was one (§29.3).</summary>
        public CommitmentId SourceCommitmentId { get; }

        public ActivityStatus Status { get; private set; }

        /// <summary>The accepted normalized outcome, once resolved through either path (§29.6).</summary>
        public ActivityPerformanceResult? AcceptedResult { get; private set; }

        /// <summary>The scheduled completion event, kept so a rate change can invalidate it (§10.2).</summary>
        public ScheduledEventId PendingCompletionEventId { get; private set; }

        /// <summary>
        /// Definition-derived values snapshotted at construction (§42.1). Hot-reloading the definition
        /// must not retroactively change how this instance resolves.
        /// </summary>
        public IReadOnlyDictionary<AuthoredId, long> CommittedParameters => _committedParameters;

        /// <summary>Context modifiers currently applying, e.g. "hated boss present" (§29.7).</summary>
        public IReadOnlyList<ActivityContextModifier> ActiveModifiers => _activeModifiers;

        /// <summary>
        /// Revision key protecting this character's Activity aspect (§11.2.1). Deliberately addressed
        /// through the central <see cref="RevisionRegistry"/> rather than an int on this object, so a
        /// stale event and the state it depends on can never disagree about the counter.
        /// </summary>
        public RevisionKey ActivityRevisionKey => new RevisionKey(CharacterId.ToRef(), RevisionAspects.Activity);

        public void CommitParameter(AuthoredId key, long value) => _committedParameters[key] = value;

        public long CommittedParameterOr(AuthoredId key, long fallback) =>
            _committedParameters.TryGetValue(key, out long value) ? value : fallback;

        public int ProgressBasisPointsAt(SimTime at) => (int)IntegerMath.Clamp(Progress.ValueAt(at), 0, 10000);

        public bool IsCompleteAt(SimTime at) => ProgressBasisPointsAt(at) >= 10000;

        public void SetSpatialContext(ActivitySpatialContext context) => SpatialContext = context;

        public void SetPendingCompletionEvent(ScheduledEventId eventId) => PendingCompletionEventId = eventId;

        /// <summary>
        /// Applies a context change at the moment it happens: materialize what has accumulated so far,
        /// then continue at the new rate (§29.7).
        /// <para>
        /// This is why the boss's twenty minutes in the room matter for twenty minutes instead of being
        /// ignored because he had left by the time the shift ended. The caller still owes the rest of
        /// §10.2: bump the Activity revision and recompute the completion event.
        /// </para>
        /// </summary>
        public void ApplyContextChange(SimTime at, long performanceRateNumerator, long performanceRateDenominator = 1)
        {
            Performance = Performance.WithRate(at, performanceRateNumerator, performanceRateDenominator);
        }

        public void AddModifier(ActivityContextModifier modifier) => _activeModifiers.Add(modifier);

        public bool HasModifier(AuthoredId modifierId)
        {
            for (int i = 0; i < _activeModifiers.Count; i++)
            {
                if (_activeModifiers[i].ModifierId == modifierId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RemoveModifier(AuthoredId modifierId)
        {
            for (int i = 0; i < _activeModifiers.Count; i++)
            {
                if (_activeModifiers[i].ModifierId.Equals(modifierId))
                {
                    _activeModifiers.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Adjusts the completion schedule after a rate change (§10.2).</summary>
        public void RebaseProgress(SimTime at, long rateNumerator, long rateDenominator = 1)
        {
            Progress = Progress.WithRate(at, rateNumerator, rateDenominator);
        }

        /// <summary>
        /// Accepts the normalized outcome — from automatic resolution or from a player-provided
        /// interactive result. Both paths land here and feed the same consequence pipeline (§29.6).
        /// </summary>
        public void Complete(ActivityPerformanceResult result, SimTime at)
        {
            if (Status != ActivityStatus.Active)
            {
                throw new InvalidOperationException($"{Id} is already {Status} and cannot accept another result.");
            }

            AcceptedResult = result;
            Status = ActivityStatus.Completed;
            Progress = Progress.Reanchored(at);
            Performance = Performance.Reanchored(at);
        }

        /// <summary>
        /// Restores saved lifecycle state without re-running the consequence pipeline (§38).
        /// A Traveling Activity's committed route parameters must round-trip exactly (§40).
        /// </summary>
        public void RestoreStatus(ActivityStatus status, ActivityPerformanceResult? acceptedResult)
        {
            Status = status;
            AcceptedResult = acceptedResult;
        }

        public void Abandon(SimTime at)
        {
            Status = ActivityStatus.Abandoned;
            Progress = Progress.Reanchored(at);
            Performance = Performance.Reanchored(at);
        }

        public override string ToString() => $"{DefinitionId} by {CharacterId} {SpatialContext} ({Status})";
    }
}
