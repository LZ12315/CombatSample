/// <summary>
/// gameplay 侧看到的接地状态。
/// 由 ActorMotionRuntime 根据 KCC 稳定接地事实推进。
/// </summary>
public enum ActorGroundState
{
    Grounded,
    JustLeftGround,
    Airborne,
    JustLanded
}

/// <summary>
/// 动画根运动的应用策略。
/// Managed 表示 ActorMotor/KCC 应用动画根位移和根旋转；
/// External 表示运行时只缓存 Animator 输出，不把根位移或根旋转叠加到角色运动上。
/// </summary>
public enum RootMotionApplyMode
{
    Managed,
    External
}
