using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
/// - OnClipStart：按方向模式解析世界方向，一次性注入水平 + 垂直冲量
/// - OnClipUpdate：无逻辑（水平由 Movement 的 drag 自然衰减，垂直由重力自然衰减）
/// - OnClipStop：无运动学恢复（整招重力等由 ActionMotionConfig / OnExit 负责）
///
/// 方向解析已抽到 MotionDirectionResolver 公共工具，与 VelocityClip 共用同一套逻辑。
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    /// <summary>缓存的 Instigator Transform（FromInstigator 模式下解析方向时使用）</summary>
    private Transform _instigatorTransform;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.movement == null || config == null) return;

        // 1. 根据方向模式计算冲量方向（世界空间，已水平化 + 归一化）
        _instigatorTransform = null;
        Vector3 horizontalDirection = MotionDirectionResolver.Resolve(
            config.directionMode,
            actor,
            actionInstance,
            config.fixedLocalDirection,
            ref _instigatorTransform);

        // 2. 一次性注入水平冲量（由 Movement 的 drag 自然衰减）
        if (Mathf.Abs(config.horizontalForce) > 0.001f)
        {
            actor.movement.AddHorizontalImpulse(horizontalDirection * config.horizontalForce);
        }

        // 3. 一次性注入垂直冲量（AddVerticalImpulse 内部会先截 0 再累加，保证二段跳手感）
        if (Mathf.Abs(config.verticalForce) > 0.001f)
        {
            actor.movement.AddVerticalImpulse(config.verticalForce);
        }

        if (config.debugLog)
        {
            Debug.Log($"[Impulse] Start — dir={horizontalDirection}, hForce={config.horizontalForce}, " +
                      $"vForce={config.verticalForce}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // 新架构下 ImpulseClip 的能量在 OnClipStart 一次性注入完成，
        // 水平由 Movement 的 drag 衰减，垂直由重力衰减，Update 不再需要逐帧写入。
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.movement == null) return;

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Impulse] Stop — isFinish={isFinish}");
        }

        _instigatorTransform = null;
    }
}
