using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    public enum AppraisalStrength
    {
        Negligible = 0,
        Minor = 1,
        Moderate = 2,
        Strong = 3,
        Extreme = 4,
    }

    public readonly struct AppraisalStrengthThreshold
    {
        public AppraisalStrengthThreshold(long minimumMagnitude, AppraisalStrength strength)
        {
            MinimumMagnitude = IntegerMath.Clamp(minimumMagnitude, 0, SocialNumeric.Scale);
            Strength = strength;
        }

        public long MinimumMagnitude { get; }
        public AppraisalStrength Strength { get; }
    }

    /// <summary>Shared cross-lens calibration from normalized appraisal to gameplay strength.</summary>
    public sealed class AppraisalCalibrationProfile
    {
        private readonly AppraisalStrengthThreshold[] _thresholds;

        public AppraisalCalibrationProfile(AuthoredId id, IReadOnlyList<AppraisalStrengthThreshold> thresholds, int version)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A calibration profile needs a stable id.", nameof(id));
            }

            Id = id;
            Version = version;
            _thresholds = new AppraisalStrengthThreshold[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++)
            {
                _thresholds[i] = thresholds[i];
            }
            Array.Sort(_thresholds, (a, b) => a.MinimumMagnitude.CompareTo(b.MinimumMagnitude));
        }

        public AuthoredId Id { get; }
        public int Version { get; }
        public IReadOnlyList<AppraisalStrengthThreshold> Thresholds => _thresholds;

        public AppraisalStrength Calibrate(long normalizedAppraisal)
        {
            long magnitude = Math.Abs(normalizedAppraisal);
            AppraisalStrength result = AppraisalStrength.Negligible;
            for (int i = 0; i < _thresholds.Length && magnitude >= _thresholds[i].MinimumMagnitude; i++)
            {
                result = _thresholds[i].Strength;
            }

            return result;
        }
    }
}
