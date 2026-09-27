using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Immutable locomotion configuration and per-Actor runtime factory.
/// Concrete assets decide their own runtime behavior; ActorLocomotion never switches on their type.
/// </summary>
public abstract class LocomotionAsset : ScriptableObject
{
    [SerializeField] private int priority;
    [SerializeReference, SubclassSelector] private List<LocomotionModeCondition> entryConditions = new();
    [SerializeField] private List<TagReference> selfTags = new();
    [SerializeField] private LocomotionMovementConfig movementConfig = LocomotionMovementConfig.Default;
    [SerializeField] private LocomotionMoveDefinition move = new();

    public int Priority => priority;
    public IReadOnlyList<LocomotionModeCondition> EntryConditions => entryConditions;
    public IReadOnlyList<TagReference> SelfTags => selfTags;
    public LocomotionMovementConfig MovementConfig => movementConfig.Sanitize();
    public LocomotionMoveDefinition Move => move;
    public bool HasValidMovementConfig => movementConfig.IsValid;
    public bool HasValidRuntimeConfig => movementConfig.IsValid && ValidateSpecificRuntimeConfig();

    public bool AreEntryConditionsMet(in LocomotionSelectionContext context)
    {
        if (entryConditions == null)
            return true;

        for (int i = 0; i < entryConditions.Count; i++)
        {
            LocomotionModeCondition condition = entryConditions[i];
            if (condition == null || !condition.Check(context))
                return false;
        }

        return true;
    }

    public bool ValidateRuntime(UnityEngine.Object context, ref bool warned)
    {
        bool valid = HasValidRuntimeConfig && !HasNullEntryCondition() && HasValidSelfTags();
        if (!valid && !warned)
        {
            Debug.LogWarning($"[ActorLocomotion] Invalid LocomotionAsset '{name}'.", context);
            warned = true;
        }

        return valid;
    }

    public void CollectAnimationCoverageIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        if (move == null)
            issues.Add("Move definition is missing.");
        else
            move.CollectCoverageIssues(issues);

        CollectSpecificAnimationCoverageIssues(issues);
    }

    public abstract LocomotionRuntime CreateRuntime();

    protected virtual bool ValidateSpecificRuntimeConfig() => true;
    protected virtual void CollectSpecificAnimationCoverageIssues(List<string> issues) { }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (HasNullEntryCondition())
            Debug.LogWarning($"[ActorLocomotion] LocomotionAsset '{name}' contains a null EntryCondition.", this);
    }
#endif

    private bool HasNullEntryCondition()
    {
        if (entryConditions == null)
            return false;

        for (int i = 0; i < entryConditions.Count; i++)
            if (entryConditions[i] == null)
                return true;

        return false;
    }

    private bool HasValidSelfTags()
    {
        if (selfTags == null)
            return true;

        for (int i = 0; i < selfTags.Count; i++)
            if (selfTags[i] == null || selfTags[i].GetTag() == null)
                return false;

        return true;
    }
}

[Serializable]
public struct LocomotionMovementConfig
{
    [FormerlySerializedAs("MoveSpeed")] public float MaxSpeed;
    [FormerlySerializedAs("GroundAcceleration")] public float Acceleration;
    [FormerlySerializedAs("GroundDeceleration")] public float Deceleration;
    public float RotateSpeed;
    public float TurnResponseTime;

    public static LocomotionMovementConfig Default => new LocomotionMovementConfig
    {
        MaxSpeed = 5f,
        Acceleration = 20f,
        Deceleration = 32f,
        RotateSpeed = 600f,
        TurnResponseTime = 0.08f,
    };

    public bool IsValid => IsFiniteNonNegative(MaxSpeed)
        && IsFinitePositive(Acceleration) && IsFinitePositive(Deceleration)
        && IsFinitePositive(RotateSpeed) && IsFiniteNonNegative(TurnResponseTime);

    public LocomotionMovementConfig Sanitize()
    {
        LocomotionMovementConfig fallback = Default;
        return new LocomotionMovementConfig
        {
            MaxSpeed = IsFiniteNonNegative(MaxSpeed) ? MaxSpeed : fallback.MaxSpeed,
            Acceleration = IsFinitePositive(Acceleration) ? Acceleration : fallback.Acceleration,
            Deceleration = IsFinitePositive(Deceleration) ? Deceleration : fallback.Deceleration,
            RotateSpeed = IsFinitePositive(RotateSpeed) ? RotateSpeed : fallback.RotateSpeed,
            TurnResponseTime = IsFiniteNonNegative(TurnResponseTime) ? TurnResponseTime : fallback.TurnResponseTime,
        };
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsFinitePositive(float value) => IsFiniteNonNegative(value) && value > 0f;
}

public enum LocomotionMoveBlendType
{
    OneDimensional = 0,
    TwoDimensional = 1,
}

public enum LocomotionMove1DParameter
{
    HorizontalSpeed = 0,
    VerticalSpeed = 1,
}

[Serializable]
public sealed class LocomotionMoveDefinition
{
    [SerializeField] private LocomotionMoveBlendType blendType;
    [SerializeField] private LocomotionMove1DDefinition oneDimensional = new();
    [SerializeField] private LocomotionMove2DDefinition twoDimensional = new();

    public LocomotionMoveBlendType BlendType => blendType;
    public LocomotionMove1DDefinition OneDimensional => oneDimensional;
    public LocomotionMove2DDefinition TwoDimensional => twoDimensional;

    public void CollectPlaybackIssues(List<string> issues)
    {
        if (!Enum.IsDefined(typeof(LocomotionMoveBlendType), blendType))
            issues.Add("Move blend type is invalid.");
        else if (blendType == LocomotionMoveBlendType.OneDimensional)
        {
            if (oneDimensional == null)
                issues.Add("Move 1D definition is missing.");
            else
                oneDimensional.CollectCoverageIssues(issues, false);
        }
        else if (twoDimensional == null)
            issues.Add("Move 2D definition is missing.");
        else
            twoDimensional.CollectCoverageIssues(issues, false);
    }

    public void CollectCoverageIssues(List<string> issues)
    {
        if (!Enum.IsDefined(typeof(LocomotionMoveBlendType), blendType))
        {
            issues.Add("Move blend type is invalid.");
            return;
        }

        if (blendType == LocomotionMoveBlendType.OneDimensional)
        {
            if (oneDimensional == null)
                issues.Add("Move 1D definition is missing.");
            else
                oneDimensional.CollectCoverageIssues(issues);
        }
        else
        {
            if (twoDimensional == null)
                issues.Add("Move 2D definition is missing.");
            else
                twoDimensional.CollectCoverageIssues(issues);
        }
    }
}

[Serializable]
public sealed class LocomotionMove1DDefinition
{
    [SerializeField] private LocomotionMove1DParameter parameter;
    [SerializeField] private List<LocomotionMove1DSample> samples = new();

    public LocomotionMove1DParameter Parameter => parameter;
    public IReadOnlyList<LocomotionMove1DSample> Samples => samples;

    public void CollectCoverageIssues(List<string> issues, bool checkTrajectory = true)
    {
        if (!Enum.IsDefined(typeof(LocomotionMove1DParameter), parameter))
            issues.Add("Move 1D parameter is invalid.");
        if (samples == null || samples.Count == 0)
        {
            issues.Add("Move 1D samples are missing.");
            return;
        }

        bool hasZero = false;
        for (int i = 0; i < samples.Count; i++)
        {
            LocomotionMove1DSample sample = samples[i];
            if (sample == null || !LocomotionDataValidation.IsFinite(sample.Threshold)
                || (parameter == LocomotionMove1DParameter.HorizontalSpeed && sample.Threshold < 0f))
            {
                issues.Add($"Move 1D[{i}] has an invalid threshold.");
            }
            else
            {
                hasZero |= Mathf.Abs(sample.Threshold) <= LocomotionDataValidation.ThresholdEpsilon;
                for (int j = 0; j < i; j++)
                {
                    LocomotionMove1DSample previous = samples[j];
                    if (previous != null && Mathf.Abs(previous.Threshold - sample.Threshold)
                        <= LocomotionDataValidation.ThresholdEpsilon)
                    {
                        issues.Add($"Move 1D[{i}] duplicates threshold {sample.Threshold}.");
                        break;
                    }
                }
            }

            bool requiresTrajectory = checkTrajectory && sample != null
                && Mathf.Abs(sample.Threshold) > LocomotionDataValidation.ThresholdEpsilon;
            LocomotionDataValidation.CheckAnimation(
                sample?.Animation, $"Move 1D[{i}]", issues, requiresTrajectory);
        }

        if (parameter == LocomotionMove1DParameter.HorizontalSpeed && !hasZero)
            issues.Add("HorizontalSpeed Move requires a zero-threshold Idle sample.");
    }
}

[Serializable]
public sealed class LocomotionMove2DDefinition
{
    [SerializeField] private List<LocomotionMove2DSample> samples = new();

    public IReadOnlyList<LocomotionMove2DSample> Samples => samples;

    public void CollectCoverageIssues(List<string> issues, bool checkTrajectory = true)
    {
        if (samples == null || samples.Count == 0)
        {
            issues.Add("Move 2D samples are missing.");
            return;
        }

        bool hasZero = false;
        for (int i = 0; i < samples.Count; i++)
        {
            LocomotionMove2DSample sample = samples[i];
            if (sample == null || !LocomotionDataValidation.IsFinite(sample.Threshold))
            {
                issues.Add($"Move 2D[{i}] has an invalid threshold.");
            }
            else
            {
                hasZero |= sample.Threshold.sqrMagnitude
                    <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon;
                for (int j = 0; j < i; j++)
                {
                    LocomotionMove2DSample previous = samples[j];
                    if (previous != null && (previous.Threshold - sample.Threshold).sqrMagnitude
                        <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon)
                    {
                        issues.Add($"Move 2D[{i}] duplicates threshold {sample.Threshold}.");
                        break;
                    }
                }
            }

            bool requiresTrajectory = checkTrajectory && sample != null && sample.Threshold.sqrMagnitude
                > LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon;
            LocomotionDataValidation.CheckAnimation(
                sample?.Animation, $"Move 2D[{i}]", issues, requiresTrajectory);
        }

        if (!hasZero)
            issues.Add("LocalVelocity Move requires a (0,0) Idle sample.");
    }
}

[Serializable]
public sealed class LocomotionMove1DSample
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private float threshold;
    [SerializeField] private bool sync = true;

    public AnimationAsset Animation => animation;
    public float Threshold => threshold;
    public bool Sync => sync;
}

[Serializable]
public sealed class LocomotionMove2DSample
{
    [SerializeField] private AnimationAsset animation;
    [SerializeField] private Vector2 threshold;
    [SerializeField] private bool sync = true;

    public AnimationAsset Animation => animation;
    public Vector2 Threshold => threshold;
    public bool Sync => sync;
}

internal static class LocomotionDataValidation
{
    internal const float ThresholdEpsilon = 0.0001f;

    internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

    internal static bool IsValidDirection(Vector2 direction) =>
        IsFinite(direction) && direction.sqrMagnitude > ThresholdEpsilon * ThresholdEpsilon;

    internal static void CheckAnimation(AnimationAsset asset, string label, List<string> issues,
        bool requiresTrajectory)
    {
        if (asset == null || asset.Clip == null)
        {
            issues.Add($"{label} AnimationAsset/Clip is missing.");
            return;
        }

        if (requiresTrajectory && (asset.RootMotionData == null || !asset.RootMotionData.ValidateData().IsValid
            || asset.RootMotionData.SourceClip != asset.Clip))
        {
            issues.Add($"{label} has no valid baked trajectory for its Clip.");
        }
    }
}
