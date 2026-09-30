using Forensics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClueHintAssetBuilder
{
    private const string UiFolder = "Assets/Forensics/UI";
    private const string ChevronSource = "Assets/MRTK.Tutorials.GettingStarted/Prefabs/Chevron.prefab";
    private const string ScenePath = "Assets/Scenes/ForensicsPrototype.unity";

    [InitializeOnLoadMethod]
    private static void BuildAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UiFolder + "/ClueHintChevron.prefab") == null)
                CreateAssets();
        };
    }

    [MenuItem("Tools/Forensics/Create Clue Hint Assets")]
    public static void CreateAssets()
    {
        Shader shader = Shader.Find("Forensics/AlwaysVisibleCheck");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ChevronSource);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiFolder + "/ClueHintChime.wav");
        if (shader == null || source == null || clip == null)
            throw new System.InvalidOperationException("Clue hint shader, Chevron prefab, or chime is missing.");

        string materialPath = UiFolder + "/ClueHintChevron.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            material.name = "ClueHintChevron";
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_Color", new Color(1f, 0.78f, 0.16f, 1f));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        var root = new GameObject("ClueHintChevron");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, UiFolder + "/ClueHintChevron.prefab");
        }
        finally { Object.DestroyImmediate(root); }

        AssetDatabase.SaveAssets();
        IntegrateScene(clip);
        Debug.Log("Created spatial clue hint sound and Chevron arrow in " + UiFolder);
    }

    private static void IntegrateScene(AudioClip clip)
    {
        if (!System.IO.File.Exists(ScenePath))
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        bool saveScene = openedHere || !scene.isDirty;

        try
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                HidingSpotGenerator generator = sceneRoot.GetComponentInChildren<HidingSpotGenerator>(true);
                if (generator == null)
                    continue;

                ClueHintController hint = generator.GetComponent<ClueHintController>();
                if (hint == null)
                    hint = generator.gameObject.AddComponent<ClueHintController>();

                var generatorProperties = new SerializedObject(generator);
                var properties = new SerializedObject(hint);
                properties.FindProperty("hidingSpotGenerator").objectReferenceValue = generator;
                properties.FindProperty("playerCamera").objectReferenceValue =
                    generatorProperties.FindProperty("playerCamera").objectReferenceValue;
                properties.FindProperty("hintSound").objectReferenceValue = clip;
                properties.FindProperty("chevronPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(UiFolder + "/ClueHintChevron.prefab");
                properties.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene)
                    EditorSceneManager.SaveScene(scene);
                else
                    Debug.LogWarning("The prototype scene has unsaved edits. Save it to keep the clue hint controller.");
                break;
            }
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
