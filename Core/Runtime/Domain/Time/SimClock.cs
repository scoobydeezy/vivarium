using System;

namespace Vivarium.Domain.Time
{
    /// <summary>
    /// The authoritative simulation clock (§8, §9).
    /// <para>
    /// Only the single simulation owner advances it (§13), and only ever forward. Nothing reads
    /// wall-clock time here: offline elapsed duration is computed outside the Domain from a persisted
    /// anchor (§21, §38).
    /// </para>
    /// </summary>
    public sealed class SimClock
    {
        public SimClock(SimTime start)
        {
            Now = start;
        }

        public SimTime Now { get; private set; }

        /// <summary>Moves the clock to <paramref name="time"/>. Never backwards.</summary>
        public void AdvanceTo(SimTime time)
        {
            if (time < Now)
            {
                throw new InvalidOperationException($"Simulation time cannot move backwards ({Now} -> {time}).");
            }

            Now = time;
        }

        public void Advance(SimDuration duration)
        {
            if (duration.IsNegative)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Simulation time cannot move backwards.");
            }

            Now = Now.Plus(duration);
        }

        public override string ToString() => Now.ToString();
    }
}
