using System;
using UnityEngine;

/// <summary>
/// 整招级运动策略 — 配置在 ActionAsset 上，由 ActionInstance.OnEnter/OnExit 应用/恢复。
/// </summary>
[Serializable]
public struct ActionMotionConfig
{
    [Tooltip("RootMotion 应用策略：Managed=由 ActorMotionRuntime/KCC 应用动画根位移和根旋转；External=两者都不应用，由程序或外部系统负责。")]
    public RootMotionApplyMode rootMotionMode;

    [Tooltip("是否压制 Locomotion 的移动和朝向输出")]
    public bool suppressLocomotion;

    [Tooltip("动作开始时的朝向行为")]
    public ActionFacingOnStart facingOnStart;

    [Tooltip("整招期间的重力倍率。-1 = 不覆盖")]
    public float gravityScale;

    [Range(0f, 1f), Tooltip("入场时继承多少旧水平动量。0=完全清掉，1=完整保留。")]
    public float horizontalMomentumInheritance;

    [Range(0f, 1f), Tooltip("入场时继承多少旧垂直动量。0=完全清掉，1=完整保留。")]
    public float verticalMomentumInheritance;

    /// <summary>
    /// 默认配置：RootMotion 托管、压制 Locomotion、起手朝目标/摇杆、不覆盖重力、不继承旧动作动量。
    /// 适用于大多数攻击动作。
    /// </summary>
    public static ActionMotionConfig Default => new()
    {
        rootMotionMode = RootMotionApplyMode.Managed,
        suppressLocomotion = true,
        facingOnStart = ActionFacingOnStart.SnapToInputOrTarget,
        gravityScale = -1f,
        horizontalMomentumInheritance = 0f,
        verticalMomentumInheritance = 0f,
    };
}
