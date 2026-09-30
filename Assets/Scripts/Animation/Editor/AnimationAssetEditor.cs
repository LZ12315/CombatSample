#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationAsset))]
public sealed class AnimationAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var asset = (AnimationAsset)target;
        AnimationAssetBakeStatus status = AnimationAssetBakeWorkflow.GetStatus(asset);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status.Message, ToMessageType(status.Code));

        using (new EditorGUI.DisabledScope(asset.Clip == null))
        {
            string label = status.Code == AnimationAssetBakeStatusCode.Missing ? "Bake Root Motion" : "Rebuild Root Motion";
            if (GUILayout.Button(label))
            {
                if (!AnimationAssetBakeWorkflow.TryBake(asset, out AnimationAssetBakeOperationResult result))
                    Debug.LogError($"[AnimationAsset Bake] {result.Message}", asset);
                else
                    Debug.Log($"[AnimationAsset Bake] {result.Message}", asset);
            }
        }
    }

    private static MessageType ToMessageType(AnimationAssetBakeStatusCode code)
    {
        return code switch
        {
            AnimationAssetBakeStatusCode.Ready => MessageType.Info,
            AnimationAssetBakeStatusCode.Missing => MessageType.Warning,
            _ => MessageType.Error,
        };
    }
}
#endif
