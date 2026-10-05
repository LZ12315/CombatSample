using Animancer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>One locked Motion tick, followed by the animation clock for that same tick.</summary>
public readonly struct LocomotionRuntimeAnimationContext
{
    public LocomotionRuntimeAnimationContext(LocomotionIntent intent, bool hasIntent,
        Vector3 velocityBeforeMotion, Vector3 modelVelocity, LocomotionMotionContext motor,
        float verticalSpeed, int actionOwnerId, float deltaTime, LocomotionFootPhaseReference sourceFootPhase = default)
    {
        Intent = intent;
        HasIntent = hasIntent;
        VelocityBeforeMotion = velocityBeforeMotion;
        ModelVelocity = modelVelocity;
        Motor = motor;
        VerticalSpeed = verticalSpeed;
        ActionOwnerId = actionOwnerId;
        DeltaTime = deltaTime;
        SourceFootPhase = sourceFootPhase;
    }

    public LocomotionFootPhaseReference SourceFootPhase { get; }
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
        LocomotionRuntimeAnimationContext playbackContext = default, float? sampleTime = null,
        IReadOnlyList<LocomotionFootPhaseBinding> footPhaseBindings = null)
    {
        State = state;
        BlendDuration = blendDuration;
        Restart = restart;
        IsMove = isMove;
        Parameter = parameter;
        IdleClip = idleClip;
        MovePlayback = movePlayback;
        PlaybackContext = playbackContext;
        SampleTime = sampleTime;
        FootPhaseBindings = footPhaseBindings;
    }

    /// <summary>Captured leaf metadata retained by the playback owner across animation blends.</summary>
    public IReadOnlyList<LocomotionFootPhaseBinding> FootPhaseBindings { get; }
    /// <summary>Explicit pose time; disables the normal animation clock for this submission.</summary>
    public float? SampleTime { get; }
    public AnimancerState State { get; }
    public float BlendDuration { get; }
    public bool Restart { get; }
    public bool IsMove { get; }
    public Vector2 Parameter { get; }
    public AnimationClip IdleClip { get; }
    public LocomotionMovePlayback MovePlayback { get; }
    public LocomotionRuntimeAnimationContext PlaybackContext { get; }
}

/// <summary>Unknown is the default. A known phase belongs to the dominant evaluated locomotion leaf.</summary>
public readonly struct LocomotionFootPhaseReference
{
    public bool IsKnown { get; }
    public float Phase { get; }
    public LocomotionFootPhaseReference(float phase)
    {
        IsKnown = LocomotionDataValidation.IsFinite(phase);
        Phase = IsKnown ? Mathf.Repeat(phase, 1f) : 0f;
    }
}

/// <summary>Runtime-bound metadata for one animation leaf; a null track explicitly means unknown.</summary>
public readonly struct LocomotionFootPhaseBinding
{
    public AnimancerState State { get; }
    public AnimationFootPhaseTrack Track { get; }
    public LocomotionFootPhaseBinding(AnimancerState state, AnimationFootPhaseTrack track)
    { State = state; Track = track; }
}

public readonly struct ActorAnimationLocomotionOwner
{
    internal ActorAnimationLocomotionOwner(int id) => Id = id;
    internal int Id { get; }
    public bool IsValid => Id != 0;
}
