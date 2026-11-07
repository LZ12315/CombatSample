using System;
using UnityEngine;

/// <summary>Immutable, unwrapped extracted root yaw. A right rear turn must not wrap into a left turn.</summary>
internal sealed class LocomotionStartYawCurve
{
    private readonly float[] _times, _yaw;
    internal float Duration => _times[_times.Length - 1];

    private LocomotionStartYawCurve(float[] times, float[] yaw) { _times = times; _yaw = yaw; }

    internal static LocomotionStartYawCurve Capture(RootMotionTrajectory trajectory)
    {
        if (trajectory == null || !trajectory.ValidateData().IsValid) return null;
        int count = trajectory.SampleCount;
        var times = new float[count];
        var yaw = new float[count];
        float previous = 0f;
        for (int i = 0; i < count; i++)
        {
            times[i] = trajectory.SampleTimes[i];
            if (!RootMotionYawUtility.TryExtractLocalYaw(trajectory.CumulativeRotations[i], out var rotation))
                return null;
            float current = rotation.eulerAngles.y;
            yaw[i] = i == 0 ? 0f : yaw[i - 1] + Mathf.DeltaAngle(previous, current);
            previous = current;
        }
        return new LocomotionStartYawCurve(times, yaw);
    }

    internal float Sample(float time)
    {
        if (time <= 0f) return _yaw[0];
        if (time >= Duration) return _yaw[_yaw.Length - 1];
        int index = Array.BinarySearch(_times, time);
        if (index >= 0) return _yaw[index];
        int upper = ~index, lower = upper - 1;
        return Mathf.Lerp(_yaw[lower], _yaw[upper], (time - _times[lower]) / (_times[upper] - _times[lower]));
    }
}

/// <summary>Coordinate-frame rules for turn-to-move Starts. Never changes gameplay motion.</summary>
internal static class LocomotionStartAlignment
{
    // Large changes invalidate the entered start, rather than replaying/reselecting a transient every tick.
    internal const float DirectionChangeLimit = 45f;

    internal static bool IsTurnToMove(in LocomotionRuntimeAnimationContext context)
    {
        Vector3 up = context.Motor.CharacterUp;
        Vector3 move = Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection, up);
        Vector3 face = Vector3.ProjectOnPlane(context.Intent.FacingDirection, up);
        return context.HasMovingInput && move.sqrMagnitude > 0.0001f && face.sqrMagnitude > 0.0001f
            && Vector3.Angle(move, face) < 1f
            && !context.Motor.MotionState.LockLocomotionRotation
            && !context.Motor.MotionState.HasRootRotationOwner
            && !context.Motor.MotionState.HasScriptedRotationOwner;
    }

    internal static Vector2 TargetDirection(float authoredTurnDegrees)
    {
        float angle = authoredTurnDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    // Direction is primary. For equal directions (notably +/-180), follow the Motor's chosen side.
    internal static int TurnSidePenalty(float authoredTurn, float motorTurn) =>
        Mathf.Abs(authoredTurn) > 0.0001f && Mathf.Abs(motorTurn) > 0.0001f
            && Mathf.Sign(authoredTurn) != Mathf.Sign(motorTurn) ? 1 : 0;

    internal static float PoseYaw(float extractedYaw, float authoredTurn, float motorTurnSinceEntry, float phase)
    {
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
        // Initially reconstruct the clip in the entry-world frame, cancelling Motor rotation.
        // By the end, express it in Motor-forward space: extractedYaw - authoredTurn.
        // The latter is NOT necessarily zero: root extraction can leave a non-forward pose baseline.
        // Unwrapped scalar interpolation preserves left/right paths through 180 degrees.
        return extractedYaw - Mathf.LerpUnclamped(motorTurnSinceEntry, authoredTurn, blend);
    }
}
