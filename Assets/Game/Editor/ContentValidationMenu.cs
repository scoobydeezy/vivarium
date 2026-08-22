using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vivarium.Unity.Authoring;

namespace Vivarium.Unity.EditorTools
{
    /// <summary>
    /// Editor-time content validation (§42).
    /// <para>
    /// Catching duplicate ids, missing references, and invalid ranges here is the difference between a
    /// designer seeing a list of problems and a player's save failing to load three patches later.
    /// </para>
    /// </summary>
    public static class ContentValidationMenu
    {
        [MenuItem("Vivarium/Validate Content Packs")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ContentPackAsset));

            if (guids.Length == 0)
            {
                Debug.LogWarning("No ContentPackAsset found to validate.");
                return;
            }

            int failed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAsset>(path);

                List<string> problems = pack.Validate();

                // Building is itself a validation pass: catalog construction rejects duplicates, and
                // ContentValidator checks ranges and cross-references.
                try
                {
                    pack.Build();
                }
                catch (System.Exception exception)
                {
                    problems.Add(exception.Message);
                }

                if (problems.Count == 0)
                {
                    Debug.Log($"{path}: content valid (version {pack.ContentVersion}).");
                    continue;
                }

                failed++;
                Debug.LogError($"{path}: {problems.Count} problem(s):\n - " + string.Join("\n - ", problems));
            }

            if (failed == 0)
            {
                Debug.Log($"Validated {guids.Length} content pack(s) with no problems.");
            }
        }
    }
}
