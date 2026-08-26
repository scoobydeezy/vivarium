using System;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Queries
{
    /// <summary>Projects world time plus the host's explicit execution state for the Unity HUD.</summary>
    public sealed class SimulationStatusProjector
    {
        public SimulationStatusView Project(
            WorldState world,
            SimulationMode mode,
            int speedPercent,
            long offlineElapsedMinutes = -1)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (speedPercent < 0) throw new ArgumentOutOfRangeException(nameof(speedPercent));

            long totalMinutes = world.Clock.Now.TotalMinutes;
            long day = totalMinutes / 1440;
            long hour = totalMinutes % 1440 / 60;
            long minute = totalMinutes % 60;
            bool paused = speedPercent == 0;
            bool offlineReturn = offlineElapsedMinutes >= 0;
            string status = offlineReturn
                ? "Returned from offline"
                : paused ? "Paused" : mode == SimulationMode.PlayerFastForward ? "Fast-forward" : "Live";
            string speed = paused ? "0x" : FormatSpeed(speedPercent);
            string offline = offlineReturn ? FormatDuration(offlineElapsedMinutes) : null;

            return new SimulationStatusView(
                $"Day {day} {hour:00}:{minute:00}",
                status,
                speed,
                paused,
                offlineReturn,
                offline);
        }

        private static string FormatSpeed(int speedPercent) =>
            speedPercent % 100 == 0 ? speedPercent / 100 + "x" : speedPercent / 100m + "x";

        private static string FormatDuration(long totalMinutes)
        {
            long days = totalMinutes / 1440;
            long hours = totalMinutes % 1440 / 60;
            long minutes = totalMinutes % 60;
            if (days > 0) return $"{days}d {hours}h {minutes}m elapsed";
            if (hours > 0) return $"{hours}h {minutes}m elapsed";
            return $"{minutes}m elapsed";
        }
    }
}
