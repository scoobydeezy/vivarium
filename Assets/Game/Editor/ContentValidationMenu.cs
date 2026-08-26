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
            List<string> indexProblems = ContentPackBaker.ValidateAllFresh();
            for (int i = 0; i < indexProblems.Count; i++) Debug.LogError(indexProblems[i]);

            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ContentPackIndexAsset));

            if (guids.Length == 0)
            {
                Debug.LogWarning("No ContentPackIndexAsset found to validate.");
                return;
            }

            int failed = indexProblems.Count;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var index = AssetDatabase.LoadAssetAtPath<ContentPackIndexAsset>(path);

                List<string> problems = index.ValidateContent();

                // Contribution construction rejects same-pack duplicates. Catalog-wide reference
                // validation occurs only after configured load-order resolution.
                try
                {
                    index.BuildDefinitionSet();
                }
                catch (System.Exception exception)
                {
                    problems.Add(exception.Message);
                }

                if (problems.Count == 0)
                {
                    Debug.Log($"{path}: pack contribution valid (version {index.Manifest.PackVersion}).");
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
