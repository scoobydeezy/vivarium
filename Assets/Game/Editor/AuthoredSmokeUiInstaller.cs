using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vivarium.Unity.Bootstrap;
using Vivarium.Unity.Presentation;

namespace Vivarium.Unity.Editor
{
    public static class AuthoredSmokeUiInstaller
    {
        private const string TestScenePath = "Assets/Scenes/TestScene.unity";
        private const string PrefabFolder = "Assets/Game/Presentation/Prefabs";

        [MenuItem("Vivarium/Install Authored Smoke UI")]
        public static void Install()
        {
            EnsureFolder("Assets/Game/Presentation", "Prefabs");

            CharacterView characterPrefab = CreateCharacterViewPrefab();
            CharacterRosterEntry rosterEntryPrefab = CreateRosterEntryPrefab();
            CharacterProfilePanel profilePrefab = CreateProfilePanelPrefab();
            CharacterRosterPanel rosterPrefab = CreateRosterPanelPrefab(rosterEntryPrefab);
            DecisionPanel decisionPrefab = CreateDecisionPanelPrefab();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            GameBootstrapper bootstrapper = Object.FindAnyObjectByType<GameBootstrapper>();
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (bootstrapper == null || canvas == null)
            {
                throw new System.InvalidOperationException("TestScene needs both GameBootstrapper and Canvas components.");
            }

            WorldPresenter presenter = EnsurePresenter();
            Transform viewRoot = EnsureChild(presenter.transform, "Character View Root");
            CharacterProfilePanel profilePanel = EnsurePrefabInstance(profilePrefab, canvas.transform, "Character Profile Panel");
            CharacterRosterPanel rosterPanel = EnsurePrefabInstance(rosterPrefab, canvas.transform, "Character Roster Panel");
            DecisionPanel decisionPanel = EnsurePrefabInstance(decisionPrefab, canvas.transform, "Decision Panel");
            EnsurePersistencePanel(canvas.transform, bootstrapper);

            var presenterObject = new SerializedObject(presenter);
            presenterObject.FindProperty("characterViewPrefab").objectReferenceValue = characterPrefab;
            presenterObject.FindProperty("viewRoot").objectReferenceValue = viewRoot;
            presenterObject.FindProperty("profilePanel").objectReferenceValue = profilePanel;
            presenterObject.FindProperty("rosterPanel").objectReferenceValue = rosterPanel;
            presenterObject.FindProperty("decisionPanel").objectReferenceValue = decisionPanel;
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            var bootstrapObject = new SerializedObject(bootstrapper);
            bootstrapObject.FindProperty("presenter").objectReferenceValue = presenter;
            bootstrapObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = presenter;
            Debug.Log("Vivarium authored smoke UI installed and TestScene saved.");
        }

        private static CharacterView CreateCharacterViewPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "CharacterView";
            root.transform.localScale = new Vector3(0.75f, 1f, 0.75f);
            CharacterView view = root.AddComponent<CharacterView>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CharacterView.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CharacterView>();
        }

        private static CharacterProfilePanel CreateProfilePanelPrefab()
        {
            GameObject root = UiObject("Character Profile Panel", null, new Vector2(620f, 330f));
            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f));
            root.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.08f, 0.88f);
            CharacterProfilePanel panel = root.AddComponent<CharacterProfilePanel>();

            TextMeshProUGUI summary = CreateText(root.transform, "Summary", 26f, TextAlignmentOptions.TopLeft);
            RectTransform summaryRect = summary.rectTransform;
            summaryRect.anchorMin = new Vector2(0f, 0f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.offsetMin = new Vector2(18f, 64f);
            summaryRect.offsetMax = new Vector2(-18f, -18f);

            Button close = CreateButton(root.transform, "Close", new Vector2(18f, 12f), new Color(0.16f, 0.16f, 0.16f, 1f));
            Button travel = CreateButton(root.transform, "Travel", new Vector2(174f, 12f), new Color(0.08f, 0.35f, 0.5f, 1f));

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("summaryText").objectReferenceValue = summary;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("travelButton").objectReferenceValue = travel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CharacterProfilePanel.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CharacterProfilePanel>();
        }

        private static CharacterRosterEntry CreateRosterEntryPrefab()
        {
            GameObject root = UiObject("Character Roster Entry", null, new Vector2(280f, 44f));
            Image image = root.AddComponent<Image>();
            Button button = root.AddComponent<Button>();
            TextMeshProUGUI label = CreateText(root.transform, "Label", 22f, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.raycastTarget = false;

            CharacterRosterEntry entry = root.AddComponent<CharacterRosterEntry>();
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("background").objectReferenceValue = image;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CharacterRosterEntry.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CharacterRosterEntry>();
        }

        private static CharacterRosterPanel CreateRosterPanelPrefab(CharacterRosterEntry entryPrefab)
        {
            GameObject root = UiObject("Character Roster Panel", null, new Vector2(300f, 240f));
            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(rect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -24f));
            root.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.08f, 0.75f);
            CharacterRosterPanel panel = root.AddComponent<CharacterRosterPanel>();

            GameObject content = UiObject("Entries", root.transform, new Vector2(280f, 220f));
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(10f, 10f);
            contentRect.offsetMax = new Vector2(-10f, -10f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("entryPrefab").objectReferenceValue = entryPrefab;
            serialized.FindProperty("entryRoot").objectReferenceValue = content.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CharacterRosterPanel.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CharacterRosterPanel>();
        }

        private static DecisionPanel CreateDecisionPanelPrefab()
        {
            GameObject root = UiObject("Decision Panel", null, new Vector2(520f, 400f));
            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f));
            root.AddComponent<Image>().color = new Color(0.08f, 0.05f, 0.1f, 0.9f);
            DecisionPanel panel = root.AddComponent<DecisionPanel>();

            TextMeshProUGUI summary = CreateText(root.transform, "Summary", 23f, TextAlignmentOptions.TopLeft);
            summary.rectTransform.anchorMin = new Vector2(0f, 0f);
            summary.rectTransform.anchorMax = new Vector2(1f, 1f);
            summary.rectTransform.offsetMin = new Vector2(18f, 64f);
            summary.rectTransform.offsetMax = new Vector2(-18f, -18f);

            Button hold = CreateButton(root.transform, "Hold", new Vector2(18f, 12f), new Color(0.32f, 0.22f, 0.08f, 1f));
            Button release = CreateButton(root.transform, "Release", new Vector2(174f, 12f), new Color(0.24f, 0.18f, 0.08f, 1f));
            Button intervene = CreateButton(root.transform, "Encourage", new Vector2(330f, 12f), new Color(0.35f, 0.12f, 0.3f, 1f));

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("summaryText").objectReferenceValue = summary;
            serialized.FindProperty("holdButton").objectReferenceValue = hold;
            serialized.FindProperty("releaseButton").objectReferenceValue = release;
            serialized.FindProperty("interveneButton").objectReferenceValue = intervene;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/DecisionPanel.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DecisionPanel>();
        }

        private static WorldPresenter EnsurePresenter()
        {
            WorldPresenter existing = Object.FindAnyObjectByType<WorldPresenter>();
            if (existing != null)
            {
                return existing;
            }

            return new GameObject("World Presenter").AddComponent<WorldPresenter>();
        }

        private static T EnsurePrefabInstance<T>(T prefab, Transform parent, string name) where T : Component
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent(out T component))
            {
                return component;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static void EnsurePersistencePanel(Transform parent, GameBootstrapper bootstrapper)
        {
            Transform existing = parent.Find("Debug Persistence Panel");
            if (existing != null)
            {
                return;
            }

            GameObject panel = UiObject("Debug Persistence Panel", parent, new Vector2(300f, 52f));
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f));
            Button save = CreateButton(panel.transform, "Save", new Vector2(4f, 4f), new Color(0.2f, 0.28f, 0.2f, 1f));
            Button load = CreateButton(panel.transform, "Load", new Vector2(156f, 4f), new Color(0.2f, 0.28f, 0.2f, 1f));
            UnityEventTools.AddPersistentListener(save.onClick, bootstrapper.SaveRuntimeSmokeTest);
            UnityEventTools.AddPersistentListener(load.onClick, bootstrapper.LoadRuntimeSmokeTestFromUi);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var created = new GameObject(name).transform;
            created.SetParent(parent, false);
            return created;
        }

        private static Button CreateButton(Transform parent, string text, Vector2 position, Color color)
        {
            GameObject root = UiObject(text, parent, new Vector2(140f, 44f));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            Image image = root.AddComponent<Image>();
            image.color = color;
            Button button = root.AddComponent<Button>();
            TextMeshProUGUI label = CreateText(root.transform, "Label", 23f, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            label.text = text;
            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            GameObject root = UiObject(name, parent, Vector2.zero);
            TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.text = name;
            return text;
        }

        private static GameObject UiObject(string name, Transform parent, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            root.GetComponent<RectTransform>().sizeDelta = size;
            return root;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
