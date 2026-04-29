using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
/// - OnClipStart：按方向模式解析世界方向，一次性注入水平 + 垂直冲量；可选通过 ClipGravity owner 短暂覆盖重力
/// - OnClipUpdate：无逻辑（水平由 Movement 的 drag 自然衰减，垂直由重力自然衰减）
/// - OnClipStop：结束 ClipGravity owner，恢复到 Action baseline（见 <see cref="ActorMovement.EndClipGravity"/>）
///
/// 方向解析已抽到 MotionDirectionResolver 公共工具，与 VelocityClip 共用同一套逻辑。
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    /// <summary>缓存的 Instigator Transform（FromInstigator 模式下解析方向时使用）</summary>
    private Transform _instigatorTransform;
    private Actor _cachedActor;
    private ActorMovement _cachedMovement;

    private MotionControlOwner _gravityOwner;
    private bool _didBeginClipGravity;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.movement == null || config == null) return;

        _cachedActor = actor;
        _cachedMovement = actor.movement;

        // 1. 根据方向模式计算冲量方向（世界空间，已水平化 + 归一化）
        _instigatorTransform = null;
        Vector3 horizontalDirection = MotionDirectionResolver.Resolve(
            config.directionMode,
            _cachedActor,
            actionInstance,
            config.fixedLocalDirection,
            ref _instigatorTransform);

        // 2. 一次性注入水平冲量（由 Movement 的 drag 自然衰减）
        if (Mathf.Abs(config.horizontalForce) > 0.001f)
        {
            _cachedMovement.AddHorizontalImpulse(horizontalDirection * config.horizontalForce);
        }

        // 3. 一次性注入垂直冲量（AddVerticalImpulse 内部会先截 0 再累加，保证二段跳手感）
        if (Mathf.Abs(config.verticalForce) > 0.001f)
        {
            _cachedMovement.AddVerticalImpulse(config.verticalForce);
        }

        _didBeginClipGravity = false;
        if (config.gravityScale >= 0f)
        {
            _gravityOwner = _cachedMovement.BeginClipGravity(config.gravityScale, nameof(ActionImpulseBehavior));
            _didBeginClipGravity = true;
        }

        if (config.debugLog)
        {
            string gravityDesc = _didBeginClipGravity ? config.gravityScale.ToString() : "(not overridden)";
            Debug.Log($"[Impulse] Start — dir={horizontalDirection}, hForce={config.horizontalForce}, " +
                      $"vForce={config.verticalForce}, gravity={gravityDesc}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // 冲量在 OnClipStart 一次性注入完成；Update 无需逐帧写入。
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (_cachedMovement == null) return;

        if (_didBeginClipGravity)
        {
            _cachedMovement.EndClipGravity(_gravityOwner);
            _didBeginClipGravity = false;
        }

        if (config != null && config.debugLog)
        {
            Debug.Log($"[Impulse] Stop — isFinish={isFinish}");
        }

        _instigatorTransform = null;
        _cachedActor = null;
        _cachedMovement = null;
    }
}
