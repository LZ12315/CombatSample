using UnityEngine;

public enum HorizontalMotionSource
{
    Unknown,
    Locomotion,
    HorizontalVelocityOwner,
    TrajectoryRootMotion,
}

/// <summary>Facts available after Action, without opening or consuming a Motion tick.</summary>
public readonly struct MotionStateSnapshot
{
    public readonly float LocomotionScale;
    public readonly float AirLocomotionScale;
    public readonly float GravityScale;
    public readonly float MovementTimeScale;
    public readonly bool HasHorizontalVelocityOwner;
    public readonly bool HasTrajectoryRootMotionOwner;
    public readonly bool HasRootRotationOwner;
    public readonly bool HasScriptedRotationOwner;

    public MotionStateSnapshot(float locomotionScale, float airLocomotionScale, float gravityScale,
        float movementTimeScale, bool hasHorizontalVelocityOwner, bool hasTrajectoryRootMotionOwner,
        bool hasRootRotationOwner, bool hasScriptedRotationOwner)
    {
        LocomotionScale = locomotionScale;
        AirLocomotionScale = airLocomotionScale;
        GravityScale = gravityScale;
        MovementTimeScale = movementTimeScale;
        HasHorizontalVelocityOwner = hasHorizontalVelocityOwner;
        HasTrajectoryRootMotionOwner = hasTrajectoryRootMotionOwner;
        HasRootRotationOwner = hasRootRotationOwner;
        HasScriptedRotationOwner = hasScriptedRotationOwner;
    }
}

/// <summary>One completed World interval; never inferred from currently active owners.</summary>
public readonly struct MotorMotionResult
{
    public readonly bool IsValid;
    public readonly Vector3 ActualSolvedVelocity;
    public readonly HorizontalMotionSource HorizontalSource;
    public readonly MotionStateSnapshot MotionState;
    public readonly bool WasGroundedAtStart;
    public readonly bool IsGroundedAfterSolve;
    public readonly bool HadSignificantHorizontalImpulse;
    public readonly bool HadPlatformCarry;
    public readonly bool HadActorSeparation;
    public readonly bool HasUnknownExternalDisplacement;

    public MotorMotionResult(Vector3 actualSolvedVelocity, HorizontalMotionSource horizontalSource,
        MotionStateSnapshot motionState, bool wasGroundedAtStart, bool isGroundedAfterSolve,
        bool hadSignificantHorizontalImpulse,
        bool hadPlatformCarry, bool hadActorSeparation, bool hasUnknownExternalDisplacement)
    {
        IsValid = true;
        ActualSolvedVelocity = actualSolvedVelocity;
        HorizontalSource = horizontalSource;
        MotionState = motionState;
        WasGroundedAtStart = wasGroundedAtStart;
        IsGroundedAfterSolve = isGroundedAfterSolve;
        HadSignificantHorizontalImpulse = hadSignificantHorizontalImpulse;
        HadPlatformCarry = hadPlatformCarry;
        HadActorSeparation = hadActorSeparation;
        HasUnknownExternalDisplacement = hasUnknownExternalDisplacement;
    }
}

public readonly struct LocomotionMotionContext
{
    public readonly float EffectiveDeltaTime;
    public readonly bool IsGrounded;
    public readonly ActorGroundState GroundState;
    public readonly Vector3 CharacterUp;
    public readonly Quaternion CurrentWorldRotation;
    public readonly MotorMotionResult PreviousResult;
    public readonly MotionStateSnapshot MotionState;

    public LocomotionMotionContext(float effectiveDeltaTime, bool isGrounded, Vector3 characterUp,
        Quaternion currentWorldRotation, MotorMotionResult previousResult, MotionStateSnapshot motionState)
        : this(effectiveDeltaTime,
            isGrounded ? ActorGroundState.Grounded : ActorGroundState.Airborne,
            characterUp, currentWorldRotation, previousResult, motionState)
    {
    }

    public LocomotionMotionContext(float effectiveDeltaTime, ActorGroundState groundState, Vector3 characterUp,
        Quaternion currentWorldRotation, MotorMotionResult previousResult, MotionStateSnapshot motionState)
    {
        EffectiveDeltaTime = effectiveDeltaTime;
        GroundState = groundState;
        IsGrounded = groundState is ActorGroundState.Grounded or ActorGroundState.JustLanded;
        CharacterUp = characterUp;
        CurrentWorldRotation = currentWorldRotation;
        PreviousResult = previousResult;
        MotionState = motionState;
    }
}

public readonly struct LocomotionMotionRequest
{
    public readonly Vector3 WorldPlanarVelocity;
    public readonly Quaternion TargetWorldRotation;
    public readonly bool HasVelocity;
    public readonly bool HasRotation;

    public LocomotionMotionRequest(Vector3 worldPlanarVelocity, Quaternion targetWorldRotation,
        bool hasVelocity, bool hasRotation)
    {
        WorldPlanarVelocity = worldPlanarVelocity;
        TargetWorldRotation = targetWorldRotation;
        HasVelocity = hasVelocity;
        HasRotation = hasRotation;
    }
}
