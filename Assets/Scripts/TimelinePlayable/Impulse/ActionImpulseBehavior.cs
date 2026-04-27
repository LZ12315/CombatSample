using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// ImpulseClip 运行时逻辑：
/// - OnClipStart：按方向模式解析世界方向，一次性注入水平 + 垂直冲量；仅当 config.gravityScale >= 0 时才临时覆盖重力
/// - OnClipUpdate：无逻辑（水平由 Movement 的 drag 自然衰减，垂直由重力自然衰减）
/// - OnClipStop：仅当 Start 时真正覆盖过重力，才恢复到 Action 层重力（见 ActionInstance.ResolveActionGravityScale）
///
/// 重力策略：
///   ImpulseClip 通常只持续 1~数帧，不再承担"整招重力"职责（那是 ActionMotionConfig 的事）。
///   只有在 config.gravityScale >= 0 时，才认为策划明确要求 Clip 级短暂覆盖重力，才生效。
///   -1（默认）= 不覆盖，保留 Action 层设置的重力不受干扰。
///
/// 方向解析已抽到 MotionDirectionResolver 公共工具，与 VelocityClip 共用同一套逻辑。
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    /// <summary>缓存的 Instigator Transform（FromInstigator 模式下解析方向时使用）</summary>
    private Transform _instigatorTransform;

    /// <summary>OnClipStart 是否真正覆盖过 gravityScale。只有覆盖过才需要在 Stop 时恢复，避免把 ActionMotionConfig 设置的整招重力抹掉。</summary>
    private bool _didOverrideGravity;

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

        // 4. 设置重力缩放（仅在 config.gravityScale >= 0 时才覆盖；负值语义 = 不覆盖，交给 ActionMotionConfig）
        _didOverrideGravity = false;
        if (config.gravityScale >= 0f)
        {
            actor.movement.SetGravityScale(config.gravityScale);
            _didOverrideGravity = true;
        }

        if (config.debugLog)
        {
            string gravityDesc = _didOverrideGravity ? config.gravityScale.ToString() : "(not overridden)";
            Debug.Log($"[Impulse] Start — dir={horizontalDirection}, hForce={config.horizontalForce}, " +
                      $"vForce={config.verticalForce}, gravity={gravityDesc}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // 新架构下 ImpulseClip 的能量在 OnClipStart 一次性注入完成，
        // 水平由 Movement._horizontalDrag 衰减，垂直由重力衰减，Update 不再需要逐帧写入。
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.movement == null) return;

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
    }
}
