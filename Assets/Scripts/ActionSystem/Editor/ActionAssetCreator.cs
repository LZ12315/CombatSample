using System.IO;
using UnityEditor;
using UnityEngine;

public static class ActionAssetCreator
{
    public static ActionAsset CreateActionAsset(string path)
    {
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        AssetDatabase.CreateAsset(actionAsset, path);
        EditorUtility.SetDirty(actionAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return actionAsset;
    }

    [MenuItem("Assets/Create/CombatSample/Action/Action", priority = 100)]
    public static void CreateActionAsset()
    {
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateActionAssetCallback>(),
            "New Action",
            EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D,
            null);
    }

    private sealed class CreateActionAssetCallback : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string path, string resourceFile)
        {
            string finalAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.ChangeExtension(path, "asset").Replace("\\", "/"));
            ActionAsset asset = CreateActionAsset(finalAssetPath);
            if (asset != null)
                ProjectWindowUtil.ShowCreatedAsset(asset);
        }
    }
}
