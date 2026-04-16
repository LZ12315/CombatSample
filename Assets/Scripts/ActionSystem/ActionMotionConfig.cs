using System;
using UnityEngine;

/// <summary>
/// 整招级运动策略 — 配置在 ActionAsset 上，由 ActionInstance.OnEnter/OnExit 应用/恢复。
/// </summary>
[Serializable]
public struct ActionMotionConfig
{
    [Tooltip("位移由 RootMotion 驱动(Managed) 还是程序驱动(External)")]
    public ActorMovement.RootMotionApplyMode rootMotionMode;

    [Tooltip("是否压制 Locomotion 的移动和朝向输出")]
    public bool suppressLocomotion;

    [Tooltip("动作开始时的朝向行为")]
    public ActionFacingOnStart facingOnStart;

    [Tooltip("整招期间的重力倍率。-1 = 不覆盖")]
    public float gravityScale;

    /// <summary>
    /// 默认配置：RootMotion 托管、压制 Locomotion、起手朝目标/摇杆、不覆盖重力。
    /// 适用于大多数攻击动作。
    /// </summary>
    public static ActionMotionConfig Default => new()
    {
        rootMotionMode = ActorMovement.RootMotionApplyMode.Managed,
        suppressLocomotion = true,
        facingOnStart = ActionFacingOnStart.SnapToInputOrTarget,
        gravityScale = -1f,
    };
}
