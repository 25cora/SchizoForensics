using Forensics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates the editable asset once; the resulting prefab is the source of truth.</summary>
public static class BloodSplatterPrefabBuilder
{
    private const string Folder = "Assets/Forensics/Clues/BloodSplatter";
    private const string PrefabPath = Folder + "/BloodSplatterClue.prefab";

    [InitializeOnLoadMethod]
    private static void BuildMissingPrefabAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefab();
            }
        };
    }

    [MenuItem("Tools/Forensics/Create Blood Splatter Prefab")]
    public static void CreatePrefab()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/BloodSplatter_Directional_25deg.png");
        if (texture == null)
        {
            throw new System.InvalidOperationException("The directional blood splatter texture must be imported as a Sprite first.");
        }

        Material splatterMaterial = GetMaterial("BloodSplatter.mat", "Sprites/Default", Color.white);
        Material stringMaterial = GetMaterial("DirectionString.mat", "Sprites/Default", Color.white);
        Material handleMaterial = GetMaterial("StringHandle.mat", "Universal Render Pipeline/Unlit", new Color(1f, 0.83f, 0.37f));

        var root = new GameObject("BloodSplatterClue");
        try
        {
            var rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.size = new Vector3(0.16f, 0.16f, 0.004f);
            var clue = root.AddComponent<BloodSplatterClue>();
            Set(clue, "clueId", "blood_splatter");
            Set(clue, "description", "Align both strings with the direction of the blood splatter.");
            Set(clue, "closeWhenClickedDuringInspection", false);
            Set(clue, "completeWhenCorrectItemIsUsed", false);
            Set(clue, "inspectionEulerOffset", new Vector3(0f, 180f, 0f));
            Set(clue, "inspectionScaleMultiplier", 1.5f);
            Set(clue, "correctDirection", new Vector2(1f, 0.4663f));
            Set(clue, "allowedAngleError", 12f);
            Set(clue, "interactionPlaneOffset", 0.006f);

            var visual = new GameObject("BloodSplatterVisual");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = texture;
            renderer.sharedMaterial = splatterMaterial;
            // Keep the upright visual within the generator's 0.07 m clearance radius.
            visual.transform.localScale = Vector3.one * (0.16f / texture.bounds.size.x);

            var interaction = new GameObject("StringInteractionRoot");
            interaction.transform.SetParent(root.transform, false);
            Set(clue, "stringInteractionRoot", interaction);

            BloodDirectionString first = CreateString(interaction.transform, "String1",
                new Vector2(-0.06f, -0.025f), new Vector2(0.06f, -0.025f), stringMaterial, handleMaterial);
            BloodDirectionString second = CreateString(interaction.transform, "String2",
                new Vector2(-0.06f, 0.025f), new Vector2(0.06f, 0.025f), stringMaterial, handleMaterial);
            Set(clue, "firstString", first);
            Set(clue, "secondString", second);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created editable blood splatter prefab at " + PrefabPath);

            // The prototype scene is where HidingSpotGenerator currently stores its clue list.
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/ForensicsPrototype.unity");
            if (scene.isLoaded)
            {
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                {
                    var generator = sceneRoot.GetComponentInChildren<HidingSpotGenerator>(true);
                    if (generator == null) continue;
                    var serialized = new SerializedObject(generator);
                    var list = serialized.FindProperty("cluePrefabs");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                    bool found = false;
                    for (int i = 0; i < list.arraySize; i++)
                        found |= list.GetArrayElementAtIndex(i).objectReferenceValue == prefab;
                    if (!found)
                    {
                        int index = list.arraySize;
                        list.InsertArrayElementAtIndex(index);
                        list.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    break;
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static BloodDirectionString CreateString(Transform parent, string name, Vector2 start,
        Vector2 end, Material lineMaterial, Material handleMaterial)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent, false);
        var line = item.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.startWidth = 0.0025f;
        line.endWidth = 0.0025f;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.sortingOrder = 10;
        var directionString = item.AddComponent<BloodDirectionString>();
        var startHandle = CreateHandle(item.transform, "StartHandle", start, handleMaterial);
        var endHandle = CreateHandle(item.transform, "EndHandle", end, handleMaterial);
        Set(directionString, "lineRenderer", line);
        Set(directionString, "startHandle", startHandle);
        Set(directionString, "endHandle", endHandle);
        Set(directionString, "movementHalfExtents", new Vector2(0.07f, 0.07f));
        Set(directionString, "minimumStringLength", 0.035f);
        line.SetPosition(0, startHandle.transform.localPosition);
        line.SetPosition(1, endHandle.transform.localPosition);
        return directionString;
    }

    private static BloodStringHandle CreateHandle(Transform parent, string name, Vector2 position, Material material)
    {
        var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = name;
        handle.transform.SetParent(parent, false);
        handle.transform.localPosition = new Vector3(position.x, position.y, 0.006f);
        handle.transform.localScale = Vector3.one * 0.014f;
        handle.GetComponent<MeshRenderer>().sharedMaterial = material;
        handle.GetComponent<MeshRenderer>().sortingOrder = 11;
        return handle.AddComponent<BloodStringHandle>();
    }

    private static Material GetMaterial(string file, string shaderName, Color color)
    {
        string path = Folder + "/" + file;
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException("Missing shader: " + shaderName);
        var material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Set(Object target, string property, object value)
    {
        var serialized = new SerializedObject(target);
        var field = serialized.FindProperty(property);
        if (field == null) throw new System.InvalidOperationException(target.name + " has no serialized field " + property);
        switch (value)
        {
            case string text: field.stringValue = text; break;
            case bool flag: field.boolValue = flag; break;
            case float number: field.floatValue = number; break;
            case Vector2 vector: field.vector2Value = vector; break;
            case Vector3 vector: field.vector3Value = vector; break;
            case Object reference: field.objectReferenceValue = reference; break;
            default: throw new System.InvalidOperationException("Unsupported field value: " + property);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
