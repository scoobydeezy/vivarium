using Vivarium.Domain.Common;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// A revision a scheduled event depends on (§11.2).
    /// <para>
    /// <c>MinaLeavesWork</c> depends on <c>Employment#72</c> and <c>Schedule:Mina</c> — and
    /// deliberately not on her hunger. Recording only real dependencies is what keeps routine state
    /// updates from becoming invalidation storms (§11.2.1).
    /// </para>
    /// <para>
    /// A mismatch means the event is <i>likely</i> obsolete. Revision checks are an optimization;
    /// semantic validation in the handler is authoritative.
    /// </para>
    /// </summary>
    public readonly struct EventDependency
    {
        public EventDependency(RevisionKey key, int expectedRevision)
        {
            Key = key;
            ExpectedRevision = expectedRevision;
        }

        public RevisionKey Key { get; }

        public int ExpectedRevision { get; }

        /// <summary>Captures the dependency at its current revision.</summary>
        public static EventDependency Capture(RevisionRegistry revisions, RevisionKey key) =>
            new EventDependency(key, revisions.Get(key));

        public static EventDependency Capture(RevisionRegistry revisions, EntityRef subject, AuthoredId aspect) =>
            Capture(revisions, new RevisionKey(subject, aspect));

        public bool IsSatisfiedBy(RevisionRegistry revisions) => revisions.Matches(Key, ExpectedRevision);

        public override string ToString() => $"{Key} rev {ExpectedRevision}";
    }
}
