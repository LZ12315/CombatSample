using UnityEngine;

/// <summary>
/// 控制攻击者（可选受击者）的 ActionPlayer Timeline 播放速度，实现 HitStop/HitStick 效果；
/// 并同步 <see cref="ActorMotor"/> 的 movement time scale，使 KCC/重力/冲量与动画同速冻结。
/// 
/// 本效果不再缓存并恢复旧速度；它只向 ActionPlayer / ActorMotor 申请临时 modifier token，
/// 结束时释放 token，由速度所有者统一重算最终速度。
/// </summary>
public class ActionSpeedEffect : ImpactEffect
{
    private ActionPlayer _attackerPlayer;
    private ActionPlayer _targetPlayer;
    private ActorMotor _attackerMotor;
    private ActorMotor _targetMotor;

    private SpeedModifierToken _attackerActionToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _targetActionToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _attackerMovementToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _targetMovementToken = SpeedModifierToken.Invalid;

    private float _duration;
    private float _speedScale;
    private bool _affectTarget;
    private float _elapsed;

    /// <summary>本实例结束时机（墙钟，含受击方延迟时把 TARGET_START_DELAY 算入）。</summary>
    private float _effectEndTime;

    private bool _released;

    // --- 受击方延迟缓速（设计不变量，勿在重构中移除）---
    // 受击方不得在命中当帧立即申请速度 modifier。必须先保留若干帧的满速/正常播放时间，
    // 让受击动作从 Idle/上一动作切入反应姿态，再开始缓速，否则易与 Director/Animancer
    // 的首帧一起被拉慢而出现 T-pose 观感。此延迟与「攻击方当帧即缓速」是刻意不对称的。
    /// <summary>受击方速度 modifier 开始生效前的延迟（秒）。</summary>
    private const float TARGET_START_DELAY = 0.06666667f; // 4/60s at 60fps

    private bool _targetSpeedApplied;

    public override bool IsActive =>
        !_released &&
        _elapsed < _effectEndTime &&
        (_attackerPlayer != null || _targetPlayer != null || _attackerMotor != null || _targetMotor != null);

    /// <summary>
    /// 执行速度效果。
    /// </summary>
    public void Execute(ImpactData impactData, SpeedEffectConfig config)
    {
        Reset();

        if (impactData == null || config == null || !config.enabled)
            return;

        _duration = config.duration;
        _speedScale = config.speedScale;
        _affectTarget = config.affectBothParties;

        _attackerPlayer = impactData.Attacker?.GetComponentInChildren<ActionPlayer>();
        _attackerMotor = MotorFrom(_attackerPlayer);

        if (_affectTarget)
        {
            _targetPlayer = impactData.TargetObject?.GetComponentInChildren<ActionPlayer>();
            _targetMotor = MotorFrom(_targetPlayer);
        }

        if (_attackerPlayer == null && _targetPlayer == null && _attackerMotor == null && _targetMotor == null)
            return;

        // 总时长：仅当存在「需要前摇后再缓速的受击方」时，才把 TARGET_START_DELAY 计入（与配置 duration 的叠加语义一致）。
        bool hasDelayedTarget = HasDelayedTarget();
        _effectEndTime = _duration + (hasDelayedTarget ? TARGET_START_DELAY : 0f);

        // duration=0 且仅攻击方时 _effectEndTime 为 0，IsActive 恒假会导致无法入队、token 永不释放；至少保留一拍更新。
        if (_effectEndTime <= 0f)
            _effectEndTime = 0.0001f;

        _elapsed = 0f;
        _targetSpeedApplied = false;
        _released = false;

        // 攻击者：当帧即降速（无 TARGET_START_DELAY）；ActorMotor 与 Timeline 同步缩放 dt。
        ApplyAttackerSpeed();

        // 受击者：TARGET_START_DELAY 后再申请 Action / Movement modifier（不变量，见上）。
    }

    private void ApplyAttackerSpeed()
    {
        if (_attackerPlayer != null)
        {
            _attackerActionToken = _attackerPlayer.AddExternalSpeedModifier(
                _speedScale,
                SpeedModifierBlendMode.Min,
                "SpeedVFX_Attacker_Action");
        }

        if (_attackerMotor != null)
        {
            _attackerMovementToken = _attackerMotor.AddMovementTimeScaleModifier(
                _speedScale,
                SpeedModifierBlendMode.Min,
                "SpeedVFX_Attacker_Movement");
        }
    }

    private void ApplyTargetSpeed()
    {
        if (!_affectTarget || _targetSpeedApplied)
            return;

        if (_targetPlayer != null)
        {
            _targetActionToken = _targetPlayer.AddExternalSpeedModifier(
                _speedScale,
                SpeedModifierBlendMode.Min,
                "SpeedVFX_Target_Action");
        }

        if (_targetMotor != null)
        {
            _targetMovementToken = _targetMotor.AddMovementTimeScaleModifier(
                _speedScale,
                SpeedModifierBlendMode.Min,
                "SpeedVFX_Target_Movement");
        }

        _targetSpeedApplied = true;
    }

    public override bool Update()
    {
        if (!IsActive)
            return false;

        _elapsed += Time.unscaledDeltaTime;

        if (!_targetSpeedApplied && HasDelayedTarget() && _elapsed >= TARGET_START_DELAY)
        {
            ApplyTargetSpeed();
        }

        if (_elapsed >= _effectEndTime)
        {
            // token 释放由 ImpactSystem 在 Update 返回 false 后统一调用 Reset()，避免与 Update 内重复清理。
            return false;
        }

        return true;
    }

    private bool HasDelayedTarget()
    {
        return _affectTarget && (_targetPlayer != null || _targetMotor != null);
    }

    public override void Reset()
    {
        ReleaseTokens();

        _attackerPlayer = null;
        _targetPlayer = null;
        _attackerMotor = null;
        _targetMotor = null;

        _elapsed = 0f;
        _duration = 0f;
        _effectEndTime = 0f;
        _speedScale = 1f;
        _affectTarget = false;
        _targetSpeedApplied = false;
    }

    private void ReleaseTokens()
    {
        if (_released)
            return;

        _released = true;

        if (_attackerPlayer != null)
            _attackerPlayer.RemoveExternalSpeedModifier(_attackerActionToken);

        if (_targetPlayer != null)
            _targetPlayer.RemoveExternalSpeedModifier(_targetActionToken);

        if (_attackerMotor != null)
            _attackerMotor.RemoveMovementTimeScaleModifier(_attackerMovementToken);

        if (_targetMotor != null)
            _targetMotor.RemoveMovementTimeScaleModifier(_targetMovementToken);

        _attackerActionToken = SpeedModifierToken.Invalid;
        _targetActionToken = SpeedModifierToken.Invalid;
        _attackerMovementToken = SpeedModifierToken.Invalid;
        _targetMovementToken = SpeedModifierToken.Invalid;
    }



    private static ActorMotor MotorFrom(ActionPlayer player)
    {
        if (player == null)
            return null;

        var actor = player.GetComponentInParent<Actor>();
        if (actor != null && actor.actorMotor != null)
            return actor.actorMotor;

        return player.GetComponentInParent<ActorMotor>();
    }
}
