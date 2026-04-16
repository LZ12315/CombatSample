using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
/// - OnClipStart：读取 EventContext 方向，注入垂直初速度，设置重力缩放
/// - OnClipUpdate：每帧写入水平冲量（曲线衰减）
/// - OnClipStop：恢复重力缩放
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    /// <summary>缓存的水平冲量方向（世界空间，已归一化）。FromInstigator 模式下每帧更新。</summary>
    private Vector3 _horizontalDirection;

    /// <summary>缓存的 Clip 总时长</summary>
    private double _clipDuration;

    /// <summary>缓存的 Instigator Transform（FromInstigator 模式下每帧追踪）</summary>
    private Transform _instigatorTransform;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.movement == null || config == null) return;

        // 1. 根据方向模式计算冲量方向
        _instigatorTransform = null;
        _horizontalDirection = ResolveDirection();

        // 2. 缓存 Clip 时长
        _clipDuration = playable.GetDuration();

        // 3. 注入垂直初速度到重力通道（一次性，之后由重力自然衰减）
        if (Mathf.Abs(config.verticalForce) > 0.001f)
        {
            actor.movement.SetVerticalVelocity(config.verticalForce);
        }

        // 4. 设置重力缩放
        actor.movement.SetGravityScale(config.gravityScale);

        if (config.debugLog)
        {
            Debug.Log($"[Impulse] Start — dir={_horizontalDirection}, hForce={config.horizontalForce}, " +
                      $"vForce={config.verticalForce}, gravity={config.gravityScale}, duration={_clipDuration:F3}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (actor == null || actor.movement == null || config == null) return;
        if (_clipDuration <= 0) return;

        // FromInstigator 模式：每帧重新计算方向（追踪攻击者实时位置）
        if (config.directionMode == ImpulseDirectionMode.FromInstigator)
        {
            _horizontalDirection = ResolveDirection();
        }

        // 计算归一化时间
        float t = Mathf.Clamp01((float)(playable.GetTime() / _clipDuration));

        // 从衰减曲线读取系数
        float decay = config.horizontalDecay.Evaluate(t);

        // 计算水平冲量速度
        Vector3 horizontalVel = _horizontalDirection * (config.horizontalForce * decay);

        // 写入冲量通道（帧末自动清零）
        actor.movement.SetImpulseVelocity(horizontalVel);

        if (config.debugLog)
        {
            Debug.Log($"[Impulse] Update — t={t:F3}, decay={decay:F3}, vel={horizontalVel}");
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.movement == null) return;

        // 恢复重力缩放
        actor.movement.SetGravityScale(1f);

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Impulse] Stop — isFinish={isFinish}");
        }

        _instigatorTransform = null;
    }

    /// <summary>
    /// 根据 config.directionMode 计算水平冲量方向（世界空间，已归一化）。
    /// 方向无效时 fallback 到角色反方向（被打向后退）。
    /// </summary>
    private Vector3 ResolveDirection()
    {
        Vector3 dir = Vector3.zero;

        switch (config.directionMode)
        {
            case ImpulseDirectionMode.FromContext:
                // 使用命中瞬间快照的方向
                if (actionInstance != null)
                    dir = actionInstance.EventContext.Direction;
                break;

            case ImpulseDirectionMode.FromInstigator:
                // 运行时计算 Instigator → Self 方向
                if (_instigatorTransform == null && actionInstance != null)
                    _instigatorTransform = actionInstance.EventContext.Instigator?.transform;

                if (_instigatorTransform != null)
                    dir = actor.transform.position - _instigatorTransform.position;
                break;

            case ImpulseDirectionMode.ActorForward:
                dir = actor.transform.forward;
                break;

            case ImpulseDirectionMode.ActorBackward:
                dir = -actor.transform.forward;
                break;

            case ImpulseDirectionMode.Fixed:
                // 将本地方向转换为世界空间
                dir = actor.transform.TransformDirection(config.fixedLocalDirection);
                break;
        }

        // 水平化
        dir.y = 0f;

        // 方向无效时 fallback
        if (dir.sqrMagnitude < 0.001f)
        {
            dir = -actor.transform.forward;
            dir.y = 0f;
        }

        return dir.normalized;
    }
}
