using UnityEngine;

/// <summary>
/// One motor-tick movement/facing intent. Player input and AI produce this shared gameplay semantic;
/// ActorLocomotion owns it without knowing whether Player or AI submitted it.
/// </summary>
public struct LocomotionIntent
{
    public Vector3 WorldMoveDirection;
    public float MoveStrength;

    /// <summary>
    /// World-space horizontal facing direction. Zero means preserve the current facing.
    /// Producers must explicitly submit the move direction when they want orient-to-movement behavior.
    /// </summary>
    public Vector3 FacingDirection;

    public static LocomotionIntent Idle => new LocomotionIntent
    {
        WorldMoveDirection = Vector3.zero,
        MoveStrength = 0f,
        FacingDirection = Vector3.zero
    };
}
