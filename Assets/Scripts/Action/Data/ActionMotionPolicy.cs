using System;
using UnityEngine;

public enum ActionMovementControl { Allow = 0, Brake = 1, Locked = 2 }
public enum ActionTurnControl { Normal = 0, Limited = 1, Locked = 2 }

/// <summary>Whole-action defaults. Movement sources such as RootMotion are deliberately separate.</summary>
[Serializable]
public sealed class ActionMotionPolicy
{
    public ActionMovementControl movement = ActionMovementControl.Locked;
    public float locomotionScale = 1f;
    public ActionTurnControl turning = ActionTurnControl.Locked;
    public float maxTurnSpeed = 180f;
    public float airLocomotionScale = 1f;
    public float gravityScale = 1f;

    internal MotionPolicyValues Capture()
    {
        ValidateMovement(movement, locomotionScale);
        ValidateTurning(turning, maxTurnSpeed);
        ValidateScale(airLocomotionScale, true, "Air movement");
        ValidateScale(gravityScale, false, "Gravity");
        return new MotionPolicyValues(movement == ActionMovementControl.Locked ? 0f
            : movement == ActionMovementControl.Brake ? 1f : locomotionScale,
            airLocomotionScale, gravityScale, movement == ActionMovementControl.Brake,
            turning == ActionTurnControl.Locked, turning == ActionTurnControl.Limited ? maxTurnSpeed : 0f);
    }

    internal static void ValidateMovement(ActionMovementControl mode, float scale)
    {
        if (!Enum.IsDefined(typeof(ActionMovementControl), mode))
            throw new InvalidOperationException("Invalid movement control mode.");
        if (mode == ActionMovementControl.Allow)
            ValidateScale(scale, true, "Movement");
    }

    internal static void ValidateTurning(ActionTurnControl mode, float speed)
    {
        if (!Enum.IsDefined(typeof(ActionTurnControl), mode))
            throw new InvalidOperationException("Invalid turn control mode.");
        if (mode == ActionTurnControl.Limited && (!ActionAuthoringMath.IsFinite(speed) || speed <= 0f))
            throw new InvalidOperationException("Limited turning requires a finite, positive speed in degrees/second.");
    }

    internal static void ValidateScale(float value, bool restricted, string name)
    {
        if (!ActionAuthoringMath.IsFinite(value) || value < 0f || (restricted && value > 1f))
            throw new InvalidOperationException($"{name} scale must be finite and {(restricted ? "within [0, 1]" : "non-negative")}.");
    }
}

/// <summary>Immutable per-field window snapshot, including read-only interpretation of legacy fields.</summary>
internal readonly struct ActionMotionPolicyOverride
{
    internal readonly bool Movement, Turning, Air, Gravity;
    internal readonly MotionPolicyValues Values;
    internal bool HasAny => Movement || Turning || Air || Gravity;
    internal bool OverlapsFields(in ActionMotionPolicyOverride other) =>
        (Movement && other.Movement) || (Turning && other.Turning) || (Air && other.Air) || (Gravity && other.Gravity);

    internal ActionMotionPolicyOverride(MotionPolicyItemConfig config)
    {
        if (config == null) throw new InvalidOperationException("Motion Policy window has no configuration.");
        Movement = config.useMovementControl || config.useLocomotionScale;
        Turning = config.useTurnControl;
        Air = config.useAirLocomotionScale;
        Gravity = config.useGravityScale;
        var movement = config.useMovementControl ? config.movement
            : config.locomotionScale == 0f ? ActionMovementControl.Locked : ActionMovementControl.Allow;
        if (Movement) ActionMotionPolicy.ValidateMovement(movement, config.locomotionScale);
        if (Turning) ActionMotionPolicy.ValidateTurning(config.turning, config.maxTurnSpeed);
        if (Air) ActionMotionPolicy.ValidateScale(config.airLocomotionScale, true, "Air movement");
        if (Gravity) ActionMotionPolicy.ValidateScale(config.gravityScale, false, "Gravity");
        Values = new MotionPolicyValues(!Movement ? 1f : movement == ActionMovementControl.Locked ? 0f
            : movement == ActionMovementControl.Brake ? 1f : config.locomotionScale,
            Air ? config.airLocomotionScale : 1f, Gravity ? config.gravityScale : 1f,
            Movement && movement == ActionMovementControl.Brake,
            Turning && config.turning == ActionTurnControl.Locked,
            Turning && config.turning == ActionTurnControl.Limited ? config.maxTurnSpeed : 0f);
    }
}
