using UnityEngine;

/// <summary>Published World feedback is useful only for the same uninterrupted locomotion context.</summary>
public sealed class LocomotionVelocityFeedback
{
    private bool _hasPrevious;
    private MotionStateSnapshot _policy;
    private ActorGroundState _ground;
    private int _action;
    public void Reset() => _hasPrevious = false;

    public bool TryGetSpeed(in LocomotionRuntimeAnimationContext context, out float speed)
    {
        speed = 0f;
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
        { Reset(); return false; }
        var motor = context.Motor;
        bool continuous = _hasPrevious && _ground == motor.GroundState && _action == context.ActionOwnerId
            && SamePolicy(_policy, motor.MotionState);
        _hasPrevious = true;
        _policy = motor.MotionState;
        _ground = motor.GroundState;
        _action = context.ActionOwnerId;
        var result = motor.PreviousResult;
        if (!continuous || context.ActionOwnerId != 0 || !IsAutonomousResult(result)
            || !SamePolicy(result.MotionState, motor.MotionState)
            || result.WasGroundedAtStart != motor.IsGrounded
            || result.IsGroundedAfterSolve != motor.IsGrounded) return false;
        speed = Vector3.ProjectOnPlane(result.ActualSolvedVelocity, motor.CharacterUp).magnitude
            / result.MotionState.MovementTimeScale;
        return LocomotionDataValidation.IsFinite(speed);
    }

    internal static bool IsAutonomousResult(in MotorMotionResult result) => result.IsValid
        && result.HorizontalSource == HorizontalMotionSource.Locomotion
        && !result.MotionState.HasHorizontalVelocityOwner && !result.MotionState.HasTrajectoryRootMotionOwner
        && !result.HadSignificantHorizontalImpulse && !result.HadPlatformCarry && !result.HadActorSeparation
        && !result.HasUnknownExternalDisplacement && result.MotionState.MovementTimeScale > 0f;

    public static bool SamePolicy(in MotionStateSnapshot a, in MotionStateSnapshot b) =>
        a.LocomotionScale == b.LocomotionScale && a.AirLocomotionScale == b.AirLocomotionScale
        && a.GravityScale == b.GravityScale && a.MovementTimeScale == b.MovementTimeScale
        && a.HasHorizontalVelocityOwner == b.HasHorizontalVelocityOwner
        && a.HasTrajectoryRootMotionOwner == b.HasTrajectoryRootMotionOwner
        && a.HasRootRotationOwner == b.HasRootRotationOwner && a.HasScriptedRotationOwner == b.HasScriptedRotationOwner;
}
