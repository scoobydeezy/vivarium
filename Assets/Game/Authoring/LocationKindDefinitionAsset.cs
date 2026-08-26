using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Domain.Spatial;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Location Kind.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Location Kind Definition", fileName = "location_kind_")]
    public sealed class LocationKindDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "location_kind.";
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private bool occupiableByDefault = true;

        public string AuthoredId => authoredId;

        public LocationKindDefinition ToDefinition() => new LocationKindDefinition(
            new AuthoredId(authoredId), displayName, occupiableByDefault);

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            if (!authoredId.StartsWith("location_kind."))
                yield return $"{name}: Location Kind ids should be namespaced 'location_kind.<something>'.";
            if (string.IsNullOrWhiteSpace(displayName))
                yield return $"{name}: display name is required.";
        }
    }
}
