using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CombatSample/Locomotion/Set", fileName = "LocomotionSet")]
public sealed class LocomotionSetAsset : LocomotionAsset
{
    [SerializeField, Min(0f)] private float transitionBlendDuration = 0.1f;
    [SerializeField] private LocomotionStopPlaybackMode stopPlaybackMode;
    [SerializeField] private LocomotionTransitionDecisionConfig transitionDecisionConfig = LocomotionTransitionDecisionConfig.Default;
    [SerializeField] private List<LocomotionStartEntry> start = new();
    [SerializeField] private List<LocomotionStopEntry> stop = new();
    [SerializeField] private List<LocomotionPivotEntry> pivot = new();

    public LocomotionStopPlaybackMode StopPlaybackMode => stopPlaybackMode;
    public float TransitionBlendDuration => transitionBlendDuration;
    public LocomotionTransitionDecisionConfig TransitionDecisionConfig => transitionDecisionConfig;
    public IReadOnlyList<LocomotionStartEntry> Start => start;
    public IReadOnlyList<LocomotionStopEntry> Stop => stop;
    public IReadOnlyList<LocomotionPivotEntry> Pivot => pivot;

    public override LocomotionRuntime CreateRuntime() => new LocomotionSetRuntime(this);

    protected override bool ValidateSpecificRuntimeConfig() =>
        LocomotionDataValidation.IsFinite(transitionBlendDuration) && transitionBlendDuration >= 0f
        && transitionDecisionConfig.IsValid;
}

[Serializable]
public struct LocomotionTransitionDecisionConfig
{
    [Tooltip("Speed threshold used by Start/Stop animation decisions (m/s).")]
    public float StationarySpeed;
    [Tooltip("Minimum model speed required to enter Pivot (m/s).")]
    public float PivotMinimumSpeed;
    [Tooltip("Minimum input reversal angle required to enter Pivot (degrees).")]
    public float PivotMinimumAngleDegrees;

    public static LocomotionTransitionDecisionConfig Default => new LocomotionTransitionDecisionConfig
    {
        StationarySpeed = 0.1f,
        PivotMinimumSpeed = 0.5f,
        PivotMinimumAngleDegrees = 120f,
    };

    public bool IsValid => LocomotionDataValidation.IsFinite(StationarySpeed) && StationarySpeed >= 0f
        && LocomotionDataValidation.IsFinite(PivotMinimumSpeed) && PivotMinimumSpeed >= 0f
        && LocomotionDataValidation.IsFinite(PivotMinimumAngleDegrees)
        && PivotMinimumAngleDegrees >= 0f && PivotMinimumAngleDegrees <= 180f;
}

[Serializable]
public sealed class LocomotionStartEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 targetLocalDirection = Vector2.up;

    public AnimationAsset Animation => animation;
    public Vector2 TargetLocalDirection => targetLocalDirection;
}

[Serializable]
public sealed class LocomotionStopEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 sourceLocalDirection = Vector2.up;

    public AnimationAsset Animation => animation;
    public Vector2 SourceLocalDirection => sourceLocalDirection;
}

[Serializable]
public sealed class LocomotionPivotEntry
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 sourceLocalDirection = Vector2.up;
    [SerializeField] private Vector2 targetLocalDirection = Vector2.down;

    public AnimationAsset Animation => animation;
    public Vector2 SourceLocalDirection => sourceLocalDirection;
    public Vector2 TargetLocalDirection => targetLocalDirection;
    public float SignedAngle => Vector2.SignedAngle(sourceLocalDirection, targetLocalDirection);
}

public enum LocomotionStopPlaybackMode { Time, Distance }
