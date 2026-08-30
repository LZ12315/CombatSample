using UnityEngine;

/// <summary>
/// Reusable Action animation resource. Runtime reads only the clip and baked
/// trajectory; bake inputs exist only in the Unity Editor.
/// </summary>
[CreateAssetMenu(fileName = "AnimationAsset", menuName = "Combat/Animation/Animation Asset")]
public sealed class AnimationAsset : ScriptableObject
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, HideInInspector] private bool _hasRootMotionData;
    [SerializeField, HideInInspector] private RootMotionTrajectory _rootMotionData = new RootMotionTrajectory();

    public AnimationClip Clip => _clip;
    public RootMotionTrajectory RootMotionData => _hasRootMotionData ? _rootMotionData : null;

#if UNITY_EDITOR
    [Header("Root Motion Bake Context")]
    [SerializeField] private RootMotionBakeSettings _rootMotionBakeSettings = new RootMotionBakeSettings();

    public RootMotionBakeSettings RootMotionBakeSettings => _rootMotionBakeSettings;

    public void EditorSetClip(AnimationClip clip) => _clip = clip;

    public void EditorSetRootMotionData(RootMotionTrajectory rootMotionData)
    {
        _hasRootMotionData = rootMotionData != null;
        _rootMotionData = rootMotionData ?? new RootMotionTrajectory();
    }
#endif
}
