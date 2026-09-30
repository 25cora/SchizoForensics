using TMPro;
using Forensics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClueProgressAssetBuilder
{
    private const string Folder = "Assets/Forensics/UI";

    [InitializeOnLoadMethod]
    private static void BuildMissingAssetsAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SolvedClueMarker.prefab") == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ClueProgressHUD.prefab") == null)
                CreateAssets();
        };
    }

    [MenuItem("Tools/Forensics/Create Clue Progress Assets")]
    public static void CreateAssets()
    {
        string texturePath = Folder + "/SolvedCheck.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer == null)
            throw new System.InvalidOperationException("SolvedCheck.png is missing.");
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Sprite check = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        Shader shader = Shader.Find("Forensics/AlwaysVisibleCheck");
        if (check == null || shader == null)
            throw new System.InvalidOperationException("The check Sprite or always-visible shader did not import.");

        string materialPath = Folder + "/SolvedCheck.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            material.SetTexture("_MainTex", check.texture);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        var marker = new GameObject("SolvedClueMarker");
        try
        {
            marker.AddComponent<Forensics.SolvedClueMarker>();
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = check;
            renderer.sharedMaterial = material;
            marker.transform.localScale = Vector3.one * (0.065f / check.bounds.size.x);
            PrefabUtility.SaveAsPrefabAsset(marker, Folder + "/SolvedClueMarker.prefab");
        }
        finally { Object.DestroyImmediate(marker); }

        var hud = new GameObject("ClueProgressHUD");
        try
        {
            var text = hud.AddComponent<TextMeshPro>();
            text.text = "0 of 0 clues found\n0 out of 0 clues Solved";
            text.fontSize = 3.5f;
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(36f, 10f);
            text.rectTransform.pivot = new Vector2(0.99f, 0.95f);
            hud.transform.localScale = Vector3.one * 0.07f;
            PrefabUtility.SaveAsPrefabAsset(hud, Folder + "/ClueProgressHUD.prefab");
        }
        finally { Object.DestroyImmediate(hud); }

        AssetDatabase.SaveAssets();
        IntegratePrototypeScene();
        Debug.Log("Created clue progress HUD, always-visible marker, and material in " + Folder);
    }

    private static void IntegratePrototypeScene()
    {
        const string scenePath = "Assets/Scenes/ForensicsPrototype.unity";
        if (!System.IO.File.Exists(scenePath))
            return;

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        bool saveScene = openedHere || !scene.isDirty;

        try
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                HidingSpotGenerator generator = sceneRoot.GetComponentInChildren<HidingSpotGenerator>(true);
                if (generator == null)
                    continue;

                ClueProgressController progress = generator.GetComponent<ClueProgressController>();
                if (progress == null)
                    progress = generator.gameObject.AddComponent<ClueProgressController>();

                var generatorProperties = new SerializedObject(generator);
                var progressProperties = new SerializedObject(progress);
                progressProperties.FindProperty("hidingSpotGenerator").objectReferenceValue = generator;
                progressProperties.FindProperty("playerCamera").objectReferenceValue =
                    generatorProperties.FindProperty("playerCamera").objectReferenceValue;
                progressProperties.FindProperty("markerParent").objectReferenceValue =
                    generatorProperties.FindProperty("spawnedClueParent").objectReferenceValue;
                GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ClueProgressHUD.prefab");
                progressProperties.FindProperty("hudTextPrefab").objectReferenceValue = hud.GetComponent<TMP_Text>();
                progressProperties.FindProperty("solvedMarkerPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SolvedClueMarker.prefab");
                progressProperties.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene)
                    EditorSceneManager.SaveScene(scene);
                else
                    Debug.LogWarning("The prototype scene had unsaved edits. Clue progress was added in the open editor; save the scene when ready.");
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
