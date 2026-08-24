using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Employment
{
    /// <summary>An authored recurring obligation available to an Employment.</summary>
    public sealed class EmploymentObligationPattern
    {
        public EmploymentObligationPattern(
            AuthoredId id,
            AuthoredId commitmentKind,
            int cycleLengthDays,
            int activeDaysMask,
            int startMinuteOfDay,
            SimDuration duration,
            int priority,
            AuthoredId activityDefinitionId,
            SimDuration startWindow = default,
            CommitmentAccountabilityPolicy accountabilityPolicy = null)
        {
            if (!id.IsSet) throw new ArgumentException("Employment obligation patterns need a stable authored id.", nameof(id));
            if (!commitmentKind.IsSet) throw new ArgumentException("Employment obligation patterns need a Commitment kind.", nameof(commitmentKind));
            if (cycleLengthDays < 1 || cycleLengthDays > 31) throw new ArgumentOutOfRangeException(nameof(cycleLengthDays));
            int validDaysMask = cycleLengthDays == 31 ? int.MaxValue : (1 << cycleLengthDays) - 1;
            if (activeDaysMask <= 0 || (activeDaysMask & ~validDaysMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(activeDaysMask), "Active days must be non-empty and fit inside the recurrence cycle.");
            if (startMinuteOfDay < 0 || startMinuteOfDay >= 24 * 60) throw new ArgumentOutOfRangeException(nameof(startMinuteOfDay));
            if (duration.IsNegative || duration.IsZero) throw new ArgumentOutOfRangeException(nameof(duration));
            if (startWindow.IsNegative) throw new ArgumentOutOfRangeException(nameof(startWindow));

            Id = id;
            CommitmentKind = commitmentKind;
            CycleLengthDays = cycleLengthDays;
            ActiveDaysMask = activeDaysMask;
            StartMinuteOfDay = startMinuteOfDay;
            Duration = duration;
            Priority = priority;
            ActivityDefinitionId = activityDefinitionId;
            StartWindow = startWindow;
            AccountabilityPolicy = accountabilityPolicy ?? CommitmentAccountabilityPolicy.None;
        }

        public AuthoredId Id { get; }
        public AuthoredId CommitmentKind { get; }
        public int CycleLengthDays { get; }
        public int ActiveDaysMask { get; }
        public int StartMinuteOfDay { get; }
        public SimDuration Duration { get; }
        public int Priority { get; }
        public AuthoredId ActivityDefinitionId { get; }
        public SimDuration StartWindow { get; }
        public CommitmentAccountabilityPolicy AccountabilityPolicy { get; }
    }

    /// <summary>Content describing a role and the obligation patterns that role may be assigned.</summary>
    public sealed class EmploymentDefinition
    {
        private readonly EmploymentObligationPattern[] _obligationPatterns;

        public EmploymentDefinition(
            AuthoredId id,
            AuthoredId roleId,
            IReadOnlyList<EmploymentObligationPattern> obligationPatterns = null)
        {
            if (!id.IsSet) throw new ArgumentException("Employment definitions need a stable authored id.", nameof(id));
            if (!roleId.IsSet) throw new ArgumentException("Employment definitions need a stable role id.", nameof(roleId));

            Id = id;
            RoleId = roleId;
            _obligationPatterns = CopyPatterns(obligationPatterns);
        }

        public AuthoredId Id { get; }
        public AuthoredId RoleId { get; }
        public IReadOnlyList<EmploymentObligationPattern> ObligationPatterns => _obligationPatterns;

        private static EmploymentObligationPattern[] CopyPatterns(IReadOnlyList<EmploymentObligationPattern> source)
        {
            if (source == null || source.Count == 0) return new EmploymentObligationPattern[0];
            var result = new EmploymentObligationPattern[source.Count];
            var seen = new HashSet<AuthoredId>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null) throw new ArgumentException("Employment obligation patterns cannot contain null.", nameof(source));
                if (!seen.Add(source[i].Id)) throw new ArgumentException($"Employment obligation pattern '{source[i].Id}' is duplicated.", nameof(source));
                result[i] = source[i];
            }
            Array.Sort(result, (a, b) => a.Id.CompareTo(b.Id));
            return result;
        }
    }
}
