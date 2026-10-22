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

// Keep the original 1D numeric values: existing assets need no rewrite.
public enum LocomotionMoveParameter
{
    HorizontalSpeed = 0,
    VerticalSpeed = 1,
    InputStrength = 2,
    LocalInputX = 3,
    LocalInputZ = 4,
    LocalVelocityX = 5,
    LocalVelocityZ = 6,
    TurnLean = 7,
}

public enum LocomotionMove2DAlgorithm
{
    Directional = 0,
    Cartesian = 1,
}

[Serializable]
public sealed class LocomotionMoveDefinition
{
    [SerializeField] private LocomotionMoveBlendType blendType;
    [SerializeField] private LocomotionMove1DDefinition oneDimensional = new();
    [SerializeField] private LocomotionMove2DDefinition twoDimensional = new();
    [SerializeField] private LocomotionTurnLeanConfig turnLean = LocomotionTurnLeanConfig.Default;

    public LocomotionTurnLeanConfig TurnLean => turnLean;
    public LocomotionMoveBlendType BlendType => blendType;
    public LocomotionMove1DDefinition OneDimensional => oneDimensional;
    public LocomotionMove2DDefinition TwoDimensional => twoDimensional;
}

[Serializable]
public sealed class LocomotionMove1DDefinition
{
    [SerializeField, Tooltip("InputStrength: 0–1; local input: -1–1; speed/velocity: m/s; TurnLean: -1 left, +1 right.")]
    private LocomotionMoveParameter parameter;
    [SerializeField] private List<LocomotionMove1DSample> samples = new();

    public LocomotionMoveParameter Parameter => parameter;
    public IReadOnlyList<LocomotionMove1DSample> Samples => samples;
}

[Serializable]
public sealed class LocomotionMove2DDefinition
{
    // Read-only compatibility with old LocalVelocity=0 / LocalInput=1 assets.
    // Only an explicit axis edit in the Inspector opts into independent axes.
    [SerializeField, HideInInspector] private int parameter;
    [SerializeField, HideInInspector] private bool axesConfigured;
    [SerializeField] private LocomotionMoveParameter xParameter = LocomotionMoveParameter.LocalVelocityX;
    [SerializeField] private LocomotionMoveParameter yParameter = LocomotionMoveParameter.LocalVelocityZ;
    [SerializeField] private LocomotionMove2DAlgorithm algorithm;
    [SerializeField] private List<LocomotionMove2DSample> samples = new();

    public LocomotionMoveParameter XParameter => axesConfigured ? xParameter : ResolveLegacyAxis(parameter, true);
    public LocomotionMoveParameter YParameter => axesConfigured ? yParameter : ResolveLegacyAxis(parameter, false);
    public LocomotionMove2DAlgorithm Algorithm => algorithm;
    public IReadOnlyList<LocomotionMove2DSample> Samples => samples;

    // Shared with the Inspector so legacy data has one interpretation without rewriting assets.
    internal static LocomotionMoveParameter ResolveLegacyAxis(int parameter, bool x) => parameter == 0
        ? (x ? LocomotionMoveParameter.LocalVelocityX : LocomotionMoveParameter.LocalVelocityZ)
        : parameter == 1
            ? (x ? LocomotionMoveParameter.LocalInputX : LocomotionMoveParameter.LocalInputZ)
            : (LocomotionMoveParameter)(-1);
}

[Serializable]
public struct LocomotionTurnLeanConfig
{
    [Tooltip("Lateral model acceleration producing full lean (m/s²). Must be positive.")]
    public float FullLeanAcceleration;
    [Tooltip("Target lean is zero below this model speed (m/s); full speed influence at twice this speed.")]
    public float MinimumSpeed;
    [Tooltip("Exponential response time in motion seconds. Zero responds immediately.")]
    public float SmoothSeconds;

    public static LocomotionTurnLeanConfig Default => new LocomotionTurnLeanConfig
    {
        FullLeanAcceleration = 20f,
        MinimumSpeed = 0.5f,
        SmoothSeconds = 0.1f,
    };

    public bool IsValid => LocomotionDataValidation.IsFinite(FullLeanAcceleration) && FullLeanAcceleration > 0f
        && LocomotionDataValidation.IsFinite(MinimumSpeed) && MinimumSpeed >= 0f
        && LocomotionDataValidation.IsFinite(SmoothSeconds) && SmoothSeconds >= 0f;
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
