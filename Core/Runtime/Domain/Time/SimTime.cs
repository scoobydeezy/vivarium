using System;

namespace Vivarium.Domain.Time
{
    /// <summary>
    /// An absolute point in simulation time, in whole simulation minutes since the world epoch (§9).
    /// <para>
    /// Integral by design: authoritative branching must never depend on float accumulation (§16), and
    /// nothing here relates to Unity frame time. If a finer resolution is ever needed, change
    /// <see cref="MinutesPerHour"/>-style constants and the save schema together — never introduce a
    /// parallel float clock.
    /// </para>
    /// </summary>
    public readonly struct SimTime : IEquatable<SimTime>, IComparable<SimTime>
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        /// <summary>Day 0, 00:00 — the world epoch.</summary>
        public static readonly SimTime Epoch = new SimTime(0);

        /// <summary>Sentinel for "never"; later than any schedulable time.</summary>
        public static readonly SimTime Never = new SimTime(long.MaxValue);

        public SimTime(long totalMinutes)
        {
            TotalMinutes = totalMinutes;
        }

        /// <summary>Whole simulation minutes since the epoch. The authoritative representation.</summary>
        public long TotalMinutes { get; }

        public int Day => (int)Math.DivRem(TotalMinutes, MinutesPerDay, out long _);

        public int HourOfDay => (int)(Math.Abs(TotalMinutes % MinutesPerDay) / MinutesPerHour);

        public int MinuteOfHour => (int)(Math.Abs(TotalMinutes % MinutesPerHour));

        /// <summary>Minutes elapsed since midnight of <see cref="Day"/>.</summary>
        public int MinuteOfDay => (int)(TotalMinutes - ((long)Day * MinutesPerDay));

        public static SimTime FromDayAndMinute(int day, int minuteOfDay) =>
            new SimTime(((long)day * MinutesPerDay) + minuteOfDay);

        public static SimTime FromClockTime(int day, int hour, int minute) =>
            new SimTime(((long)day * MinutesPerDay) + ((long)hour * MinutesPerHour) + minute);

        /// <summary>Midnight starting <paramref name="day"/>.</summary>
        public static SimTime StartOfDay(int day) => new SimTime((long)day * MinutesPerDay);

        public SimTime Plus(SimDuration duration) => new SimTime(TotalMinutes + duration.TotalMinutes);

        public SimTime Minus(SimDuration duration) => new SimTime(TotalMinutes - duration.TotalMinutes);

        public SimDuration Since(SimTime earlier) => new SimDuration(TotalMinutes - earlier.TotalMinutes);

        public bool Equals(SimTime other) => TotalMinutes == other.TotalMinutes;

        public override bool Equals(object obj) => obj is SimTime other && Equals(other);

        public override int GetHashCode() => TotalMinutes.GetHashCode();

        public int CompareTo(SimTime other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public override string ToString() => this == Never
            ? "never"
            : $"Day {Day} {HourOfDay:00}:{MinuteOfHour:00}";

        public static SimTime operator +(SimTime time, SimDuration duration) => time.Plus(duration);

        public static SimTime operator -(SimTime time, SimDuration duration) => time.Minus(duration);

        public static SimDuration operator -(SimTime later, SimTime earlier) => later.Since(earlier);

        public static bool operator ==(SimTime a, SimTime b) => a.TotalMinutes == b.TotalMinutes;

        public static bool operator !=(SimTime a, SimTime b) => a.TotalMinutes != b.TotalMinutes;

        public static bool operator <(SimTime a, SimTime b) => a.TotalMinutes < b.TotalMinutes;

        public static bool operator >(SimTime a, SimTime b) => a.TotalMinutes > b.TotalMinutes;

        public static bool operator <=(SimTime a, SimTime b) => a.TotalMinutes <= b.TotalMinutes;

        public static bool operator >=(SimTime a, SimTime b) => a.TotalMinutes >= b.TotalMinutes;
    }
}
