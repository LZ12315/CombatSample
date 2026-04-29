using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
<<<<<<< HEAD
/// - OnClipStart：按方向模式解析世界方向，一次性注入水平 + 垂直冲量；可选通过 ClipGravity owner 短暂覆盖重力
/// - OnClipUpdate：无逻辑（水平由 Movement 的 drag 自然衰减，垂直由重力自然衰减）
/// - OnClipStop：结束 ClipGravity owner，恢复到 Action baseline（见 <see cref="ActorMovement.EndClipGravity"/>）
=======
/// - OnClipStart：按方向模式解析世界方向，一次性注入水平 + 垂直冲量；仅当 config.gravityScale >= 0 时才临时覆盖重力
/// - OnClipUpdate：无逻辑（水平由 Movement 的 drag 自然衰减，垂直由重力自然衰减）
/// - OnClipStop：仅当 Start 时真正覆盖过重力，才恢复到 Action 层重力（见 ActionInstance.ResolveActionGravityScale）
///
/// 重力策略：
///   ImpulseClip 通常只持续 1~数帧，不再承担"整招重力"职责（那是 ActionMotionConfig 的事）。
///   只有在 config.gravityScale >= 0 时，才认为策划明确要求 Clip 级短暂覆盖重力，才生效。
///   -1（默认）= 不覆盖，保留 Action 层设置的重力不受干扰。
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
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

    /// <summary>OnClipStart 是否真正覆盖过 gravityScale。只有覆盖过才需要在 Stop 时恢复，避免把 ActionMotionConfig 设置的整招重力抹掉。</summary>
    private bool _didOverrideGravity;

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

        // 4. 设置重力缩放（仅在 config.gravityScale >= 0 时才覆盖；负值语义 = 不覆盖，交给 ActionMotionConfig）
        _didOverrideGravity = false;
        if (config.gravityScale >= 0f)
        {
            actor.movement.SetGravityScale(config.gravityScale);
            _didOverrideGravity = true;
        }

        if (config.debugLog)
        {
<<<<<<< HEAD
            string gravityDesc = _didBeginClipGravity ? config.gravityScale.ToString() : "(not overridden)";
=======
            string gravityDesc = _didOverrideGravity ? config.gravityScale.ToString() : "(not overridden)";
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
            Debug.Log($"[Impulse] Start — dir={horizontalDirection}, hForce={config.horizontalForce}, " +
                      $"vForce={config.verticalForce}, gravity={gravityDesc}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
<<<<<<< HEAD
        // 冲量在 OnClipStart 一次性注入完成；Update 无需逐帧写入。
=======
        // 新架构下 ImpulseClip 的能量在 OnClipStart 一次性注入完成，
        // 水平由 Movement._horizontalDrag 衰减，垂直由重力衰减，Update 不再需要逐帧写入。
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (_cachedMovement == null) return;

        if (_didBeginClipGravity)
        {
            _cachedMovement.EndClipGravity(_gravityOwner);
            _didBeginClipGravity = false;
        }

        // 恢复重力缩放（仅当 Start 时真正覆盖过才恢复；否则不要碰，避免抹掉 ActionMotionConfig 设置的整招重力）
        if (_didOverrideGravity)
        {
            float restoreTarget = actionInstance != null
                ? actionInstance.ResolveActionGravityScale()
                : 1f;
            actor.movement.SetGravityScale(restoreTarget);
            _didOverrideGravity = false;
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
