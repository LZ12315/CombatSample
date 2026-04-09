using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
/// - OnClipStart：读取 EventContext 方向，注入垂直初速度，设置重力缩放，锁定朝向
/// - OnClipUpdate：每帧写入水平冲量（曲线衰减）
/// - OnClipStop：恢复重力缩放，解锁朝向
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    /// <summary>缓存的水平冲量方向（世界空间，已归一化）</summary>
    private Vector3 _horizontalDirection;

    /// <summary>缓存的 Clip 总时长</summary>
    private double _clipDuration;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.movement == null || config == null) return;

        // 1. 从 EventContext 读取方向并水平化
        Vector3 dir = Vector3.zero;
        if (actionInstance != null)
        {
            dir = actionInstance.EventContext.Direction;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            // 方向无效时，fallback 到角色反方向（被打向后退）
            dir = -actor.transform.forward;
            dir.y = 0f;
        }
        _horizontalDirection = dir.normalized;

        // 2. 缓存 Clip 时长
        _clipDuration = playable.GetDuration();

        // 3. 锁定朝向
        if (config.lockFacing)
        {
            actor.movement.LockFacing();
        }

        // 4. 注入垂直初速度到重力通道（一次性，之后由重力自然衰减）
        if (Mathf.Abs(config.verticalForce) > 0.001f)
        {
            actor.movement.SetVerticalVelocity(config.verticalForce);
        }

        // 5. 设置重力缩放
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

        // 解锁朝向
        if (config != null && config.lockFacing)
        {
            actor.movement.UnlockFacing();
        }

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Impulse] Stop — isFinish={isFinish}");
        }
    }
}
