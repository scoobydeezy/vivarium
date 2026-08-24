using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// A recurring routine or obligation pattern — the content behind "Mina works Monday to Friday"
    /// (§29.4, also called a RoutinePattern in the brief).
    /// <para>
    /// A template is <b>not</b> a calendar. It is materialized into concrete
    /// <see cref="Commitment"/> instances reactively, only across the bounded planning horizon that
    /// current simulation or a future-facing query actually needs (invariant 44). Nothing here
    /// eagerly enumerates the next thirty years.
    /// </para>
    /// </summary>
    public sealed class CommitmentTemplate
    {
        public CommitmentTemplate(
            AuthoredId id,
            AuthoredId commitmentKind,
            int cycleLengthDays,
            int activeDaysMask,
            int startMinuteOfDay,
            SimDuration duration,
            LocationId locationId,
            int priority,
            AuthoredId activityDefinitionId = default,
            SimDuration startWindow = default,
            EntityRef source = default,
            CommitmentAccountabilityPolicy accountabilityPolicy = null,
            IReadOnlyList<StakeholderRef> stakeholders = null)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            if (cycleLengthDays < 1 || cycleLengthDays > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleLengthDays), "Cycle length must be 1–31 days.");
            }

            Id = id;
            CommitmentKind = commitmentKind;
            CycleLengthDays = cycleLengthDays;
            ActiveDaysMask = activeDaysMask;
            StartMinuteOfDay = startMinuteOfDay;
            Duration = duration;
            LocationId = locationId;
            Priority = priority;
            ActivityDefinitionId = activityDefinitionId;
            StartWindow = startWindow;
            Source = source;
            AccountabilityPolicy = accountabilityPolicy ?? CommitmentAccountabilityPolicy.None;
            Stakeholders = stakeholders;
        }

        public AuthoredId Id { get; }

        public AuthoredId CommitmentKind { get; }

        /// <summary>Length of the repeating cycle in days — 7 for a week, 1 for daily.</summary>
        public int CycleLengthDays { get; }

        /// <summary>Bitmask of days within the cycle on which this occurs. Bit 0 is day 0 of the cycle.</summary>
        public int ActiveDaysMask { get; }

        public int StartMinuteOfDay { get; }

        public SimDuration Duration { get; }

        public LocationId LocationId { get; }

        public int Priority { get; }

        /// <summary>The Activity each materialized occurrence becomes (§29.5).</summary>
        public AuthoredId ActivityDefinitionId { get; }

        /// <summary>How late the character may still start and count it as kept. Zero means punctual only.</summary>
        public SimDuration StartWindow { get; }

        public EntityRef Source { get; }

        public CommitmentAccountabilityPolicy AccountabilityPolicy { get; }

        public IReadOnlyList<StakeholderRef> Stakeholders { get; }

        public bool OccursOnDay(int day) => (ActiveDaysMask & (1 << (((day % CycleLengthDays) + CycleLengthDays) % CycleLengthDays))) != 0;

        /// <summary>
        /// The first occurrence at or after <paramref name="from"/>, or <c>null</c> if the mask is empty.
        /// Bounded scan: at most one cycle is ever examined.
        /// </summary>
        public SimTime? FirstOccurrenceAtOrAfter(SimTime from)
        {
            if (ActiveDaysMask == 0)
            {
                return null;
            }

            for (int offset = 0; offset <= CycleLengthDays; offset++)
            {
                int day = from.Day + offset;
                if (!OccursOnDay(day))
                {
                    continue;
                }

                SimTime candidate = SimTime.FromDayAndMinute(day, StartMinuteOfDay);
                if (candidate >= from)
                {
                    return candidate;
                }
            }

            return null;
        }

        public override string ToString() => $"{Id} ({CommitmentKind} every {CycleLengthDays}d mask {ActiveDaysMask:X})";
    }
}
