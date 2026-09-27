using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>One locked Motion tick, followed by the animation clock for that same tick.</summary>
public readonly struct LocomotionRuntimeAnimationContext
{
    public LocomotionRuntimeAnimationContext(LocomotionIntent intent, bool hasIntent,
        Vector3 velocityBeforeMotion, Vector3 modelVelocity, LocomotionMotionContext motor,
        float verticalSpeed, int actionOwnerId, float deltaTime)
    {
        Intent = intent;
        HasIntent = hasIntent;
        VelocityBeforeMotion = velocityBeforeMotion;
        ModelVelocity = modelVelocity;
        Motor = motor;
        VerticalSpeed = verticalSpeed;
        ActionOwnerId = actionOwnerId;
        DeltaTime = deltaTime;
    }

    public LocomotionIntent Intent { get; }
    public bool HasIntent { get; }
    public Vector3 VelocityBeforeMotion { get; }
    public Vector3 ModelVelocity { get; }
    public LocomotionMotionContext Motor { get; }
    public float VerticalSpeed { get; }
    public int ActionOwnerId { get; }
    public float DeltaTime { get; }
    public bool HasMovingInput => HasIntent && LocomotionDataValidation.IsFinite(Intent.MoveStrength)
        && Intent.MoveStrength > 0.01f && LocomotionAnimationUtility.IsValidPlanarDirection(Intent.WorldMoveDirection);

    public Vector3 PolicyVelocity => ModelVelocity * Motor.MotionState.LocomotionScale
        * (Motor.IsGrounded ? 1f : Motor.MotionState.AirLocomotionScale);

    public Vector2 ToLocalDirection(Vector3 worldDirection)
    {
        Vector3 local = Quaternion.Inverse(Motor.CurrentWorldRotation) * worldDirection;
        return new Vector2(local.x, local.z).normalized;
    }
}

/// <summary>A Layer 0 command. A missing target asks for pose protection, never an empty layer.</summary>
public readonly struct LocomotionAnimationRequest
{
    public LocomotionAnimationRequest(AnimancerState state, float blendDuration = 0f,
        bool restart = false, bool isMove = false, Vector2 parameter = default,
        AnimationClip idleClip = null, LocomotionMovePlayback movePlayback = null,
        LocomotionRuntimeAnimationContext playbackContext = default, float? entryPhase = null)
    {
        State = state;
        BlendDuration = blendDuration;
        Restart = restart;
        IsMove = isMove;
        Parameter = parameter;
        IdleClip = idleClip;
        MovePlayback = movePlayback;
        PlaybackContext = playbackContext;
        EntryPhase = entryPhase;
    }

    public AnimancerState State { get; }
    public float BlendDuration { get; }
    public bool Restart { get; }
    public bool IsMove { get; }
    public Vector2 Parameter { get; }
    public AnimationClip IdleClip { get; }
    public LocomotionMovePlayback MovePlayback { get; }
    public LocomotionRuntimeAnimationContext PlaybackContext { get; }
    public float? EntryPhase { get; }
}

public readonly struct ActorAnimationLocomotionOwner
{
    internal ActorAnimationLocomotionOwner(int id) => Id = id;
    internal int Id { get; }
    public bool IsValid => Id != 0;
}

internal static class LocomotionAnimationUtility
{
    internal const float WeightEpsilon = 0.0001f;

    internal static bool IsValidPlanarDirection(Vector3 direction) =>
        LocomotionDataValidation.IsFinite(direction.x) && LocomotionDataValidation.IsFinite(direction.z)
        && direction.x * direction.x + direction.z * direction.z > 0.0001f;

    internal static bool IsUsableClip(AnimationClip clip) => clip != null
        && LocomotionDataValidation.IsFinite(clip.length) && clip.length > 0f;

    internal static bool WasGraphDestroyed(AnimancerState state) =>
        state != null && state.Graph != null && !state.Playable.IsValid();

    internal static void Destroy(AnimancerState state)
    {
        // Graph destruction already removed its native nodes; do not touch stale layer connections.
        if (state != null && !WasGraphDestroyed(state))
            state.Destroy();
    }
}
