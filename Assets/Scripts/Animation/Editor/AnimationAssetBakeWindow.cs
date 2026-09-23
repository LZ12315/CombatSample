#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Small single-clip entry point for creating and baking AnimationAsset data.</summary>
public sealed class AnimationAssetBakeWindow : EditorWindow
{
    [SerializeField] private AnimationAsset _target;
    [SerializeField] private AnimationClip _clip;
    [SerializeField] private AnimationRigAsset _rig;

    [MenuItem("Tools/CombatSample/Animation/Bake Root Motion")]
    private static void Open()
    {
        GetWindow<AnimationAssetBakeWindow>("Animation Bake").Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Single AnimationAsset Bake", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Choose one clip and one shared rig context. Bake commits the configuration and RootMotionData together; a failed bake leaves the asset unchanged.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        _target = (AnimationAsset)EditorGUILayout.ObjectField("Animation Asset", _target, typeof(AnimationAsset), false);
        if (EditorGUI.EndChangeCheck() && _target != null)
        {
            _clip = _target.Clip;
            _rig = _target.AnimationRigAsset;
        }
        _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);
        _rig = (AnimationRigAsset)EditorGUILayout.ObjectField("Animation Rig", _rig, typeof(AnimationRigAsset), false);

        using (new EditorGUI.DisabledScope(_target == null && _clip == null))
        {
            if (GUILayout.Button(_target == null ? "Create AnimationAsset" : "Apply Clip And Rig"))
                ApplySetup();
        }

        using (new EditorGUI.DisabledScope(_target == null || _clip == null || _rig == null))
        {
            if (GUILayout.Button("Bake Root Motion"))
            {
                if (AnimationAssetBakeWorkflow.TryBake(_target, _clip, _rig, out AnimationAssetBakeOperationResult result))
                    Debug.Log($"[AnimationAsset Bake] {result.Message}", _target);
                else
                    Debug.LogError($"[AnimationAsset Bake] {result.Message}", _target);
            }
        }

        if (_target != null)
        {
            AnimationAssetBakeStatus status = AnimationAssetBakeWorkflow.GetStatus(_target);
            EditorGUILayout.HelpBox(status.Message,
                status.Code == AnimationAssetBakeStatusCode.Ready ? MessageType.Info :
                status.Code == AnimationAssetBakeStatusCode.Missing ? MessageType.Warning : MessageType.Error);
        }
    }

    private void ApplySetup()
    {
        if (_target == null)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create AnimationAsset", _clip != null ? _clip.name : "AnimationAsset", "asset", string.Empty);
            if (string.IsNullOrEmpty(path))
                return;
            _target = ScriptableObject.CreateInstance<AnimationAsset>();
            AssetDatabase.CreateAsset(_target, path);
        }

        Undo.RecordObject(_target, "Configure Animation Asset Bake");
        _target.EditorSetClip(_clip);
        _target.EditorSetAnimationRigAsset(_rig);
        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssetIfDirty(_target);
        Selection.activeObject = _target;
    }
}
#endif
