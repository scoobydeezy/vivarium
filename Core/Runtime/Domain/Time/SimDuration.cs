using System;

namespace Vivarium.Domain.Time
{
    /// <summary>A signed span of simulation minutes (§9).</summary>
    public readonly struct SimDuration : IEquatable<SimDuration>, IComparable<SimDuration>
    {
        public static readonly SimDuration Zero = new SimDuration(0);

        public SimDuration(long totalMinutes)
        {
            TotalMinutes = totalMinutes;
        }

        public long TotalMinutes { get; }

        public bool IsZero => TotalMinutes == 0;

        public bool IsNegative => TotalMinutes < 0;

        public static SimDuration FromMinutes(long minutes) => new SimDuration(minutes);

        public static SimDuration FromHours(long hours) => new SimDuration(hours * SimTime.MinutesPerHour);

        public static SimDuration FromDays(long days) => new SimDuration(days * SimTime.MinutesPerDay);

        public SimDuration Plus(SimDuration other) => new SimDuration(TotalMinutes + other.TotalMinutes);

        public SimDuration Minus(SimDuration other) => new SimDuration(TotalMinutes - other.TotalMinutes);

        public SimDuration Scaled(long factor) => new SimDuration(TotalMinutes * factor);

        public SimDuration Clamped(SimDuration min, SimDuration max) =>
            new SimDuration(Math.Min(Math.Max(TotalMinutes, min.TotalMinutes), max.TotalMinutes));

        public bool Equals(SimDuration other) => TotalMinutes == other.TotalMinutes;

        public override bool Equals(object obj) => obj is SimDuration other && Equals(other);

        public override int GetHashCode() => TotalMinutes.GetHashCode();

        public int CompareTo(SimDuration other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public override string ToString()
        {
            long abs = Math.Abs(TotalMinutes);
            string sign = TotalMinutes < 0 ? "-" : string.Empty;
            if (abs < SimTime.MinutesPerHour)
            {
                return $"{sign}{abs}m";
            }

            if (abs < SimTime.MinutesPerDay)
            {
                return $"{sign}{abs / SimTime.MinutesPerHour}h{abs % SimTime.MinutesPerHour:00}m";
            }

            return $"{sign}{abs / SimTime.MinutesPerDay}d{(abs % SimTime.MinutesPerDay) / SimTime.MinutesPerHour}h";
        }

        public static SimDuration operator +(SimDuration a, SimDuration b) => a.Plus(b);

        public static SimDuration operator -(SimDuration a, SimDuration b) => a.Minus(b);

        public static SimDuration operator *(SimDuration a, long factor) => a.Scaled(factor);

        public static bool operator ==(SimDuration a, SimDuration b) => a.TotalMinutes == b.TotalMinutes;

        public static bool operator !=(SimDuration a, SimDuration b) => a.TotalMinutes != b.TotalMinutes;

        public static bool operator <(SimDuration a, SimDuration b) => a.TotalMinutes < b.TotalMinutes;

        public static bool operator >(SimDuration a, SimDuration b) => a.TotalMinutes > b.TotalMinutes;

        public static bool operator <=(SimDuration a, SimDuration b) => a.TotalMinutes <= b.TotalMinutes;

        public static bool operator >=(SimDuration a, SimDuration b) => a.TotalMinutes >= b.TotalMinutes;
    }
}
