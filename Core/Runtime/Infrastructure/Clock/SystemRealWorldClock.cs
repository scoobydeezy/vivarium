using System;
using Vivarium.Application.Ports;

namespace Vivarium.Infrastructure.Clock
{
    /// <summary>
    /// Wall clock backed by the system clock (§48).
    /// <para>
    /// The only place in the codebase that reads real time for simulation purposes. Domain rules reach
    /// it never; Application uses it solely to turn a save's anchor into an offline catch-up duration
    /// (invariant 32).
    /// </para>
    /// </summary>
    public sealed class SystemRealWorldClock : IRealWorldClock
    {
        public long UtcNowTicks => DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// Fixed clock for tests and reproducible headless runs.
    /// <para>
    /// Offline catch-up is a formally represented mode (§21), so testing it must not require actually
    /// waiting — or worse, depending on when the test happens to run.
    /// </para>
    /// </summary>
    public sealed class FixedRealWorldClock : IRealWorldClock
    {
        public FixedRealWorldClock(long utcNowTicks)
        {
            UtcNowTicks = utcNowTicks;
        }

        public long UtcNowTicks { get; set; }

        public void AdvanceMinutes(long minutes) => UtcNowTicks += minutes * 600000000L;
    }
}
