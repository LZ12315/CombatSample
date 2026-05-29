using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the MiHoYo release boss arena environment directly into the target scene.
/// It only owns objects under BossArena_RuinedSanctum_Environment, so gameplay actors,
/// cameras, hit boxes, action assets and existing non-environment objects are left intact.
/// </summary>
public static class ReleaseArenaBaker
{
    private const string DefaultScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EnvironmentRootName = "BossArena_RuinedSanctum_Environment";

    private const string GroundLayer = "Ground";
    private const string FloorLayer = "Floor";
    private const string ObstacleLayer = "Obstacle";
    private const string DefaultLayer = "Default";

    [MenuItem("Tools/Release/Bake Boss Arena Environment")]
    public static void BakeActiveScene()
    {
        EnsureLayers();
        BuildEnvironmentInScene(SceneManager.GetActiveScene());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    /// <summary>
    /// Command line entry point:
    /// Unity -batchmode -quit -projectPath <project> -executeMethod ReleaseArenaBaker.BakeDefaultScene
    /// </summary>
    public static void BakeDefaultScene()
    {
        EnsureLayers();

        var scene = EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
        BuildEnvironmentInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildEnvironmentInScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogError("ReleaseArenaBaker: Active scene is invalid. Environment was not baked.");
            return;
        }

        var oldRoot = GameObject.Find(EnvironmentRootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        var root = new GameObject(EnvironmentRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        var gameplay = CreateEmpty("Gameplay_Reference", root.transform);
        var geometry = CreateEmpty("Level_Geometry", root.transform);
        var dressing = CreateEmpty("Art_Dressing_NoGameplayCollision", root.transform);
        var lighting = CreateEmpty("Lighting", root.transform);
        var debug = CreateEmpty("Debug_Markers", root.transform);
        debug.SetActive(false);

        var matFloor = MakeMaterial("BossArena_Mat_StoneFloor", new Color(0.32f, 0.32f, 0.34f));
        var matDarkStone = MakeMaterial("BossArena_Mat_DarkStone", new Color(0.16f, 0.15f, 0.15f));
        var matWall = MakeMaterial("BossArena_Mat_WallStone", new Color(0.24f, 0.23f, 0.22f));
        var matAccent = MakeMaterial("BossArena_Mat_AccentStone", new Color(0.40f, 0.36f, 0.30f));
        var matFire = MakeMaterial("BossArena_Mat_FireGlow", new Color(1.0f, 0.45f, 0.12f));
        EnableEmission(matFire, new Color(1.0f, 0.32f, 0.05f) * 1.7f);

        // Clean, flat boss-combat space. Top surface is y = 0.
        CreateCube("Ground_Base_Foundation", geometry.transform, new Vector3(0f, -0.15f, 0f), new Vector3(30f, 0.3f, 30f), matDarkStone, GroundLayer, true);
        CreateOctagonalPrism("Ground_MainArena_Octagon", geometry.transform, 12f, 0.28f, new Vector3(0f, 0.02f, 0f), matFloor, GroundLayer);
        CreateCube("Ground_CenterMark_Visual", geometry.transform, new Vector3(0f, 0.175f, 0f), new Vector3(5.4f, 0.04f, 5.4f), matAccent, GroundLayer, false);

        // Camera-safe hard boundaries. These are large, simple colliders.
        CreateCube("Obstacle_Boundary_NorthWall", geometry.transform, new Vector3(0f, 2.5f, 14.5f), new Vector3(30f, 5f, 1f), matWall, ObstacleLayer, true);
        CreateCube("Obstacle_Boundary_SouthWall", geometry.transform, new Vector3(0f, 2.5f, -14.5f), new Vector3(30f, 5f, 1f), matWall, ObstacleLayer, true);
        CreateCube("Obstacle_Boundary_EastWall", geometry.transform, new Vector3(14.5f, 2.5f, 0f), new Vector3(1f, 5f, 30f), matWall, ObstacleLayer, true);
        CreateCube("Obstacle_Boundary_WestWall", geometry.transform, new Vector3(-14.5f, 2.5f, 0f), new Vector3(1f, 5f, 30f), matWall, ObstacleLayer, true);

        // Ceiling exists for systems that query Floor above the combat space. It is transparent visual-wise.
        var ceiling = CreateCube("Floor_Ceiling_CombatLimit", geometry.transform, new Vector3(0f, 8f, 0f), new Vector3(30f, 0.35f, 30f), matDarkStone, FloorLayer, true);
        var ceilingRenderer = ceiling.GetComponent<Renderer>();
        if (ceilingRenderer != null)
            ceilingRenderer.enabled = false;

        // Wall-combo / boundary stress area: one readable broken-wall face, still using simple boxes.
        CreateCube("Obstacle_WallCombo_Backplate", geometry.transform, new Vector3(0f, 2.8f, 13.15f), new Vector3(12f, 5.6f, 0.8f), matWall, ObstacleLayer, true);
        CreateCube("Obstacle_WallCombo_LeftBreak", geometry.transform, new Vector3(-5.6f, 4.1f, 12.75f), new Vector3(2.2f, 2.4f, 0.9f), matDarkStone, ObstacleLayer, true);
        CreateCube("Obstacle_WallCombo_RightBreak", geometry.transform, new Vector3(5.6f, 3.6f, 12.75f), new Vector3(2.2f, 3.0f, 0.9f), matDarkStone, ObstacleLayer, true);

        // Visual dressing beyond the combat boundary. Colliders are disabled or removed.
        CreateVisualPillar("Dressing_FarPillar_NW", dressing.transform, new Vector3(-11.5f, 2.3f, 11.5f), matAccent);
        CreateVisualPillar("Dressing_FarPillar_NE", dressing.transform, new Vector3(11.5f, 2.3f, 11.5f), matAccent);
        CreateVisualPillar("Dressing_FarPillar_SW", dressing.transform, new Vector3(-11.5f, 2.3f, -11.5f), matAccent);
        CreateVisualPillar("Dressing_FarPillar_SE", dressing.transform, new Vector3(11.5f, 2.3f, -11.5f), matAccent);

        CreateCube("Dressing_BrokenGate_TopVisual", dressing.transform, new Vector3(0f, 6f, 15.1f), new Vector3(9.5f, 1.2f, 0.8f), matDarkStone, DefaultLayer, false);
        CreateCube("Dressing_BrokenGate_LeftVisual", dressing.transform, new Vector3(-5.2f, 3.0f, 15.1f), new Vector3(1.2f, 6f, 0.8f), matDarkStone, DefaultLayer, false);
        CreateCube("Dressing_BrokenGate_RightVisual", dressing.transform, new Vector3(5.2f, 3.0f, 15.1f), new Vector3(1.2f, 6f, 0.8f), matDarkStone, DefaultLayer, false);

        CreateBrazier("Dressing_FireBrazier_NW", dressing.transform, new Vector3(-9f, 0.45f, 10.5f), matDarkStone, matFire, lighting.transform);
        CreateBrazier("Dressing_FireBrazier_NE", dressing.transform, new Vector3(9f, 0.45f, 10.5f), matDarkStone, matFire, lighting.transform);
        CreateBrazier("Dressing_FireBrazier_SW", dressing.transform, new Vector3(-9f, 0.45f, -10.5f), matDarkStone, matFire, lighting.transform);
        CreateBrazier("Dressing_FireBrazier_SE", dressing.transform, new Vector3(9f, 0.45f, -10.5f), matDarkStone, matFire, lighting.transform);

        var playerRef = CreateEmpty("PlayerStart_Reference_KeepExistingPlayer", gameplay.transform);
        playerRef.transform.position = new Vector3(0f, 0f, -7f);
        playerRef.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        var bossRef = CreateEmpty("BossStart_Reference_KeepExistingBoss", gameplay.transform);
        bossRef.transform.position = new Vector3(0f, 0f, 7f);
        bossRef.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CreateDebugMarker("Debug_ArenaRadius_12m", debug.transform, new Vector3(0f, 0.05f, 0f), new Vector3(24f, 0.02f, 24f));
        CreateDebugMarker("Debug_CombatBoundary_14m", debug.transform, new Vector3(0f, 0.08f, 0f), new Vector3(28f, 0.02f, 28f));

        Debug.Log("ReleaseArenaBaker: Baked BossArena_RuinedSanctum environment into " + scene.path);
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, string layerName, bool colliderEnabled)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.tag = "Untagged";
        SafeSetLayer(go, layerName);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            if (colliderEnabled)
                collider.enabled = true;
            else
                Object.DestroyImmediate(collider);
        }

        return go;
    }

    private static GameObject CreateOctagonalPrism(string name, Transform parent, float radius, float height, Vector3 position, Material material, string layerName)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.tag = "Untagged";
        SafeSetLayer(go, layerName);

        var mesh = new Mesh { name = name + "_Mesh" };
        var vertices = new Vector3[18];
        vertices[0] = new Vector3(0f, height * 0.5f, 0f);
        vertices[9] = new Vector3(0f, -height * 0.5f, 0f);

        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Deg2Rad * (22.5f + i * 45f);
            var x = Mathf.Cos(angle) * radius;
            var z = Mathf.Sin(angle) * radius;
            vertices[1 + i] = new Vector3(x, height * 0.5f, z);
            vertices[10 + i] = new Vector3(x, -height * 0.5f, z);
        }

        var triangles = new int[96];
        var t = 0;
        for (var i = 0; i < 8; i++)
        {
            var next = (i + 1) % 8;
            triangles[t++] = 0;
            triangles[t++] = 1 + i;
            triangles[t++] = 1 + next;

            triangles[t++] = 9;
            triangles[t++] = 10 + next;
            triangles[t++] = 10 + i;

            triangles[t++] = 1 + i;
            triangles[t++] = 10 + i;
            triangles[t++] = 10 + next;

            triangles[t++] = 1 + i;
            triangles[t++] = 10 + next;
            triangles[t++] = 1 + next;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = false;

        return go;
    }

    private static void CreateVisualPillar(string name, Transform parent, Vector3 position, Material material)
    {
        var root = CreateEmpty(name, parent);
        root.transform.position = Vector3.zero;
        CreateCube(name + "_Base", root.transform, position + new Vector3(0f, -1.75f, 0f), new Vector3(1.6f, 0.5f, 1.6f), material, DefaultLayer, false);
        CreateCube(name + "_Shaft", root.transform, position + new Vector3(0f, 0f, 0f), new Vector3(1.0f, 4.0f, 1.0f), material, DefaultLayer, false);
        CreateCube(name + "_Cap", root.transform, position + new Vector3(0f, 2.2f, 0f), new Vector3(1.8f, 0.5f, 1.8f), material, DefaultLayer, false);
    }

    private static void CreateBrazier(string name, Transform visualParent, Vector3 position, Material stoneMaterial, Material fireMaterial, Transform lightParent)
    {
        var root = CreateEmpty(name, visualParent);
        root.transform.position = Vector3.zero;
        CreateCube(name + "_StoneBase", root.transform, position, new Vector3(1.2f, 0.7f, 1.2f), stoneMaterial, DefaultLayer, false);
        CreateCube(name + "_FireGlow", root.transform, position + Vector3.up * 0.55f, new Vector3(0.65f, 0.65f, 0.65f), fireMaterial, DefaultLayer, false);

        var lightGo = new GameObject(name + "_PointLight");
        lightGo.transform.SetParent(lightParent, true);
        lightGo.transform.position = position + Vector3.up * 1.25f;
        lightGo.tag = "Untagged";
        SafeSetLayer(lightGo, DefaultLayer);

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 7f;
        light.intensity = 1.4f;
        light.color = new Color(1f, 0.55f, 0.28f);
        light.shadows = LightShadows.None;
    }

    private static void CreateDebugMarker(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        var material = MakeMaterial("BossArena_Mat_Debug", new Color(0f, 1f, 1f, 0.2f));
        var go = CreateCube(name, parent, position, scale, material, DefaultLayer, false);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.name = name;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    private static void EnableEmission(Material material, Color emissionColor)
    {
        if (material == null)
            return;

        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emissionColor);
    }

    private static void SafeSetLayer(GameObject go, string layerName)
    {
        var layer = LayerMask.NameToLayer(layerName);
        go.layer = layer >= 0 ? layer : 0;
    }

    private static void EnsureLayers()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            return;

        var tagManager = new SerializedObject(assets[0]);
        EnsureLayer(tagManager, 8, GroundLayer);
        EnsureLayer(tagManager, 9, FloorLayer);
        EnsureLayer(tagManager, 10, ObstacleLayer);
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureLayer(SerializedObject tagManager, int layerIndex, string layerName)
    {
        var layers = tagManager.FindProperty("layers");
        if (layerIndex < 0 || layerIndex >= layers.arraySize)
            return;

        var layer = layers.GetArrayElementAtIndex(layerIndex);
        if (string.IsNullOrEmpty(layer.stringValue))
            layer.stringValue = layerName;
    }
}
