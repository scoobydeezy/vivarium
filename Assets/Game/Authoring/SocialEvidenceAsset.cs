using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Domain.Social;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one observed social action model.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Social Evidence", fileName = "social_evidence_")]
    public sealed class SocialEvidenceAsset : ScriptableObject
    {
        [SerializeField] private string actionDefinitionId = "social.action.";
        [SerializeField] private string explanationId = "social.explanation.";
        [SerializeField] private SocialEvidenceMeasurementEntry[] measurements =
            new SocialEvidenceMeasurementEntry[0];

        public string AuthoredId => actionDefinitionId;

        public SocialEvidenceDefinition ToDefinition()
        {
            var result = new SocialEvidenceMeasurement[measurements?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = measurements[i].ToDefinition();
            return new SocialEvidenceDefinition(
                new AuthoredId(actionDefinitionId), result, new AuthoredId(explanationId));
        }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(actionDefinitionId) || actionDefinitionId.EndsWith("."))
                yield return $"{name}: action id '{actionDefinitionId}' is incomplete.";
            if (!actionDefinitionId.StartsWith("social.action."))
                yield return $"{name}: action ids should be namespaced 'social.action.<something>'.";
            if (string.IsNullOrEmpty(explanationId) || explanationId.EndsWith("."))
                yield return $"{name}: explanation id '{explanationId}' is incomplete.";
            if ((measurements?.Length ?? 0) == 0)
                yield return $"{name}: at least one measurement is required.";

            var ids = new HashSet<string>();
            for (int i = 0; i < (measurements?.Length ?? 0); i++)
            {
                SocialEvidenceMeasurementEntry measurement = measurements[i];
                if (!ids.Add(measurement.authoredId))
                    yield return $"{name}: measurement '{measurement.authoredId}' is duplicated.";
                if (measurement.noiseVariance <= 0)
                    yield return $"{name}: measurement '{measurement.authoredId}' needs positive noise variance.";
                if ((measurement.projection?.Length ?? 0) == 0)
                    yield return $"{name}: measurement '{measurement.authoredId}' needs a projection.";
            }
        }
    }

    [System.Serializable]
    public struct SocialEvidenceMeasurementEntry
    {
        public string authoredId;
        public SocialLinearEntry[] projection;
        public long observedValue;
        public long noiseVariance;

        public SocialEvidenceMeasurement ToDefinition()
        {
            var result = new SocialLinearTerm[projection?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = projection[i].ToDefinition();
            return new SocialEvidenceMeasurement(new AuthoredId(authoredId), result, observedValue, noiseVariance);
        }
    }
}
