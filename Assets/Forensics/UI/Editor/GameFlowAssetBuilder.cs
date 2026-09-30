using System.Linq;
using Forensics;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameFlowAssetBuilder
{
    private const string Folder = "Assets/Forensics/UI";
    private const string ScenePath = "Assets/Scenes/ForensicsPrototype.unity";
    private const string ButtonPath = "Assets/MRTK.Tutorials.GettingStarted/Prefabs/PressableRoundButton.prefab";

    [InitializeOnLoadMethod]
    private static void BuildAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/GameFlowHUD.prefab") == null)
                CreateAssets();
        };
    }

    [MenuItem("Tools/Forensics/Create Game Flow Assets")]
    public static void CreateAssets()
    {
        GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
        if (buttonPrefab == null)
            throw new System.InvalidOperationException("The MRTK pressable button prefab is missing.");

        Material panelMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/GameFlowPanel.mat");
        if (panelMaterial == null)
        {
            Shader shader = Shader.Find("Forensics/AlwaysVisibleCheck");
            if (shader == null)
                throw new System.InvalidOperationException("The always-visible UI shader is missing.");
            panelMaterial = new Material(shader) { name = "GameFlowPanel" };
            AssetDatabase.CreateAsset(panelMaterial, Folder + "/GameFlowPanel.mat");
        }
        panelMaterial.shader = Shader.Find("Forensics/AlwaysVisibleCheck");
        panelMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        panelMaterial.SetColor("_Color", new Color(0.04f, 0.14f, 0.24f, 0.85f));
        panelMaterial.renderQueue = 2990;
        EditorUtility.SetDirty(panelMaterial);

        var root = new GameObject("GameFlowHUD");
        try
        {
            GameFlowView view = root.AddComponent<GameFlowView>();
            var promptPanel = new GameObject("ScanPromptPanel");
            promptPanel.transform.SetParent(root.transform, false);
            promptPanel.transform.localPosition = new Vector3(0f, 0.025f, 0.7f);
            CreateBackdrop(promptPanel.transform, panelMaterial, new Vector3(0f, 0f, 0.025f),
                new Vector3(0.57f, 0.16f, 1f));
            TMP_Text prompt = CreateText("ScanPrompt", promptPanel.transform,
                Vector3.zero, 3.5f, new Vector2(8f, 3f), 0.065f,
                "Look around to scan");

            var panel = new GameObject("WinPanel");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0.1f, 0.72f);

            CreateBackdrop(panel.transform, panelMaterial, new Vector3(0f, 0f, 0.09f),
                new Vector3(0.56f, 0.34f, 1f));

            CreateText("WinTitle", panel.transform, new Vector3(0f, 0.065f, 0f),
                5f, new Vector2(8f, 3f), 0.06f, "Case complete\nAll clues solved");

            Interactable restart = CreateButton(buttonPrefab, panel.transform,
                "RestartButton", new Vector3(-0.12f, -0.075f, -0.045f));
            Interactable exit = CreateButton(buttonPrefab, panel.transform,
                "ExitButton", new Vector3(0.12f, -0.075f, -0.045f));
            CreateText("RestartLabel", panel.transform, new Vector3(-0.12f, -0.145f, -0.08f),
                4.5f, new Vector2(3.2f, 1.5f), 0.05f, "Restart");
            CreateText("ExitLabel", panel.transform, new Vector3(0.12f, -0.145f, -0.08f),
                4.5f, new Vector2(3.2f, 1.5f), 0.05f, "Exit");

            var serialized = new SerializedObject(view);
            serialized.FindProperty("promptText").objectReferenceValue = prompt;
            serialized.FindProperty("promptPanel").objectReferenceValue = promptPanel;
            serialized.FindProperty("winPanel").objectReferenceValue = panel;
            serialized.FindProperty("restartButton").objectReferenceValue = restart;
            serialized.FindProperty("exitButton").objectReferenceValue = exit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/GameFlowHUD.prefab");
        }
        finally { Object.DestroyImmediate(root); }

        AssetDatabase.SaveAssets();
        IntegratePrototypeScene();
        PutPrototypeSceneFirstInBuild();
        Debug.Log("Created scan prompts, search message, and interactive win panel.");
    }

    private static TMP_Text CreateText(string name, Transform parent, Vector3 position,
        float fontSize, Vector2 size, float scale, string initialText)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = position;
        textObject.transform.localScale = Vector3.one * scale;
        var text = textObject.AddComponent<TextMeshPro>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.rectTransform.sizeDelta = size;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        CanvasRenderer obsoleteRenderer = textObject.GetComponent<CanvasRenderer>();
        if (obsoleteRenderer != null) Object.DestroyImmediate(obsoleteRenderer);
        return text;
    }

    private static void CreateBackdrop(Transform parent, Material material, Vector3 position, Vector3 scale)
    {
        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop";
        backdrop.transform.SetParent(parent, false);
        backdrop.transform.localPosition = position;
        backdrop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        backdrop.transform.localScale = scale;
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Interactable CreateButton(GameObject source, Transform parent,
        string name, Vector3 position)
    {
        GameObject button = (GameObject)PrefabUtility.InstantiatePrefab(source);
        button.name = name;
        button.transform.SetParent(parent, false);
        button.transform.localPosition = position;
        button.transform.localRotation = Quaternion.identity;
        button.transform.localScale = Vector3.one * 2.4f;
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "TextMeshPro") text.gameObject.SetActive(false);
            CanvasRenderer obsoleteRenderer = text.GetComponent<CanvasRenderer>();
            if (obsoleteRenderer != null && text is TextMeshPro)
                Object.DestroyImmediate(obsoleteRenderer);
        }
        Interactable interactable = button.GetComponent<Interactable>();
        if (interactable == null)
            throw new System.InvalidOperationException("The MRTK button has no Interactable component.");
        return interactable;
    }

    private static void IntegratePrototypeScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        bool saveScene = openedHere || !scene.isDirty;
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                HidingSpotGenerator generator = root.GetComponentInChildren<HidingSpotGenerator>(true);
                if (generator == null) continue;
                GameFlowController flow = generator.GetComponent<GameFlowController>();
                if (flow == null) flow = generator.gameObject.AddComponent<GameFlowController>();
                var source = new SerializedObject(generator);
                var serialized = new SerializedObject(flow);
                serialized.FindProperty("roomScanController").objectReferenceValue =
                    generator.GetComponent<RoomScanController>();
                serialized.FindProperty("hidingSpotGenerator").objectReferenceValue = generator;
                serialized.FindProperty("clueProgress").objectReferenceValue =
                    generator.GetComponent<ClueProgressController>();
                serialized.FindProperty("playerCamera").objectReferenceValue =
                    source.FindProperty("playerCamera").objectReferenceValue;
                serialized.FindProperty("viewPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/GameFlowHUD.prefab").GetComponent<GameFlowView>();
                Transform finishButton = generator.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child => child.name == "FinishScan");
                serialized.FindProperty("manualFinishScanButton").objectReferenceValue =
                    finishButton != null ? finishButton.gameObject : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene) EditorSceneManager.SaveScene(scene);
                else Debug.LogWarning("The prototype scene has unsaved edits. Save it to keep the game flow controller.");
                break;
            }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void PutPrototypeSceneFirstInBuild()
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
