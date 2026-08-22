using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Characters
{
    /// <summary>
    /// One need's authoritative state: hunger, fatigue, loneliness (§10.1).
    /// <para>
    /// The value itself is never ticked — it is an <see cref="AnalyticalProgression"/> evaluated at
    /// whatever time someone asks (invariant 12). Hunger at 15:00 is arithmetic, not sixty events.
    /// </para>
    /// <para>
    /// <see cref="PendingThresholdEventId"/> carries the other half of the contract: a need whose
    /// threshold can change behaviour <b>must</b> keep a real scheduled crossing, and that event must
    /// be invalidated and recomputed whenever the rate changes (§10.2, invariants 13–14).
    /// </para>
    /// </summary>
    public readonly struct NeedState
    {
        public NeedState(
            AuthoredId needId,
            AnalyticalProgression progression,
            long behaviouralThreshold,
            ScheduledEventId pendingThresholdEventId = default)
        {
            NeedId = needId;
            Progression = progression;
            BehaviouralThreshold = behaviouralThreshold;
            PendingThresholdEventId = pendingThresholdEventId;
        }

        /// <summary>Authored need id, e.g. <c>need.social</c>.</summary>
        public AuthoredId NeedId { get; }

        public AnalyticalProgression Progression { get; }

        /// <summary>
        /// The next value that can change behaviour — the only kind of threshold worth scheduling
        /// ("not every numerical threshold imaginable", §10.2).
        /// </summary>
        public long BehaviouralThreshold { get; }

        /// <summary>The scheduled crossing event, if one is currently queued.</summary>
        public ScheduledEventId PendingThresholdEventId { get; }

        public long ValueAt(SimTime at) => Progression.ValueAt(at);

        /// <summary>The aspect-scoped revision key protecting this need's schedule (§11.2.1).</summary>
        public RevisionKey RevisionKeyFor(CharacterId character) =>
            new RevisionKey(character.ToRef(), RevisionAspects.Scoped(RevisionAspects.Need, NeedId));

        public NeedState WithProgression(AnalyticalProgression progression) =>
            new NeedState(NeedId, progression, BehaviouralThreshold, PendingThresholdEventId);

        public NeedState WithThreshold(long behaviouralThreshold) =>
            new NeedState(NeedId, Progression, behaviouralThreshold, PendingThresholdEventId);

        public NeedState WithPendingThresholdEvent(ScheduledEventId eventId) =>
            new NeedState(NeedId, Progression, BehaviouralThreshold, eventId);

        public override string ToString() => $"{NeedId} {Progression}";
    }
}
