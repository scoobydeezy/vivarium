using UnityEngine;
using Vivarium.Domain.Content;

namespace Vivarium.Unity.Authoring
{
    [System.Serializable]
    public struct ContentOverrideEntry
    {
        public ContentDefinitionFamily family;
        public string authoredId;
        public string expectedSourcePackId;
    }

    /// <summary>Stable identity and resolution intent for one authored content pack.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Content Pack Manifest", fileName = "pack.manifest")]
    public sealed class ContentPackManifestAsset : ScriptableObject
    {
        [SerializeField] private string packId = "pack.";
        [SerializeField] private string displayName = string.Empty;
        [Min(1)] [SerializeField] private int packVersion = 1;
        [SerializeField] private ContentOverrideEntry[] overrides = new ContentOverrideEntry[0];

        public string PackId => packId;
        public string DisplayName => displayName;
        public int PackVersion => packVersion;
        public ContentOverrideEntry[] Overrides => overrides;
    }
}
