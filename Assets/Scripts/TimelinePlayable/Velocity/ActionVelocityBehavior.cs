using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// VelocityClip 运行时行为 —— 持续型速度覆盖写入器。
/// 
/// 生命周期：
///   - OnClipStart：按配置声明的轴申请 Velocity owner
///   - OnClipUpdate：每帧按 Clip 进度读取曲线 → 解析方向 → 覆盖对应轴速度
///   - OnClipStop：释放本 Clip 持有的 owner
///   - Action 切换场景：若旧 Clip 未正常 Stop，Action 入场会统一 ClearVelocityOwners() 兜底回收
/// 
/// 设计要点：
///   - 方向每帧都重算（FromInstigator 模式追踪移动中的攻击者；ActorForward 会跟随角色转身）。
///     如果希望"锁定开始瞬间的方向"，应使用 FromContext（命中瞬间快照）而非 ActorForward。
///   - 曲线 horizontalCurve / verticalCurve 以 Clip 归一化时间 (0..1) 为输入，默认恒定 1。
///     用曲线做吹飞减速、起跳抛物线等手感塑形，比 Impulse 的自然衰减更可控。
///   - Velocity owner 存在时，对应轴由 VelocityClip 覆盖；未声明的轴不接管。
/// </summary>
public class ActionVelocityBehavior : ActionBehaviourBase
{
    /// <summary>由 ActionVelocityClip.CreatePlayable 注入的配置。</summary>
    public VelocityConfig config;

    /// <summary>缓存 Instigator Transform，FromInstigator 模式下避免每帧重新 GetComponent。</summary>
    private Transform _instigatorTransform;

    private MotionOwner _horizontalOwner;
    private MotionOwner _verticalOwner;

    protected override void OnClipStart(Playable playable)
    {
        if (actor?.movement == null || config == null) return;

        _instigatorTransform = null;

        if (ShouldUseHorizontalVelocity())
            _horizontalOwner = actor.movement.BeginHorizontalVelocity();

        if (ShouldUseVerticalVelocity())
            _verticalOwner = actor.movement.BeginVerticalVelocity();

        if (config.debugLog)
        {
            Debug.Log($"[Velocity] Start — hOwner={_horizontalOwner.IsValid}, vOwner={_verticalOwner.IsValid}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (actor == null || actor.movement == null || config == null) return;

        // 1. 计算 Clip 归一化进度（0..1），用于曲线采样
        double duration = playable.GetDuration();
        float t = duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 0f;

        // 2. 水平通道：声明接管时覆盖水平程序速度，即使速度为 0 也保持控制。
        if (_horizontalOwner.IsValid)
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
            actor.movement.SetHorizontalVelocity(_horizontalOwner, horizontal);
        }

        // 3. 垂直通道：声明接管时覆盖最终垂直程序速度。
        if (_verticalOwner.IsValid)
        {
            float vCurve = config.verticalCurve?.Evaluate(t) ?? 1f;
            float vertical = config.verticalSpeed * vCurve;
            actor.movement.SetVerticalVelocity(_verticalOwner, vertical);
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.movement == null) return;

        actor.movement.EndHorizontalVelocity(_horizontalOwner);
        actor.movement.EndVerticalVelocity(_verticalOwner);
        _horizontalOwner = default;
        _verticalOwner = default;

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Velocity] Stop — isFinish={isFinish}");
        }

        _instigatorTransform = null;
    }

    private bool ShouldUseHorizontalVelocity()
    {
        return config.useHorizontalVelocity || Mathf.Abs(config.horizontalSpeed) > 0.001f;
    }

    private bool ShouldUseVerticalVelocity()
    {
        return config.useVerticalVelocity ||
               Mathf.Abs(config.verticalSpeed) > 0.001f;
    }
}
