using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for the pack's singleton Decision Importance policy.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Decision Importance Policy", fileName = "policy_decision_importance")]
    public sealed class DecisionImportancePolicyAsset : ScriptableObject
    {
        public const string StableId = "policy.decision_importance";

        [SerializeField] private int admissionFloor = 6500;
        [SerializeField] private int prioritizedFeedFloor = 6500;
        [SerializeField] private int normalFeedFloor = 7000;
        [SerializeField] private int autoHoldFloor = 7000;

        public string AuthoredId => StableId;

        public DecisionImportancePolicyDefinition ToDefinition() =>
            new DecisionImportancePolicyDefinition(
                admissionFloor,
                prioritizedFeedFloor,
                normalFeedFloor,
                autoHoldFloor);

        public IEnumerable<string> Validate()
        {
            if (admissionFloor < 0 || autoHoldFloor > SignalNumeric.Scale)
                yield return $"{name}: Importance floors must stay within 0..{SignalNumeric.Scale}.";
            if (admissionFloor > prioritizedFeedFloor ||
                prioritizedFeedFloor > normalFeedFloor ||
                normalFeedFloor > autoHoldFloor)
                yield return $"{name}: floors must satisfy Admission <= PrioritizedFeed <= NormalFeed <= AutoHold.";
        }
    }
}
