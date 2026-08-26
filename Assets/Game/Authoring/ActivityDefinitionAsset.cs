using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Activity definition.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Activity Definition", fileName = "activity_")]
    public sealed class ActivityDefinitionAsset : ScriptableObject
    {
        [Tooltip("Stable id persisted by runtime state and saves, e.g. activity.working.")]
        [SerializeField] private string authoredId = "activity.";
        [SerializeField] private string displayName = string.Empty;
        [Min(0)] [SerializeField] private int defaultDurationMinutes;
        [SerializeField] private bool producesOutcome;
        [SerializeField] private bool supportsInteractiveResolution;
        [SerializeField] private bool isTravel;

        public string AuthoredId => authoredId;

        public ActivityDefinition ToDefinition() => new ActivityDefinition(
            new AuthoredId(authoredId),
            displayName,
            SimDuration.FromMinutes(defaultDurationMinutes),
            producesOutcome,
            supportsInteractiveResolution,
            isTravel);

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("activity."))
                yield return $"{name}: Activity ids should be namespaced 'activity.<something>'.";
            if (string.IsNullOrWhiteSpace(displayName))
                yield return $"{name}: display name is required.";
            if (defaultDurationMinutes < 0)
                yield return $"{name}: default duration cannot be negative.";
            if (supportsInteractiveResolution && !producesOutcome)
                yield return $"{name}: interactive resolution requires an outcome-producing Activity.";
        }
    }
}
