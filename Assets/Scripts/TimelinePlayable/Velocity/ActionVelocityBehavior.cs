using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// VelocityClip：持续覆盖水平/垂直程序速度（owner），可选临时覆盖重力。
/// 兼容旧 VelocityConfig（VerticalMode / Clamp / Release）。
/// </summary>
public class ActionVelocityBehavior : ActionBehaviourBase
{
    public VelocityConfig config;

    private Transform _instigatorTransform;
    private Actor _cachedActor;
    private ActorMovement _cachedMovement;

    private MotionControlOwner _horizontalOwner;
    private MotionControlOwner _verticalOwner;
    private MotionControlOwner _clipGravityOwner;
    private MotionControlOwner _legacyClampOwner;

    private bool _hasHorizontalOwner;
    private bool _hasVerticalOwner;
    private bool _didApplyClipGravity;
    private bool _hasLegacyClampOwner;

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
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (_cachedActor == null || _cachedMovement == null || config == null) return;

        double duration = playable.GetDuration();
        float t = duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 0f;

        bool legacyClamp = config.verticalMode == VerticalVelocityMode.ClampRange;

        // ── 水平 ──
        if (_hasHorizontalOwner && Mathf.Abs(config.horizontalSpeed) > 0.001f)
        {
            Vector3 dir = MotionDirectionResolver.Resolve(
                config.directionMode,
                _cachedActor,
                actionInstance,
                config.fixedLocalDirection,
                ref _instigatorTransform
            );
            float hCurve = config.horizontalCurve?.Evaluate(t) ?? 1f;
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
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (_cachedMovement == null) return;

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

        _instigatorTransform = null;
        _cachedActor = null;
        _cachedMovement = null;
        _hasHorizontalOwner = false;
        _hasVerticalOwner = false;
        _hasLegacyClampOwner = false;
        _didApplyClipGravity = false;
    }

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
}
