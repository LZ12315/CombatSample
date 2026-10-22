using System;
using UnityEngine;

/// <summary>One immutable parameter sample. Reading either axis never advances simulation.</summary>
internal readonly struct LocomotionMoveParameters
{
    private readonly Vector3 _localInput, _localVelocity;
    private readonly float _horizontalSpeed, _verticalSpeed, _strength, _lean;

    internal LocomotionMoveParameters(in LocomotionRuntimeAnimationContext context, float lean)
    {
        Quaternion inverse = Quaternion.Inverse(context.Motor.CurrentWorldRotation);
        Vector3 input = context.HasIntent
            ? Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection, context.Motor.CharacterUp).normalized
                * context.Intent.MoveStrength : Vector3.zero;
        _localInput = inverse * input;
        _localVelocity = inverse * context.PolicyVelocity;
        _horizontalSpeed = Vector3.ProjectOnPlane(context.PolicyVelocity, context.Motor.CharacterUp).magnitude;
        _verticalSpeed = context.VerticalSpeed;
        _strength = context.HasIntent ? context.Intent.MoveStrength : 0f;
        _lean = lean;
    }

    internal float Read(LocomotionMoveParameter source) => source switch
    {
        LocomotionMoveParameter.HorizontalSpeed => _horizontalSpeed,
        LocomotionMoveParameter.VerticalSpeed => _verticalSpeed,
        LocomotionMoveParameter.InputStrength => _strength,
        LocomotionMoveParameter.LocalInputX => _localInput.x,
        LocomotionMoveParameter.LocalInputZ => _localInput.z,
        LocomotionMoveParameter.LocalVelocityX => _localVelocity.x,
        LocomotionMoveParameter.LocalVelocityZ => _localVelocity.z,
        LocomotionMoveParameter.TurnLean => _lean,
        _ => 0f,
    };

    internal static bool IsValid(LocomotionMoveParameter source) =>
        Enum.IsDefined(typeof(LocomotionMoveParameter), source);

    internal static bool IsHorizontalMovement(LocomotionMoveParameter source) =>
        source != LocomotionMoveParameter.VerticalSpeed && source != LocomotionMoveParameter.TurnLean && IsValid(source);
}

/// <summary>Per-runtime visual response to autonomous model steering; never moves the Actor.</summary>
internal sealed class LocomotionTurnLean
{
    private bool _interrupted;
    internal float Value { get; private set; }

    internal void Reset()
    {
        Value = 0f;
        _interrupted = false;
    }

    // An animation feedback gap is an interruption, not merely a zero-dt sample.
    // Clear stale steering and skip the first resumed sample; do not retain an old turn.
    internal void Suspend()
    {
        Reset();
        _interrupted = true;
    }

    internal float Update(in LocomotionRuntimeAnimationContext context, in LocomotionTurnLeanConfig config)
    {
        float dt = context.Motor.EffectiveDeltaTime;
        if (!LocomotionDataValidation.IsFinite(dt) || dt <= 0f) return Value;
        var policy = context.Motor.MotionState;
        float scale = policy.LocomotionScale;
        bool blocked = !config.IsValid || !context.Motor.IsGrounded || context.ActionOwnerId != 0
            || policy.HasHorizontalVelocityOwner || policy.HasTrajectoryRootMotionOwner
            || policy.HasRootRotationOwner || policy.HasScriptedRotationOwner
            || !LocomotionDataValidation.IsFinite(scale) || scale <= 0f;
        if (blocked)
        {
            Suspend();
            return Value;
        }
        // Both velocities belong to this tick. Positive scale/time changes do not invalidate
        // them, unlike previous-World-result feedback. Only an actual interruption rebaselines.
        bool rebaseline = _interrupted;
        _interrupted = false;
        if (rebaseline)
        {
            Value = 0f;
            return Value;
        }

        // Input-driven spaces must reach their stationary row exactly on release.
        // A decaying lean with InputStrength=0 could otherwise blend running poses back in.
        if (!context.HasMovingInput)
        {
            Value = 0f;
            return Value;
        }

        Vector3 up = context.Motor.CharacterUp.normalized;
        Vector3 before = Vector3.ProjectOnPlane(context.VelocityBeforeMotion, up);
        Vector3 after = Vector3.ProjectOnPlane(context.ModelVelocity, up);
        float speed = Mathf.Min(before.magnitude, after.magnitude);
        float target = 0f;
        // Reversals are not arcs. Avoid choosing an arbitrary lean side near 180 degrees.
        if (LocomotionDataValidation.IsFinite(before) && LocomotionDataValidation.IsFinite(after)
            && speed > Mathf.Max(config.MinimumSpeed, 0.0001f) && up.sqrMagnitude > 0.5f
            && Vector3.Angle(before, after) < 120f)
        {
            Vector3 right = Vector3.Cross(up, before.normalized);
            float lateralAcceleration = Vector3.Dot((after - before) / dt, right) * scale;
            float speedInfluence = config.MinimumSpeed > 0f
                ? Mathf.Clamp01((speed - config.MinimumSpeed) / config.MinimumSpeed) : 1f;
            target = Mathf.Clamp(lateralAcceleration / config.FullLeanAcceleration, -1f, 1f) * speedInfluence;
        }
        if (!LocomotionDataValidation.IsFinite(target)) target = 0f;
        float alpha = config.SmoothSeconds > 0f ? 1f - Mathf.Exp(-dt / config.SmoothSeconds) : 1f;
        Value = Mathf.Lerp(Value, target, alpha);
        return Value;
    }
}
