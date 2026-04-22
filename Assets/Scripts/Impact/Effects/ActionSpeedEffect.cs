using UnityEngine;

/// <summary>
/// 控制攻击者（可选受击者）的 ActionPlayer Timeline 播放速度，实现 HitStop/HitStick 效果。
/// 取代原有的 AttackerSpeedEffect，支持通过 affectBothParties 参数控制是否双方冻结。
/// 攻击方在命中当帧立即降速；受击方见下方「受击方延迟缓速」不变量。
/// </summary>
public class ActionSpeedEffect : ImpactEffect
{
    private ActionPlayer _attackerPlayer;
    private ActionPlayer _targetPlayer;
    private double _attackerOriginalSpeed;
    private double _targetOriginalSpeed;

    private float _duration;
    private float _speedScale;
    private bool _affectTarget;
    private float _elapsed;
    /// <summary>本实例结束时机（墙钟，含受击方延迟时把 TARGET_START_DELAY 算入）。</summary>
    private float _effectEndTime;

    // --- 受击方延迟缓速（设计不变量，勿在重构中移除）---
    // 受击方不得在命中当帧立即 SetSpeed。必须先保留若干帧的满速/正常播放时间，
    // 让受击动作从 Idle/上一动作切入反应姿态，再开始缓速，否则易与 Director/Animancer
    // 的首帧一起被拉慢而出现 T-pose 观感。此延迟与「攻击方当帧即缓速」是刻意不对称的。
    /// <summary>受击方 <see cref="SetSpeed"/> 开始生效前的延迟（秒）。</summary>
    private const float TARGET_START_DELAY = 0.06666667f; // 4/60s at 60fps
    private bool _targetSpeedApplied;

    public override bool IsActive => _elapsed < _effectEndTime && (_attackerPlayer != null || _targetPlayer != null);

    /// <summary>
    /// 执行速度效果。
    /// </summary>
    public void Execute(ImpactData impactData, SpeedEffectConfig config)
    {
        if (config == null || !config.enabled) return;

        _duration = config.duration;
        _speedScale = config.speedScale;
        _affectTarget = config.affectBothParties;

        _attackerPlayer = impactData.Attacker?.GetComponentInChildren<ActionPlayer>();
        _attackerOriginalSpeed = _attackerPlayer?.PlaybackSpeed ?? 1.0;

        if (_affectTarget)
        {
            _targetPlayer = impactData.TargetObject?.GetComponentInChildren<ActionPlayer>();
            _targetOriginalSpeed = _targetPlayer?.PlaybackSpeed ?? 1.0;
        }
        else
        {
            _targetPlayer = null;
        }

        if (_attackerPlayer == null && _targetPlayer == null)
        {
            return;
        }

        // 总时长：仅当存在「需要前摇后再缓速的受击方」时，才把 TARGET_START_DELAY 计入（与配置 duration 的叠加语义一致）。仅攻击方时不再额外多等这段延迟。
        bool hasDelayedTarget = _affectTarget && _targetPlayer != null;
        _effectEndTime = _duration + (hasDelayedTarget ? TARGET_START_DELAY : 0f);
        // duration=0 且仅攻击方时 _effectEndTime 为 0，IsActive 恒假会导致无法入队、SetSpeed 永不被 Reset；至少保留一拍更新。
        if (_effectEndTime <= 0f)
            _effectEndTime = 0.0001f;

        _elapsed = 0f;
        _targetSpeedApplied = false;

        // 攻击者：当帧即降速（无 TARGET_START_DELAY）。
        if (_attackerPlayer != null)
            _attackerPlayer.SetSpeed(_speedScale);

        // 受击者：TARGET_START_DELAY 后再 SetSpeed（不变量，见上）。
    }

    private void ApplyTargetSpeed()
    {
        if (!_affectTarget || _targetPlayer == null) return;
        _targetPlayer.SetSpeed(_speedScale);
        _targetSpeedApplied = true;
    }

    public override bool Update()
    {
        if (!IsActive) return false;

        _elapsed += Time.unscaledDeltaTime;

        if (!_targetSpeedApplied && hasDelayedTarget() && _elapsed >= TARGET_START_DELAY)
        {
            ApplyTargetSpeed();
        }

        if (_elapsed >= _effectEndTime)
        {
            // 恢复速度由 ImpactSystem 在 Update 返回 false 后统一调用 Reset()，避免与 Update 内重复清理。
            return false;
        }

        return true;
    }

    private bool hasDelayedTarget()
    {
        return _affectTarget && _targetPlayer != null;
    }

    public override void Reset()
    {
        if (_attackerPlayer != null)
            _attackerPlayer.SetSpeed(_attackerOriginalSpeed);
        if (_affectTarget && _targetPlayer != null)
            _targetPlayer.SetSpeed(_targetOriginalSpeed);

        _attackerPlayer = null;
        _targetPlayer = null;
        _elapsed = 0f;
    }
}
