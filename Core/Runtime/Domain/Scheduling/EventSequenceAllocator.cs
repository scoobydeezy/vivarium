using System;

namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// Monotonic scheduler-local tie-break counter (§11, §34).
    /// <para>
    /// Deliberately <b>not</b> the Application's <c>CommandSequence</c>: this orders same-instant
    /// scheduled work, that one orders external command ingress. Different scope, different lifetime,
    /// separate persistence.
    /// </para>
    /// <para>
    /// Because newly scheduled same-time events always receive a later sequence than whatever caused
    /// them, causal order is preserved inside an instant and two events can never tie.
    /// </para>
    /// </summary>
    public sealed class EventSequenceAllocator
    {
        private long _issued;

        public EventSequenceAllocator(long alreadyIssued = 0)
        {
            if (alreadyIssued < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alreadyIssued));
            }

            _issued = alreadyIssued;
        }

        /// <summary>Counter value for persistence (<c>NextEventSequence</c> in the save, §38).</summary>
        public long Issued => _issued;

        public long Next() => ++_issued;
    }
}
