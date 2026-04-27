using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// VelocityClip 运行时行为 —— 持续型外部速度的每帧写入器。
/// 
/// 生命周期：
///   - OnClipStart：记录旧 gravityScale，覆盖为 config.gravityScale（默认 0 = 浮空）
///   - OnClipUpdate：每帧按 Clip 进度读取曲线 → 解析方向 → 写入 Movement 的外部速度通道
///   - OnClipStop：清空外部速度通道，恢复 gravityScale
/// 
/// 设计要点：
///   - 方向每帧都重算（FromInstigator 模式追踪移动中的攻击者；ActorForward 会跟随角色转身）。
///     如果希望"锁定开始瞬间的方向"，应使用 FromContext（命中瞬间快照）而非 ActorForward。
///   - 曲线 horizontalCurve / verticalCurve 以 Clip 归一化时间 (0..1) 为输入，默认恒定 1。
///     用曲线做吹飞减速、起跳抛物线等手感塑形，比 Impulse 的自然衰减更可控。
///   - 写入的是 Movement._externalHorizontalVelocity / _externalVerticalVelocity 通道，
///     与 Locomotion / Impulse / Gravity 通道独立叠加。
/// 
/// 重力处理：
///   Clip 开始时覆盖 gravityScale，结束时恢复到 1.0（约定的默认值）。
///   如果外部（如 ActionMotionConfig）也在同一 Action 期间设置了 gravityScale，会有冲突
///   —— 当前约定是 Clip 级别的覆盖优先于 Action 级别。如果未来出现嵌套需求，
///   需要引入"scale stack"机制。
/// </summary>
public class ActionVelocityBehavior : ActionBehaviourBase
{
    /// <summary>由 ActionVelocityClip.CreatePlayable 注入的配置。</summary>
    public VelocityConfig config;

    /// <summary>缓存 Instigator Transform，FromInstigator 模式下避免每帧重新 GetComponent。</summary>
    private Transform _instigatorTransform;

    /// <summary>OnClipStart 记录的 gravityScale 原值，OnClipStop 时还原用。</summary>
    private float _savedGravityScale = 1f;

    protected override void OnClipStart(Playable playable)
    {
        if (actor?.movement == null || config == null) return;

        _instigatorTransform = null;

        // 记录原值（当前实现里 Movement 没有 GetGravityScale，用约定默认 1f；
        // 后续如果 ActionMotionConfig 做了嵌套覆盖，再改为真正读取）
        _savedGravityScale = 1f;

        // 覆盖重力缩放（默认 0 = 完全浮空，由 Clip 接管垂直）
        actor.movement.SetGravityScale(config.gravityScale);

        if (config.releaseMode == VerticalReleaseMode.ResetVertical)
            actor.movement.ResetVerticalState();

        if (config.debugLog)
        {
            Debug.Log($"[Velocity] Start — gravityScale={config.gravityScale}, vMode={config.verticalMode}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (actor == null || actor.movement == null || config == null) return;

        // 1. 计算 Clip 归一化进度（0..1），用于曲线采样
        double duration = playable.GetDuration();
        float t = duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 0f;

        // 2. 水平通道：若速度非零则解析方向并写入外部水平通道
        if (Mathf.Abs(config.horizontalSpeed) > 0.001f)
        {
            Vector3 dir = MotionDirectionResolver.Resolve(
                config.directionMode,
                actor,
                actionInstance,
                config.fixedLocalDirection,
                ref _instigatorTransform
            );

            float hCurve = config.horizontalCurve?.Evaluate(t) ?? 1f;
            Vector3 horizontal = dir * (config.horizontalSpeed * hCurve);
            actor.movement.SetExternalHorizontalVelocity(horizontal);
        }
        else
        {
            // 水平速度为 0 时显式清零水平通道，避免残留
            actor.movement.SetExternalHorizontalVelocity(Vector3.zero);
        }

        // 3. 垂直通道：按模式写入（方向模式只解析水平）
        float vCurve = config.verticalCurve?.Evaluate(t) ?? 1f;
        float vertical = config.verticalSpeed * vCurve;
        switch (config.verticalMode)
        {
            case VerticalVelocityMode.AdditiveExternal:
                actor.movement.SetExternalVerticalVelocity(Mathf.Abs(vertical) > 0.001f ? vertical : 0f);
                break;

            case VerticalVelocityMode.ClampRange:
                actor.movement.SetExternalVerticalVelocity(Mathf.Max(0f, vertical));
                actor.movement.SetVerticalClamp(config.clampMin, config.clampMax);
                break;

            case VerticalVelocityMode.OverrideVertical:
                actor.movement.SetExternalVerticalVelocity(0f);
                actor.movement.SetVerticalVelocityOverride(vertical);
                break;
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.movement == null) return;

        // 权威移交：需要时重置垂直通道到干净状态（冲量+重力归零）
        if (config != null && config.releaseMode == VerticalReleaseMode.ResetVertical)
            actor.movement.ResetVerticalState();

        // 清空外部速度通道（水平 + 垂直）
        actor.movement.ClearExternalVelocity();
        actor.movement.ClearVerticalVelocityOverride();
        actor.movement.ClearVerticalClamp();

        // 恢复重力缩放
        actor.movement.SetGravityScale(_savedGravityScale);

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Velocity] Stop — isFinish={isFinish}, release={config.releaseMode}");
        }

        _instigatorTransform = null;
    }
}
