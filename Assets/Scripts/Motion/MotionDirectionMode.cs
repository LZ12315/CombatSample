using UnityEngine;

/// <summary>
/// 通用"运动方向来源"枚举。被 ImpulseConfig 和 VelocityConfig 共用。
/// 
/// 设计目的：Impulse（瞬时冲量）和 Velocity（持续速度）在"如何解析方向"这件事上
/// 逻辑完全一致——都需要从上下文 / Instigator / 角色朝向 / Fixed 本地方向中选一个。
/// 与其维护两套几乎相同的枚举，不如抽成公共一套，避免将来漂移。
/// 
/// 维护备忘：如果新增方向模式（如 TowardsCameraForward、FromAttacker），请在这里扩展，
///           同时在 MotionDirectionResolver.Resolve 里实现对应分支。
/// </summary>
public enum MotionDirectionMode
{
    /// <summary>使用 EventContext.Direction（攻击者→被击者命中瞬间快照方向）。最常见的击退模式。</summary>
    FromContext,

    /// <summary>每帧重算 Instigator→Self 方向。用于追踪移动中的攻击者。</summary>
    FromInstigator,

    /// <summary>使用角色自身的 transform.forward。用于冲刺 / 主动技能的前向位移。</summary>
    ActorForward,

    /// <summary>使用角色自身的 -transform.forward。用于后撤步 / 后跳。</summary>
    ActorBackward,

    /// <summary>使用 Config 中配置的本地方向（相对角色朝向）。用于固定角度的演出位移。</summary>
    Fixed,
}

/// <summary>
/// 方向解析工具。把 MotionDirectionMode 翻译为世界空间的单位向量（已水平化 + 归一化）。
/// 
/// 使用者：ActionImpulseBehavior、ActionVelocityBehavior 及任何需要"按方向模式取世界方向"
/// 的 Clip 行为。把算法集中在这里，避免每个 Behavior 各写一份导致逻辑漂移。
/// 
/// 行为约定：
///   - 返回值已水平化（y=0）并归一化；方向无效时 fallback 到 -actor.transform.forward
///   - FromInstigator 需要 instigator 的 Transform，若为 null 则退化为 FromContext 的行为
///   - Fixed 模式的 localDirection 通过 actor.transform.TransformDirection 转世界空间
/// </summary>
public static class MotionDirectionResolver
{
    /// <summary>
    /// 解析方向为世界空间单位向量（水平化、归一化）。
    /// </summary>
    /// <param name="mode">方向模式</param>
    /// <param name="actor">当前 Actor（提供 transform.forward / TransformDirection）</param>
    /// <param name="actionInstance">当前 Action 实例（FromContext / FromInstigator 模式下读取 EventContext）</param>
    /// <param name="fixedLocalDirection">Fixed 模式下的本地方向（相对角色朝向）</param>
    /// <param name="cachedInstigatorTransform">
    /// FromInstigator 模式的缓存 Transform 引用。首次调用为 null 时会尝试从 EventContext.Instigator 取并回写。
    /// 传 ref 是为了避免每帧重复 GetComponent。
    /// </param>
    /// <returns>世界空间单位方向向量，方向无效时 fallback 到角色反方向</returns>
    public static Vector3 Resolve(
        MotionDirectionMode mode,
        Actor actor,
        ActionInstance actionInstance,
        Vector3 fixedLocalDirection,
        ref Transform cachedInstigatorTransform)
    {
        if (actor == null) return Vector3.zero;

        Vector3 dir = Vector3.zero;

        switch (mode)
        {
            case MotionDirectionMode.FromContext:
                // 使用命中瞬间快照的方向（攻击者→被击者，最常见的击退模式）
                if (actionInstance != null)
                    dir = actionInstance.EventContext.Direction;
                break;

            case MotionDirectionMode.FromInstigator:
                // 每帧重算 Instigator→Self，追踪移动中的攻击者
                if (cachedInstigatorTransform == null && actionInstance != null)
                    cachedInstigatorTransform = actionInstance.EventContext.Instigator?.transform;

                if (cachedInstigatorTransform != null)
                    dir = actor.transform.position - cachedInstigatorTransform.position;
                break;

            case MotionDirectionMode.ActorForward:
                dir = actor.transform.forward;
                break;

            case MotionDirectionMode.ActorBackward:
                dir = -actor.transform.forward;
                break;

            case MotionDirectionMode.Fixed:
                // 本地方向转世界空间（相对角色朝向）
                dir = actor.transform.TransformDirection(fixedLocalDirection);
                break;
        }

        // 统一水平化（Motion 通道的方向都不含垂直分量，垂直由 verticalSpeed / verticalForce 直接决定）
        dir.y = 0f;

        // 方向无效时 fallback 到角色反方向（语义：被攻击时默认向后退）
        if (dir.sqrMagnitude < 0.001f)
        {
            dir = -actor.transform.forward;
            dir.y = 0f;
        }

        return dir.normalized;
    }
}
