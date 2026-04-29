using UnityEngine;
using UnityEngine.Playables;

/// <summary>
<<<<<<< HEAD
/// VelocityClip：持续覆盖水平/垂直程序速度（owner），可选临时覆盖重力。
/// 兼容旧 VelocityConfig（VerticalMode / Clamp / Release）。
=======
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
///   Clip 开始时覆盖 gravityScale，结束时恢复到 <see cref="ActionInstance.ResolveActionGravityScale"/>（整招设定；MotionConfig 为 -1 时等价 1）。
///   若多个 Clip 同时改写重力且重叠，仍无栈式嵌套——需未来再引入 scale stack。
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
/// </summary>
public class ActionVelocityBehavior : ActionBehaviourBase
{
    public VelocityConfig config;

    private Transform _instigatorTransform;
    private Actor _cachedActor;
    private ActorMovement _cachedMovement;

<<<<<<< HEAD
    private MotionControlOwner _horizontalOwner;
    private MotionControlOwner _verticalOwner;
    private MotionControlOwner _clipGravityOwner;
    private MotionControlOwner _legacyClampOwner;

    private bool _hasHorizontalOwner;
    private bool _hasVerticalOwner;
    private bool _didApplyClipGravity;
    private bool _hasLegacyClampOwner;
=======
    /// <summary>OnClipStart 记录的 gravityScale 原值，OnClipStop 时还原用。</summary>
    private float _savedGravityScale = 1f;
>>>>>>> parent of 50a4ffc (基本完成第一步整理)

    protected override void OnClipStart(Playable playable)
    {
        if (actor?.movement == null || config == null) return;

        _cachedActor = actor;
        _cachedMovement = actor.movement;
        _instigatorTransform = null;
        _horizontalOwner = default;
        _verticalOwner = default;
        _clipGravityOwner = default;
        _legacyClampOwner = default;
        _hasHorizontalOwner = false;
        _hasVerticalOwner = false;
        _didApplyClipGravity = false;
        _hasLegacyClampOwner = false;

<<<<<<< HEAD
        bool effH = EffectiveControlHorizontal();
        bool effVAuthoritative = EffectiveVerticalAuthoritative();
        bool legacyClamp = config.verticalMode == VerticalVelocityMode.ClampRange;

        if (config.overrideGravity)
        {
            _clipGravityOwner = _cachedMovement.BeginClipGravity(config.gravityScale, nameof(ActionVelocityBehavior));
            _didApplyClipGravity = true;
        }

        if (effH)
        {
            _horizontalOwner = _cachedMovement.BeginHorizontalVelocityControl(nameof(ActionVelocityBehavior));
            _hasHorizontalOwner = true;
        }

        if (effVAuthoritative && !legacyClamp)
        {
            _verticalOwner = _cachedMovement.BeginVerticalVelocityControl(nameof(ActionVelocityBehavior));
            _hasVerticalOwner = true;
        }

        if (legacyClamp)
        {
            _legacyClampOwner = _cachedMovement.BeginLegacyVerticalClamp(
                config.clampMin,
                config.clampMax,
                nameof(ActionVelocityBehavior));
            _hasLegacyClampOwner = true;
        }

        if (config.debugLog)
        {
            Debug.Log($"[Velocity] Start — effH={effH}, effVAuth={effVAuthoritative}, legacyClamp={legacyClamp}, overrideGravity={config.overrideGravity}");
=======
        _savedGravityScale = actionInstance != null
            ? actionInstance.ResolveActionGravityScale()
            : 1f;

        // 覆盖重力缩放（默认 0 = 完全浮空，由 Clip 接管垂直）
        actor.movement.SetGravityScale(config.gravityScale);

        if (config.releaseMode == VerticalReleaseMode.ResetVertical)
            actor.movement.ResetVerticalState();

        if (config.debugLog)
        {
            Debug.Log($"[Velocity] Start — gravityScale={config.gravityScale}, vMode={config.verticalMode}");
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (_cachedActor == null || _cachedMovement == null || config == null) return;

        double duration = playable.GetDuration();
        float t = duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 0f;

<<<<<<< HEAD
        bool legacyClamp = config.verticalMode == VerticalVelocityMode.ClampRange;

        // ── 水平 ──
        if (_hasHorizontalOwner && Mathf.Abs(config.horizontalSpeed) > 0.001f)
=======
        // 2. 水平通道：若速度非零则解析方向并写入外部水平通道
        if (Mathf.Abs(config.horizontalSpeed) > 0.001f)
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
        {
            Vector3 dir = MotionDirectionResolver.Resolve(
                config.directionMode,
                _cachedActor,
                actionInstance,
                config.fixedLocalDirection,
                ref _instigatorTransform
            );
            float hCurve = config.horizontalCurve?.Evaluate(t) ?? 1f;
<<<<<<< HEAD
            _cachedMovement.SetHorizontalVelocity(_horizontalOwner, dir * (config.horizontalSpeed * hCurve));
        }
        else if (_hasHorizontalOwner)
        {
            _cachedMovement.SetHorizontalVelocity(_horizontalOwner, Vector3.zero);
        }

        // ── 垂直 ──
        if (_hasVerticalOwner)
        {
            float vCurve = config.verticalCurve?.Evaluate(t) ?? 1f;
            float vy = config.verticalSpeed * vCurve;
            _cachedMovement.SetVerticalProgramVelocity(_verticalOwner, vy);
        }
        else if (legacyClamp && _hasLegacyClampOwner)
        {
            float vCurve = config.verticalCurve?.Evaluate(t) ?? 1f;
            float vy = config.verticalSpeed * vCurve;
            _cachedMovement.SetLegacyVerticalAddon(_legacyClampOwner, vy);
=======
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
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (_cachedMovement == null) return;

<<<<<<< HEAD
        if (config != null && config.releaseMode == VerticalReleaseMode.ResetVertical && isFinish)
            _cachedMovement.ResetVerticalState();

        if (_hasHorizontalOwner)
            _cachedMovement.EndHorizontalVelocityControl(_horizontalOwner);
        if (_hasVerticalOwner)
            _cachedMovement.EndVerticalVelocityControl(_verticalOwner);

        if (_hasLegacyClampOwner)
            _cachedMovement.EndLegacyVerticalClamp(_legacyClampOwner);

        if (_didApplyClipGravity)
            _cachedMovement.EndClipGravity(_clipGravityOwner);

        if (config != null && config.debugLog)
            Debug.Log($"[Velocity] Stop — isFinish={isFinish}");
=======
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
>>>>>>> parent of 50a4ffc (基本完成第一步整理)

        _instigatorTransform = null;
        _cachedActor = null;
        _cachedMovement = null;
        _hasHorizontalOwner = false;
        _hasVerticalOwner = false;
        _hasLegacyClampOwner = false;
        _didApplyClipGravity = false;
    }
<<<<<<< HEAD

    private bool EffectiveControlHorizontal()
    {
        return config.controlHorizontal || Mathf.Abs(config.horizontalSpeed) > 0.001f;
    }

    /// <summary>是否使用垂直 Velocity owner（权威覆盖），false 时可能走 Clamp 兼容路径。</summary>
    private bool EffectiveVerticalAuthoritative()
    {
        if (config.controlVertical)
            return true;
        if (config.verticalMode == VerticalVelocityMode.ClampRange)
            return false;
        if (config.verticalMode == VerticalVelocityMode.OverrideVertical)
            return true;
        return Mathf.Abs(config.verticalSpeed) > 0.001f;
    }
=======
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
}
