using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Domain.Social;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one non-personality social pressure definition.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Social Pressure", fileName = "social_pressure_")]
    public sealed class SocialPressureAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "social.pressure.";
        [SerializeField] private SocialFactorRuleEntry[] rules = new SocialFactorRuleEntry[0];

        public string AuthoredId => authoredId;

        public SocialPressureDefinition ToDefinition()
        {
            var result = new SocialFactorRule[rules?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = rules[i].ToDefinition();
            return new SocialPressureDefinition(new AuthoredId(authoredId), result);
        }

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("social.pressure."))
                yield return $"{name}: pressure ids should be namespaced 'social.pressure.<something>'.";
        }
    }

    [System.Serializable]
    public struct SocialFactorRuleEntry
    {
        public string lensId;
        public SocialFactorSourceKind sourceKind;
        public string sourceId;
        public long coefficient;
        public string explanationId;
        public string requiredContextId;

        public SocialFactorRule ToDefinition() => new SocialFactorRule(
            new AuthoredId(lensId),
            sourceKind,
            new AuthoredId(sourceId),
            coefficient,
            new AuthoredId(explanationId),
            new AuthoredId(requiredContextId));
    }
}
