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
    public LocomotionMovementConfig MovementConfig => movementConfig;
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

    public abstract LocomotionRuntime CreateRuntime();

    protected virtual bool ValidateSpecificRuntimeConfig() => true;
}

[Serializable]
public struct LocomotionMovementConfig
{
    [FormerlySerializedAs("MoveSpeed")] public float MaxSpeed;
    [FormerlySerializedAs("GroundAcceleration")] public float Acceleration;
    [FormerlySerializedAs("GroundDeceleration")] public float Deceleration;
    [Tooltip("Velocity steering strength while moving (1/s). Higher values align velocity with input sooner. Zero disables steering assistance. Independent of facing rotation and no-input braking.")]
    public float DirectionResponse;
    public float RotateSpeed;
    public float TurnResponseTime;

    public static LocomotionMovementConfig Default => new LocomotionMovementConfig
    {
        MaxSpeed = 5f,
        Acceleration = 20f,
        Deceleration = 32f,
        DirectionResponse = 12f,
        RotateSpeed = 600f,
        TurnResponseTime = 0.08f,
    };

    public bool IsValid => IsFiniteNonNegative(MaxSpeed)
        && IsFiniteNonNegative(Acceleration) && IsFiniteNonNegative(Deceleration)
        && IsFiniteNonNegative(DirectionResponse)
        && IsFiniteNonNegative(RotateSpeed) && IsFiniteNonNegative(TurnResponseTime);

    public LocomotionMovementConfig Sanitize()
    {
        // Preserve the existing API, but never substitute unrelated default movement rules.
        if (!IsValid)
            throw new ArgumentException("Locomotion movement configuration is invalid.");
        return this;
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
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
    InputStrength = 2,
}

public enum LocomotionMove2DParameter
{
    LocalVelocity = 0,
    LocalInput = 1,
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
}

[Serializable]
public sealed class LocomotionMove1DDefinition
{
    [SerializeField] private LocomotionMove1DParameter parameter;
    [SerializeField] private List<LocomotionMove1DSample> samples = new();

    public LocomotionMove1DParameter Parameter => parameter;
    public IReadOnlyList<LocomotionMove1DSample> Samples => samples;
}

[Serializable]
public sealed class LocomotionMove2DDefinition
{
    [SerializeField] private LocomotionMove2DParameter parameter;
    [SerializeField] private List<LocomotionMove2DSample> samples = new();

    public LocomotionMove2DParameter Parameter => parameter;
    public IReadOnlyList<LocomotionMove2DSample> Samples => samples;
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
    internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}
