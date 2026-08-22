using UnityEngine;

/// <summary>
/// One motor-tick movement/facing intent. Player input and AI produce this shared gameplay semantic;
/// ActorMotor consumes it without knowing the source.
/// </summary>
public struct LocomotionIntent
{
    public Vector3 WorldMoveDirection;
    public float MoveStrength;

    /// <summary>
    /// World-space horizontal facing direction. Zero means face the move direction when moving,
    /// or keep the current facing when idle.
    /// </summary>
    public Vector3 FacingDirection;

    public static LocomotionIntent Idle => new LocomotionIntent
    {
        WorldMoveDirection = Vector3.zero,
        MoveStrength = 0f,
        FacingDirection = Vector3.zero
    };
}
