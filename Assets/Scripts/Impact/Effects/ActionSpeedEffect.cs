using System;
using UnityEngine;

/// <summary>
/// Applies temporary Action and movement speed modifiers to the attacker and,
/// optionally, the target. Lifetime is advanced explicitly by the combat fixed tick.
/// </summary>
public sealed class ActionSpeedEffect : ImpactEffect
{
    internal const int TargetStartDelayTicks = 4;

    private ActionPlayer _attackerPlayer;
    private ActionPlayer _targetPlayer;
    private ActorMotor _attackerMotor;
    private ActorMotor _targetMotor;
    private Actor _attackerActor;
    private Actor _targetActor;

    private SpeedModifierToken _attackerActionToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _targetActionToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _attackerMovementToken = SpeedModifierToken.Invalid;
    private SpeedModifierToken _targetMovementToken = SpeedModifierToken.Invalid;

    private int _durationTicks;
    private long _ageTicks;
    private float _speedScale = 1f;
    private bool _targetPending;
    private bool _released = true;

    public override bool IsActive =>
        !_released
        && (HasAttackerTokens || _targetPending || HasTargetTokens);

    private bool HasAttackerTokens =>
        _attackerActionToken.IsValid || _attackerMovementToken.IsValid;

    private bool HasTargetTokens =>
        _targetActionToken.IsValid || _targetMovementToken.IsValid;

    public void Execute(ImpactData impactData, SpeedEffectConfig config)
    {
        Reset();

        if (impactData == null || config == null || !config.enabled)
            return;

        if (!TryValidateConfig(config, out int durationTicks, out string failureReason))
        {
            Debug.LogWarning($"[ActionSpeedEffect] Effect was rejected: {failureReason}",
                impactData.Attacker != null ? impactData.Attacker : impactData.TargetObject);
            return;
        }

        if (durationTicks == 0 || config.speedScale >= 1f)
            return;

        ResolveParticipants(impactData, config.affectBothParties);
        ReleaseInvalidParticipants();
        bool hasAttacker = IsUsable(_attackerPlayer) || IsUsable(_attackerMotor);
        bool hasTarget = config.affectBothParties
                         && (IsUsable(_targetPlayer) || IsUsable(_targetMotor));
        if (!hasAttacker && !hasTarget)
        {
            Reset();
            return;
        }

        _durationTicks = durationTicks;
        _speedScale = config.speedScale;
        _ageTicks = 0;
        _targetPending = hasTarget;
        _released = false;

        try
        {
            if (hasAttacker)
                ApplyAttackerSpeed();
        }
        catch (Exception exception)
        {
            Reset();
            Debug.LogException(exception,
                impactData.Attacker != null ? impactData.Attacker : impactData.TargetObject);
        }
    }

    /// <summary>
    /// Advances exactly one combat tick. This is the only lifetime clock used by
    /// ActionSpeedEffect; ordinary MonoBehaviour Update calls do not affect it.
    /// </summary>
    internal bool AdvanceFixedTick()
    {
        if (!IsActive)
            return false;

        _ageTicks++;
        ReleaseInvalidParticipants();

        if (HasAttackerTokens && _ageTicks > _durationTicks)
            ReleaseAttackerTokens();

        if (_targetPending && _ageTicks == TargetStartDelayTicks + 1)
        {
            _targetPending = false;
            ReleaseInvalidParticipants();
            if (IsUsable(_targetPlayer) || IsUsable(_targetMotor))
            {
                try
                {
                    ApplyTargetSpeed();
                }
                catch (Exception exception)
                {
                    ReleaseTargetTokens();
                    Debug.LogException(exception,
                        _targetPlayer != null ? _targetPlayer : _targetMotor);
                }
            }
        }

        if (HasTargetTokens && _ageTicks > (long)TargetStartDelayTicks + _durationTicks)
            ReleaseTargetTokens();

        if (HasAttackerTokens || _targetPending || HasTargetTokens)
            return true;

        Reset();
        return false;
    }

    /// <summary>
    /// Kept for the ImpactEffect contract. Fixed combat ticks own progression.
    /// </summary>
    public override bool Update()
    {
        return IsActive;
    }

    public override void Reset()
    {
        ReleaseAttackerTokens();
        ReleaseTargetTokens();

        _attackerPlayer = null;
        _targetPlayer = null;
        _attackerMotor = null;
        _targetMotor = null;
        _attackerActor = null;
        _targetActor = null;
        _durationTicks = 0;
        _ageTicks = 0;
        _speedScale = 1f;
        _targetPending = false;
        _released = true;
    }

    internal static bool TryConvertDurationToTicks(float durationSeconds, out int ticks)
    {
        ticks = 0;
        if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0f)
            return false;

        double exactTicks = (double)durationSeconds * CombatSimulationTiming.FrameRate;
        if (double.IsNaN(exactTicks) || double.IsInfinity(exactTicks) || exactTicks > int.MaxValue)
            return false;

        double nearestInteger = Math.Round(exactTicks);
        // Snap only when the exact frame boundary rounds back to the supplied float.
        // This accounts for float precision at any magnitude without erasing tiny
        // positive durations or rounding a distinguishable over-boundary value down.
        if (nearestInteger > 0d
            && (float)(nearestInteger / CombatSimulationTiming.FrameRate) == durationSeconds)
            exactTicks = nearestInteger;

        ticks = (int)Math.Ceiling(exactTicks);
        return true;
    }

    internal static bool TryValidateConfig(
        SpeedEffectConfig config,
        out int durationTicks,
        out string failureReason)
    {
        durationTicks = 0;
        failureReason = null;
        if (config == null)
        {
            failureReason = "config is missing.";
            return false;
        }

        if (!TryConvertDurationToTicks(config.duration, out durationTicks))
        {
            failureReason = "duration must be finite, non-negative, and representable in combat ticks.";
            return false;
        }

        if (float.IsNaN(config.speedScale)
            || float.IsInfinity(config.speedScale)
            || config.speedScale < 0f
            || config.speedScale > 1f)
        {
            failureReason = "speed scale must be finite and within [0, 1].";
            return false;
        }

        return true;
    }

    private void ResolveParticipants(ImpactData impactData, bool affectTarget)
    {
        _attackerPlayer = impactData.Attacker != null
            ? impactData.Attacker.GetComponentInChildren<ActionPlayer>()
            : null;
        _attackerMotor = ResolveMotor(impactData.Attacker != null
            ? impactData.Attacker.gameObject
            : null, _attackerPlayer);
        _attackerActor = impactData.Attacker != null
            ? impactData.Attacker.GetComponentInParent<Actor>()
            : null;

        if (!affectTarget || impactData.TargetObject == null)
            return;

        _targetPlayer = impactData.TargetObject.GetComponentInParent<ActionPlayer>();
        if (_targetPlayer == null)
            _targetPlayer = impactData.TargetObject.GetComponentInChildren<ActionPlayer>();
        _targetMotor = ResolveMotor(impactData.TargetObject, _targetPlayer);
        _targetActor = impactData.TargetObject.GetComponentInParent<Actor>();
    }

    private void ApplyAttackerSpeed()
    {
        if (IsUsable(_attackerPlayer))
            _attackerActionToken = _attackerPlayer.AddExternalSpeedModifier(
                _speedScale, SpeedModifierBlendMode.Min, "SpeedEffect_Attacker_Action");

        if (IsUsable(_attackerMotor))
            _attackerMovementToken = _attackerMotor.AddMovementTimeScaleModifier(
                _speedScale, SpeedModifierBlendMode.Min, "SpeedEffect_Attacker_Movement");
    }

    private void ApplyTargetSpeed()
    {
        if (IsUsable(_targetPlayer))
            _targetActionToken = _targetPlayer.AddExternalSpeedModifier(
                _speedScale, SpeedModifierBlendMode.Min, "SpeedEffect_Target_Action");

        if (IsUsable(_targetMotor))
            _targetMovementToken = _targetMotor.AddMovementTimeScaleModifier(
                _speedScale, SpeedModifierBlendMode.Min, "SpeedEffect_Target_Movement");
    }

    private void ReleaseInvalidParticipants()
    {
        // Forget invalid components even before their delayed token is acquired.
        // Re-enabling them must not revive an older hit's pending application.
        if (!IsActorUsable(_attackerActor) || !IsUsable(_attackerPlayer))
        {
            if (_attackerPlayer != null && _attackerActionToken.IsValid)
                _attackerPlayer.RemoveExternalSpeedModifier(_attackerActionToken);
            _attackerActionToken = SpeedModifierToken.Invalid;
            _attackerPlayer = null;
        }
        if (!IsActorUsable(_attackerActor) || !IsUsable(_attackerMotor))
        {
            if (_attackerMotor != null && _attackerMovementToken.IsValid)
                _attackerMotor.RemoveMovementTimeScaleModifier(_attackerMovementToken);
            _attackerMovementToken = SpeedModifierToken.Invalid;
            _attackerMotor = null;
        }
        if (!IsActorUsable(_targetActor) || !IsUsable(_targetPlayer))
        {
            if (_targetPlayer != null && _targetActionToken.IsValid)
                _targetPlayer.RemoveExternalSpeedModifier(_targetActionToken);
            _targetActionToken = SpeedModifierToken.Invalid;
            _targetPlayer = null;
        }
        if (!IsActorUsable(_targetActor) || !IsUsable(_targetMotor))
        {
            if (_targetMotor != null && _targetMovementToken.IsValid)
                _targetMotor.RemoveMovementTimeScaleModifier(_targetMovementToken);
            _targetMovementToken = SpeedModifierToken.Invalid;
            _targetMotor = null;
        }

        if (_targetPending && !IsUsable(_targetPlayer) && !IsUsable(_targetMotor))
            _targetPending = false;
    }

    private void ReleaseAttackerTokens()
    {
        if (_attackerActionToken.IsValid && _attackerPlayer != null)
            _attackerPlayer.RemoveExternalSpeedModifier(_attackerActionToken);
        if (_attackerMovementToken.IsValid && _attackerMotor != null)
            _attackerMotor.RemoveMovementTimeScaleModifier(_attackerMovementToken);

        _attackerActionToken = SpeedModifierToken.Invalid;
        _attackerMovementToken = SpeedModifierToken.Invalid;
    }

    private void ReleaseTargetTokens()
    {
        if (_targetActionToken.IsValid && _targetPlayer != null)
            _targetPlayer.RemoveExternalSpeedModifier(_targetActionToken);
        if (_targetMovementToken.IsValid && _targetMotor != null)
            _targetMotor.RemoveMovementTimeScaleModifier(_targetMovementToken);

        _targetActionToken = SpeedModifierToken.Invalid;
        _targetMovementToken = SpeedModifierToken.Invalid;
    }

    private static ActorMotor ResolveMotor(GameObject origin, ActionPlayer player)
    {
        Actor actor = player != null ? player.GetComponentInParent<Actor>() : null;
        if (actor == null && origin != null)
            actor = origin.GetComponentInParent<Actor>();
        if (actor != null && actor.actorMotor != null)
            return actor.actorMotor;

        ActorMotor motor = player != null ? player.GetComponentInParent<ActorMotor>() : null;
        if (motor == null && origin != null)
            motor = origin.GetComponentInParent<ActorMotor>();
        return motor;
    }

    private static bool IsUsable(Behaviour behaviour)
    {
        return behaviour != null && behaviour.isActiveAndEnabled;
    }

    private static bool IsActorUsable(Actor actor)
    {
        // A participant may have no Actor. A previously resolved but destroyed
        // Actor, unlike a genuine null, must invalidate its remaining components.
        return ReferenceEquals(actor, null) || (actor != null && actor.isActiveAndEnabled);
    }
}
