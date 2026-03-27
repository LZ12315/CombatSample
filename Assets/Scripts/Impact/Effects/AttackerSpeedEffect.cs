using System;
using UnityEngine;

/// <summary>
/// 合并 HitStop + HitStick：只改攻击者 ActionPlayer 的 Timeline 播放速度，不改 Time.timeScale。
/// 同时开启时，在各自持续时间内取 min(停顿速度, 黏滞速度)；总时长为两者持续时间的较大值。
/// </summary>
public class AttackerSpeedEffect : ImpactEffect
{
    private ActionPlayer _actionPlayer;
    private double _originalSpeed;

    private bool _enableStop;
    private bool _enableStick;
    private float _stopDuration;
    private float _stickDuration;
    private float _stopSpeed;
    private float _stickSpeed;

    private float _elapsed;
    private float _totalDuration;

    public override bool IsActive => _elapsed < _totalDuration && _actionPlayer != null;

    public void Execute(ActorCombater attacker, HitStopEffectConfig hitStopConfig, HitStickEffectConfig hitStickConfig)
    {
        _enableStop = hitStopConfig != null && hitStopConfig.enabled;
        _enableStick = hitStickConfig != null && hitStickConfig.enabled;
        if (!_enableStop && !_enableStick) return;

        if (attacker == null) return;

        _actionPlayer = attacker.GetComponent<ActionPlayer>()
                        ?? attacker.GetComponentInChildren<ActionPlayer>();
        if (_actionPlayer == null) return;

        _stopDuration = _enableStop ? hitStopConfig.duration : 0f;
        _stickDuration = _enableStick ? hitStickConfig.duration : 0f;
        _totalDuration = Mathf.Max(_stopDuration, _stickDuration);
        if (_totalDuration <= 0f) return;

        _stopSpeed = _enableStop ? hitStopConfig.timeScale : 1f;
        _stickSpeed = _enableStick ? hitStickConfig.speedScale : 1f;

        _originalSpeed = _actionPlayer.PlaybackSpeed;
        _elapsed = 0f;

        _actionPlayer.SetSpeed(ComputeSpeedAtElapsed(0f));
    }

    private double ComputeSpeedAtElapsed(float t)
    {
        bool stopActive = _enableStop && t < _stopDuration;
        bool stickActive = _enableStick && t < _stickDuration;

        if (stopActive && stickActive)
            return Math.Min(_stopSpeed, _stickSpeed);
        if (stopActive)
            return _stopSpeed;
        if (stickActive)
            return _stickSpeed;
        return _originalSpeed;
    }

    public override bool Update()
    {
        if (!IsActive) return false;

        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed >= _totalDuration)
        {
            if (_actionPlayer != null)
                _actionPlayer.SetSpeed(_originalSpeed);
            return false;
        }

        if (_actionPlayer != null)
            _actionPlayer.SetSpeed(ComputeSpeedAtElapsed(_elapsed));

        return true;
    }

    public override void Reset()
    {
        if (_actionPlayer != null)
            _actionPlayer.SetSpeed(_originalSpeed);

        _actionPlayer = null;
        _elapsed = 0f;
        _totalDuration = 0f;
    }
}
