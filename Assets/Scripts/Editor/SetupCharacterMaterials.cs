using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SetupCharacterMaterials : EditorWindow
{
    [MenuItem("Tools/Setup Character Materials")]
    public static void Run()
    {
        SetupKiana();
        SetupStrikeJaeger();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character materials setup complete.");
    }

    static void SetupKiana()
    {
        string modelPath = "Assets/Resources/Models/Kiana/Avatar_Kiana_C2_Model.FBX";
        string matDir = "Assets/Resources/Models/Kiana/Material";
        string texDir = "Assets/Resources/Textures/Kiana";

        var materials = ExtractMaterials(modelPath, matDir);
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning("Kiana: No materials extracted.");
            return;
        }

        // Load all textures
        var bodyColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Avatar_Kiana_C2_Texture_Body&Hair_Color.png");
        var bodyLightMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Avatar_Kiana_C2_Texture_Body&Hair_LightMap.png");
        var faceColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Avatar_Kiana_Texture_Face_Color.png");
        var faceLightMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Avatar_Kiana_Texture_Face_LightMap.png");
        var faceMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Avatar_Kiana_FaceMap.png");
        var eye = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Eye.png");
        var mouth = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/Mouth.png");

        foreach (var mat in materials)
        {
            string matName = mat.name.ToLower();

            if (matName.Contains("body") || matName.Contains("hair"))
            {
                if (bodyColor != null) mat.SetTexture("_MainTex", bodyColor);
                if (bodyColor != null) mat.SetTexture("_BaseMap", bodyColor);
                if (bodyLightMap != null) mat.SetTexture("_EmissionMap", bodyLightMap);
            }
            else if (matName.Contains("face"))
            {
                if (faceColor != null) mat.SetTexture("_MainTex", faceColor);
                if (faceColor != null) mat.SetTexture("_BaseMap", faceColor);
                if (faceLightMap != null) mat.SetTexture("_EmissionMap", faceLightMap);
                if (faceMap != null) mat.SetTexture("_DetailAlbedoMap", faceMap);
            }
            else if (matName.Contains("eye"))
            {
                if (eye != null) mat.SetTexture("_MainTex", eye);
                if (eye != null) mat.SetTexture("_BaseMap", eye);
            }
            else if (matName.Contains("mouth"))
            {
                if (mouth != null) mat.SetTexture("_MainTex", mouth);
                if (mouth != null) mat.SetTexture("_BaseMap", mouth);
            }
            else
            {
                // Fallback: try body color
                if (bodyColor != null) mat.SetTexture("_MainTex", bodyColor);
                if (bodyColor != null) mat.SetTexture("_BaseMap", bodyColor);
            }

            EditorUtility.SetDirty(mat);
        }

        Debug.Log($"Kiana: {materials.Length} materials configured.");
    }

    static void SetupStrikeJaeger()
    {
        string modelPath = "Assets/Resources/Models/StrikeJaeger/Monster_StrikeJaeger_Model.fbx";
        string matDir = "Assets/Resources/Models/StrikeJaeger/Material";
        string texDir = "Assets/Resources/Textures/StrikeJaeger";

        var materials = ExtractMaterials(modelPath, matDir);
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning("StrikeJaeger: No materials extracted.");
            return;
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/StrikeJaeger_A.tga");
        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/StrikeJaeger_M.tga");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/StrikeJaeger_N.tga");
        var detail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/StrikeJaeger_D.tga");

        foreach (var mat in materials)
        {
            if (albedo != null)
            {
                mat.SetTexture("_MainTex", albedo);
                mat.SetTexture("_BaseMap", albedo);
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                // For HDRP/Lit shader
                mat.SetTexture("_MaskMap", metallic);
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTexture("_NormalMap", normal);
            }
            if (detail != null)
            {
                mat.SetTexture("_DetailAlbedoMap", detail);
                mat.SetTexture("_DetailMap", detail);
            }

            EditorUtility.SetDirty(mat);
        }

        Debug.Log($"StrikeJaeger: {materials.Length} materials configured.");
    }

    static Material[] ExtractMaterials(string modelPath, string matDir)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Cannot find ModelImporter at {modelPath}");
            return null;
        }

        if (!Directory.Exists(matDir))
            Directory.CreateDirectory(matDir);

        // Get embedded material names
        var embeddedNames = new List<string>();
        var remap = importer.GetExternalObjectMap();

        // Try extracting first
        string[] extractedPaths = null;
        try
        {
            extractedPaths = importer.ExtractMaterials(matDir);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ExtractMaterials failed: {e.Message}. Trying alternate approach.");
        }

        // If extraction returned paths, load them
        if (extractedPaths != null && extractedPaths.Length > 0)
        {
            var mats = new List<Material>();
            foreach (var p in extractedPaths)
            {
                // Sometimes Unity returns the correct path but the asset isn't ready yet
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat != null)
                    mats.Add(mat);
            }
            if (mats.Count > 0)
                return mats.ToArray();
        }

        // Fallback: scan the material directory
        AssetDatabase.Refresh();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { matDir });
        if (guids.Length > 0)
        {
            var mats = new Material[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return mats;
        }

        return new Material[0];
    }
}
