using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Domain.Social;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one shared appraisal calibration profile.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Appraisal Calibration", fileName = "social_calibration_")]
    public sealed class AppraisalCalibrationAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "social.calibration.";
        [SerializeField] private int version = 1;
        [SerializeField] private AppraisalStrengthThresholdEntry[] thresholds =
            new AppraisalStrengthThresholdEntry[0];

        public string AuthoredId => authoredId;

        public AppraisalCalibrationProfile ToDefinition()
        {
            var result = new AppraisalStrengthThreshold[thresholds?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = thresholds[i].ToDefinition();
            return new AppraisalCalibrationProfile(new AuthoredId(authoredId), result, version);
        }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("social.calibration."))
                yield return $"{name}: calibration ids should be namespaced 'social.calibration.<something>'.";
            if (version <= 0) yield return $"{name}: version must be positive.";

            var magnitudes = new HashSet<long>();
            for (int i = 0; i < (thresholds?.Length ?? 0); i++)
                if (!magnitudes.Add(thresholds[i].minimumMagnitude))
                    yield return $"{name}: threshold magnitude '{thresholds[i].minimumMagnitude}' is duplicated.";
        }
    }

    [System.Serializable]
    public struct AppraisalStrengthThresholdEntry
    {
        public long minimumMagnitude;
        public AppraisalStrength strength;

        public AppraisalStrengthThreshold ToDefinition() =>
            new AppraisalStrengthThreshold(minimumMagnitude, strength);
    }
}
