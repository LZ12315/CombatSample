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

    [SerializeField, HideInInspector] private AnimationLocomotionData _locomotionData;
    [SerializeField, HideInInspector] private AnimationClip _locomotionOverrideClip;
    [SerializeField, HideInInspector] private bool _overrideStopTime;
    [SerializeField, HideInInspector] private float _stopTimeOverride;
    [SerializeField, HideInInspector] private bool _overrideFootMarkers;
    [SerializeField, HideInInspector] private AnimationFootMarker[] _footMarkerOverrides = System.Array.Empty<AnimationFootMarker>();

    public AnimationLocomotionData LocomotionData => _locomotionData?.SourceClip == _clip ? _locomotionData : null;
    public bool HasStopTimeOverride => _locomotionOverrideClip == _clip && _overrideStopTime;
    public bool HasFootMarkerOverrides => _locomotionOverrideClip == _clip && _overrideFootMarkers;
    public float StopTime => HasStopTimeOverride ? _stopTimeOverride : LocomotionData?.StopTime ?? 0f;
    public System.Collections.Generic.IReadOnlyList<AnimationFootMarker> FootMarkers => HasFootMarkerOverrides
        ? _footMarkerOverrides : LocomotionData?.FootMarkers ?? System.Array.Empty<AnimationFootMarker>();

    internal bool TryCreateStopCurve(out AnimationStopDistanceCurve curve)
    {
        curve = null;
        return LocomotionData != null && TryGetLocomotionTrajectory(out var trajectory, out _)
            && TryCreateStopCurve(trajectory, out curve);
    }

    // The binding builder supplies an already qualified trajectory to all derived data.
    internal bool TryCreateStopCurve(RootMotionTrajectory trajectory, out AnimationStopDistanceCurve curve)
    {
        curve = null;
        return trajectory != null && LocomotionData != null
            && LocomotionData.TrajectoryHash == trajectory.DependencyHash
            && AnimationStopDistanceCurve.TryCreate(trajectory, StopTime, out curve);
    }

    internal AnimationFootPhaseTrack CreateFootPhaseTrack()
    {
        if (_clip == null) return null;
        RootMotionTrajectory trajectory = null;
        if (!HasFootMarkerOverrides && (LocomotionData == null
            || !TryGetLocomotionTrajectory(out trajectory, out _))) return null;
        return CreateFootPhaseTrack(trajectory);
    }

    internal AnimationFootPhaseTrack CreateFootPhaseTrack(RootMotionTrajectory trajectory)
    {
        if (_clip == null) return null;
        if (!HasFootMarkerOverrides)
        {
            var data = LocomotionData;
            if (data == null || trajectory == null
                || data.TrajectoryHash != trajectory.DependencyHash) return null;
#if UNITY_EDITOR
            if (data.FootSetup != RootMotionBakeSettings?.FootSetup) return null;
#endif
        }
        return new AnimationFootPhaseTrack(FootMarkers, _clip.length, _clip.isLooping);
    }

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

    public void EditorSetLocomotionData(AnimationLocomotionData data) => _locomotionData = data;

    public void EditorOverrideStopTime(bool enabled, float time)
    {
        BindOverridesToClip();
        _overrideStopTime = enabled;
        _stopTimeOverride = time;
    }

    public void EditorOverrideFootMarkers(bool enabled, AnimationFootMarker[] markers)
    {
        BindOverridesToClip();
        _overrideFootMarkers = enabled;
        _footMarkerOverrides = markers != null ? (AnimationFootMarker[])markers.Clone() : System.Array.Empty<AnimationFootMarker>();
    }

    private void BindOverridesToClip()
    {
        if (_locomotionOverrideClip == _clip) return;
        _locomotionOverrideClip = _clip;
        _overrideStopTime = _overrideFootMarkers = false;
        _footMarkerOverrides = System.Array.Empty<AnimationFootMarker>();
    }

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
