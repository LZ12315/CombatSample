using UnityEngine;

/// <summary>
/// Reusable Action/Locomotion resource. Runtime reads clips and baked Root Motion;
/// bake inputs exist only in the Unity Editor.
/// </summary>
[CreateAssetMenu(fileName = "AnimationAsset", menuName = "CombatSample/Animation/Animation")]
public sealed class AnimationAsset : ScriptableObject
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, HideInInspector] private bool _hasRootMotionData;
    [SerializeField, HideInInspector] private RootMotionTrajectory _rootMotionData = new RootMotionTrajectory();

    public AnimationClip Clip => _clip;
    public RootMotionTrajectory RootMotionData => _hasRootMotionData ? _rootMotionData : null;

    internal bool TryGetLocomotionTrajectory(out RootMotionTrajectory trajectory, out string reason)
    {
        trajectory = RootMotionData;
        reason = "Valid source-matched Root Motion trajectory is required for speed matching.";
        if (trajectory == null || _clip == null || trajectory.SourceClip != _clip
            || Mathf.Abs(trajectory.Duration - _clip.length) > 1e-5f
            || trajectory.BakerVersion != RootMotionTrajectory.CurrentBakerVersion
            || string.IsNullOrEmpty(trajectory.DependencyHash) || !trajectory.ValidateData().IsValid)
            return false;
#if UNITY_EDITOR
        // Validate authoring dependencies once at binding. Player consumes the baked resource.
        var settings = RootMotionBakeSettings;
        if (!RootMotionBakeSource.TryCompute(_clip, settings, out string hash, out reason)) return false;
        if (!trajectory.MatchesSource(_clip, settings.SampleRate, RootMotionTrajectory.CurrentBakerVersion, hash))
        {
            reason = "Root Motion dependencies changed. Rebuild before using Move speed matching.";
            return false;
        }
#endif
        reason = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    [Header("Root Motion Bake Context")]
    [SerializeField] private AnimationRigAsset _animationRigAsset;

    public AnimationRigAsset AnimationRigAsset => _animationRigAsset;
    public RootMotionBakeSettings RootMotionBakeSettings => _animationRigAsset != null
        ? _animationRigAsset.CreateEffectiveBakeSettings()
        : null;

    public void EditorSetClip(AnimationClip clip)
    {
        _clip = clip;
    }

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
