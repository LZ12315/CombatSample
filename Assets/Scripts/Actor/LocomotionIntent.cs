using UnityEngine;

/// <summary>
/// 一帧的移动/动画/朝向意图。由 <see cref="ActorLogicInput"/>（玩家）或 AI/行为树（敌人）填写；
/// <see cref="ActorMovement"/> 只消费，不读相机与摇杆。
/// </summary>
public struct LocomotionIntent
{
    /// <summary>世界空间水平移动方向（单位向量）。无输入时为 Vector3.zero。</summary>
    public Vector3 WorldMoveDirection;

    /// <summary>0..1，对应摇杆模长等；填写方应 Clamp 到合理范围。</summary>
    public float MoveStrength;

    /// <summary>
    /// 世界空间水平身体朝向。零向量表示：有移动时朝 <see cref="WorldMoveDirection"/>，无移动时不更新朝向。
    /// 锁定时可填指向目标的方向。
    /// </summary>
    public Vector3 FacingDirection;

    public static LocomotionIntent Idle => new LocomotionIntent
    {
        WorldMoveDirection = Vector3.zero,
        MoveStrength = 0f,
        FacingDirection = Vector3.zero
    };
}
