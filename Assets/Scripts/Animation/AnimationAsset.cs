using UnityEngine;

/// <summary>
/// Reusable Action/Locomotion resource. Runtime reads clips, trajectories and
/// confirmed locomotion metadata; bake inputs exist only in the Unity Editor.
/// </summary>
[CreateAssetMenu(fileName = "AnimationAsset", menuName = "CombatSample/Animation/Animation")]
public sealed class AnimationAsset : ScriptableObject
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, HideInInspector] private bool _hasRootMotionData;
    [SerializeField, HideInInspector] private RootMotionTrajectory _rootMotionData = new RootMotionTrajectory();
    [SerializeField] private LocomotionAnimationData _locomotionData = new LocomotionAnimationData();

    public AnimationClip Clip => _clip;
    public RootMotionTrajectory RootMotionData => _hasRootMotionData ? _rootMotionData : null;
    public LocomotionAnimationData LocomotionData => _locomotionData;

    public bool IsLocomotionDataCurrent
    {
        get
        {
            if (_locomotionData == null || !_locomotionData.MatchesClip(_clip)) return false;
#if UNITY_EDITOR
            return _locomotionData.SourceHash == LocomotionDataSourceHash.Compute(this);
#else
            return true;
#endif
        }
    }

#if UNITY_EDITOR
    [Header("Root Motion Bake Context")]
    [SerializeField] private AnimationRigAsset _animationRigAsset;

    public AnimationRigAsset AnimationRigAsset => _animationRigAsset;
    public RootMotionBakeSettings RootMotionBakeSettings => _animationRigAsset != null
        ? _animationRigAsset.CreateEffectiveBakeSettings()
        : null;

    public void EditorSetClip(AnimationClip clip) => _clip = clip;
    public void EditorSetLocomotionData(LocomotionAnimationData data) => _locomotionData = data;

    public void EditorSetAnimationRigAsset(AnimationRigAsset animationRigAsset)
    {
        _animationRigAsset = animationRigAsset;
    }

    public void EditorSetRootMotionData(RootMotionTrajectory rootMotionData)
    {
        _hasRootMotionData = rootMotionData != null;
        _rootMotionData = rootMotionData ?? new RootMotionTrajectory();
    }
#endif
}
