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

<<<<<<< HEAD
    [Range(0f, 1f), Tooltip("新动作入场时继承上一动作水平冲量/惯性的比例。0=不继承，1=全量继承。")]
    public float horizontalMomentumInheritance;

    [Range(0f, 1f), Tooltip("新动作入场时继承上一动作垂直重力/冲量积累的比例。0=不继承（通常用于取消/切招清垂直历史），1=全量继承。")]
    public float verticalMomentumInheritance;

=======
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
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
