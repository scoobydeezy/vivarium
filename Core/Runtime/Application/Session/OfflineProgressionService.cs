using System;
using Vivarium.Application.Persistence;
using Vivarium.Application.Ports;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Session
{
    /// <summary>
    /// How much simulated time real absence buys, and how much of it is honoured (§21).
    /// </summary>
    public sealed class OfflineProgressionPolicy
    {
        public OfflineProgressionPolicy(long simMinutesPerRealMinute = 1, long maxCatchUpMinutes = 60 * 24 * 7)
        {
            if (simMinutesPerRealMinute < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(simMinutesPerRealMinute));
            }

            SimMinutesPerRealMinute = simMinutesPerRealMinute;
            MaxCatchUpMinutes = maxCatchUpMinutes;
        }

        public long SimMinutesPerRealMinute { get; }

        /// <summary>
        /// Ceiling on a single catch-up. A player returning after a year should not trigger an unbounded
        /// simulation run before the game becomes interactive.
        /// </summary>
        public long MaxCatchUpMinutes { get; }
    }

    /// <summary>
    /// Computes offline elapsed duration outside the Domain (§21, §38, invariant 32).
    /// <para>
    /// The whole point of the persisted anchor is that <b>no Domain rule ever reads the wall clock</b>.
    /// Real elapsed time is turned into a <see cref="SimDuration"/> here, clamped by policy, and handed
    /// to the runner as an explicit <c>OfflineCatchUp</c> advance.
    /// </para>
    /// </summary>
    public sealed class OfflineProgressionService
    {
        private const long TicksPerMinute = 600000000L;

        private readonly IRealWorldClock _clock;
        private readonly OfflineProgressionPolicy _policy;

        public OfflineProgressionService(IRealWorldClock clock, OfflineProgressionPolicy policy = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _policy = policy ?? new OfflineProgressionPolicy();
        }

        /// <summary>
        /// Elapsed simulation duration implied by a save's anchor, clamped by policy.
        /// Returns <see cref="SimDuration.Zero"/> for a save with no usable anchor.
        /// </summary>
        public SimDuration ElapsedSince(SaveGameData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (save.SavedAtRealTimeUtcTicks <= 0)
            {
                return SimDuration.Zero;
            }

            long elapsedTicks = _clock.UtcNowTicks - save.SavedAtRealTimeUtcTicks;
            if (elapsedTicks <= 0)
            {
                // Clock moved backwards — a timezone change, a manual adjustment. Grant nothing rather
                // than running the world backwards, which the clock forbids anyway (§9).
                return SimDuration.Zero;
            }

            long realMinutes = elapsedTicks / TicksPerMinute;
            long simMinutes = realMinutes * _policy.SimMinutesPerRealMinute;

            if (simMinutes > _policy.MaxCatchUpMinutes)
            {
                simMinutes = _policy.MaxCatchUpMinutes;
            }

            return SimDuration.FromMinutes(simMinutes);
        }
    }
}
