using System;
using UnityEngine;

/// <summary>
/// 合并 HitStop + HitStick：只改攻击者 ActionPlayer 的 Timeline 播放速度，不改 Time.timeScale。
/// 同时开启时，在各自持续时间内取 min(停顿速度, 黏滞速度)；总时长为两者持续时间的较大值。
/// </summary>
[Obsolete("请使用 ActionSpeedEffect 配合 SpeedEffectConfig，支持双方冻结")]
public class AttackerSpeedEffect : ImpactEffect
{
    private ActionPlayer _actionPlayer;

    private bool _enableStop;
    private bool _enableStick;
    private float _stopDuration;
    private float _stickDuration;
    private float _stopSpeed;
    private float _stickSpeed;

    private SpeedModifierToken _stopToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _stickToken = SpeedModifierToken.Invalid;

    private float _elapsed;
    private float _totalDuration;
    private bool _released;

    public override bool IsActive => !_released && _elapsed < _totalDuration && _actionPlayer != null;

    public void Execute(ActorCombater attacker, HitStopEffectConfig hitStopConfig, HitStickEffectConfig hitStickConfig)
    {
        Reset();

        _enableStop = hitStopConfig != null && hitStopConfig.enabled;
        _enableStick = hitStickConfig != null && hitStickConfig.enabled;
        if (!_enableStop && !_enableStick)
            return;

        if (attacker == null)
            return;

        _actionPlayer = attacker.GetComponent<ActionPlayer>()
                        ?? attacker.GetComponentInChildren<ActionPlayer>();
        if (_actionPlayer == null)
            return;

        _stopDuration = _enableStop ? hitStopConfig.duration : 0f;
        _stickDuration = _enableStick ? hitStickConfig.duration : 0f;
        _totalDuration = Mathf.Max(_stopDuration, _stickDuration);
        if (_totalDuration <= 0f)
            return;

        _stopSpeed = _enableStop ? hitStopConfig.timeScale : 1f;
        _stickSpeed = _enableStick ? hitStickConfig.speedScale : 1f;

        _elapsed = 0f;
        _released = false;

        if (_enableStop && _stopDuration > 0f)
        {
            _stopToken = _actionPlayer.AddExternalSpeedModifier(
                _stopSpeed,
                SpeedModifierBlendMode.Min,
                "LegacyHitStop_Action");
        }

        if (_enableStick && _stickDuration > 0f)
        {
            _stickToken = _actionPlayer.AddExternalSpeedModifier(
                _stickSpeed,
                SpeedModifierBlendMode.Min,
                "LegacyHitStick_Action");
        }
    }

    public override bool Update()
    {
        if (!IsActive)
            return false;

        _elapsed += Time.unscaledDeltaTime;

        if (_actionPlayer != null && _stopToken.IsValid && _elapsed >= _stopDuration)
        {
            _actionPlayer.RemoveExternalSpeedModifier(_stopToken);
            _stopToken = SpeedModifierToken.Invalid;
        }

        if (_actionPlayer != null && _stickToken.IsValid && _elapsed >= _stickDuration)
        {
            _actionPlayer.RemoveExternalSpeedModifier(_stickToken);
            _stickToken = SpeedModifierToken.Invalid;
        }

        return _elapsed < _totalDuration;
    }

    public override void Reset()
    {
        ReleaseTokens();

        _actionPlayer = null;
        _elapsed = 0f;
        _totalDuration = 0f;
        _stopDuration = 0f;
        _stickDuration = 0f;
        _stopSpeed = 1f;
        _stickSpeed = 1f;
        _enableStop = false;
        _enableStick = false;
    }

    private void ReleaseTokens()
    {
        if (_released)
            return;

        _released = true;

        if (_actionPlayer != null)
        {
            _actionPlayer.RemoveExternalSpeedModifier(_stopToken);
            _actionPlayer.RemoveExternalSpeedModifier(_stickToken);
        }

        _stopToken = SpeedModifierToken.Invalid;
        _stickToken = SpeedModifierToken.Invalid;
    }
}
