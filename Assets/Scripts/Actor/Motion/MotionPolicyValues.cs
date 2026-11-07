using System;

/// <summary>One source's resolved constraints. Zero turn limit means no additional speed cap.</summary>
internal readonly struct MotionPolicyValues
{
    internal readonly float LocomotionScale, AirLocomotionScale, GravityScale, TurnSpeedLimit;
    internal readonly bool BrakeLocomotion, LockLocomotionRotation;

    internal static MotionPolicyValues Neutral => new(1f, 1f, 1f);

    internal MotionPolicyValues(float locomotionScale, float airLocomotionScale, float gravityScale,
        bool brakeLocomotion = false, bool lockLocomotionRotation = false, float turnSpeedLimit = 0f)
    {
        LocomotionScale = locomotionScale;
        AirLocomotionScale = airLocomotionScale;
        GravityScale = gravityScale;
        BrakeLocomotion = brakeLocomotion;
        LockLocomotionRotation = lockLocomotionRotation;
        TurnSpeedLimit = turnSpeedLimit;
    }

    internal bool IsNeutral => LocomotionScale == 1f && AirLocomotionScale == 1f && GravityScale == 1f
        && !BrakeLocomotion && !LockLocomotionRotation && TurnSpeedLimit == 0f;

    internal static MotionPolicyValues Restrict(in MotionPolicyValues a, in MotionPolicyValues b) => new(
        Math.Min(a.LocomotionScale, b.LocomotionScale), Math.Min(a.AirLocomotionScale, b.AirLocomotionScale),
        a.GravityScale * b.GravityScale, a.BrakeLocomotion || b.BrakeLocomotion,
        a.LockLocomotionRotation || b.LockLocomotionRotation, CombineTurnLimit(a.TurnSpeedLimit, b.TurnSpeedLimit));

    internal static float CombineTurnLimit(float a, float b) => a <= 0f ? b : b <= 0f ? a : Math.Min(a, b);
}
