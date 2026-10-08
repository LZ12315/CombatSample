using System;
using UnityEngine;

/// <summary>
/// The single fixed-simulation entry owned by one Actor.
/// Gameplay subsystems remain behind this boundary instead of registering
/// individual clips, conditions, or sessions with the world Driver.
/// </summary>
internal sealed class ActorSimulationRuntime
{
    private readonly Actor _actor;
    private ActionPlayer _actionPlayer;
    private ActionStateManager _actionStateManager;
    private ActorMotor _actorMotor;
    private ActorAnimation _actorAnimation;
    private ActorLocomotion _actorLocomotion;
    private ActionPlayer _tickActionPlayer;
    private bool _playedActionFrameThisTick;
    private bool _tickClosed;
    private readonly ActorHitBoxRuntime _hitBoxes;

    public ActorSimulationRuntime(Actor actor)
    {
        _actor = actor;
        _hitBoxes = new ActorHitBoxRuntime(actor);
    }

    public bool IsActive => _actor != null && _actor.isActiveAndEnabled;
    public int StableId => _actor != null ? _actor.GetInstanceID() : 0;

    public ActorHitBoxRuntime HitBoxes => _hitBoxes;

    public void Control(float deltaSeconds)
    {
        if (!IsActive)
            return;

        ResolveActorAnimation()?.BeginFixedAnimationTick();

        ActorMotor motor = ResolveActorMotor();
        ActorLocomotion locomotion = ResolveActorLocomotion();
        if (motor != null && motor.MovementTimeScale <= 0f)
        {
            locomotion?.HoldControlTick();
            return;
        }

        locomotion?.BeginControlTick();
    }

    public void DecideAction()
    {
        if (!IsActive)
            return;

        if (ShouldFreezeActionArbitration())
            return;

        ResolveActionStateManager()?.DecideAction();
    }

    public bool AdvanceAction(float deltaSeconds)
    {
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
        _tickClosed = false;

        if (!IsActive)
            return false;

        _tickActionPlayer = ResolveActionPlayer();
        _playedActionFrameThisTick = _tickActionPlayer != null && _tickActionPlayer.PlayActionFrame(deltaSeconds);
        return _playedActionFrameThisTick;
    }

    public void EvaluateAnimation(float deltaSeconds)
    {
        if (!IsActive)
            return;

        ActionPlayer actionPlayer = _tickActionPlayer ?? ResolveActionPlayer();
        bool hasAction = actionPlayer != null && actionPlayer.CurrentAction != null;
        ActorMotor actorMotor = ResolveActorMotor();
        float? movementTimeScale = actorMotor != null ? actorMotor.MovementTimeScale : (float?)null;
        float animationDeltaSeconds = CalculateAnimationDeltaSeconds(
            deltaSeconds,
            hasAction,
            actionPlayer != null && actionPlayer.IsPaused,
            actionPlayer != null ? actionPlayer.PlaybackSpeed : 1d,
            movementTimeScale);
        ActorAnimation actorAnimation = ResolveActorAnimation();
        if (actorAnimation == null)
            return;

        ActorLocomotion locomotion = ResolveActorLocomotion();
        locomotion?.UpdateAnimation(actorAnimation, actorMotor, animationDeltaSeconds);
        actorAnimation.Evaluate(animationDeltaSeconds);
#if UNITY_EDITOR
        locomotion?.TraceEvaluatedAnimationTick(actorAnimation);
#endif
    }

    internal static float CalculateAnimationDeltaSeconds(
        float deltaSeconds,
        bool hasAction,
        bool actionPaused,
        double playbackSpeed,
        float? movementTimeScale)
    {
        if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            return 0f;

        float safeMovementScale = movementTimeScale.HasValue && IsFiniteNonNegative(movementTimeScale.Value)
            ? movementTimeScale.Value : 1f;
        if (!hasAction)
            return deltaSeconds * safeMovementScale;
        if (actionPaused)
            return 0f;

        float safePlaybackScale = double.IsNaN(playbackSpeed) || double.IsInfinity(playbackSpeed) || playbackSpeed < 0d
            ? 1f
            : (float)Math.Min(playbackSpeed, float.MaxValue);
        return deltaSeconds * (movementTimeScale.HasValue
            ? Mathf.Min(safePlaybackScale, safeMovementScale)
            : safePlaybackScale);
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    public void PrepareMotion(float deltaSeconds)
    {
        if (!IsActive)
            return;

        ActorMotor motor = ResolveActorMotor();
        if (motor == null)
            return;

        LocomotionMotionContext context = motor.BeginMotion(deltaSeconds);
        ActorLocomotion locomotion = ResolveActorLocomotion();
        LocomotionMotionRequest request = locomotion != null
            ? locomotion.BuildMotionRequest(context)
            : default;
        motor.ComposeMotion(request);
    }

    public void PublishWorldResult()
    {
        if (!IsActive)
            return;

        ResolveActorMotor()?.PublishWorldResult();
    }

    public void QueryHits(CombatHitBuffer buffer)
    {
        if (!_tickClosed && _playedActionFrameThisTick)
            _hitBoxes.DetectHits(buffer);
    }

    public void FinishFrame()
    {
        if (_tickClosed)
            return;

        _tickClosed = true;
        _tickActionPlayer?.FinishActionFrame();
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
    }

    public void CancelFrame()
    {
        ResolveActionStateManager()?.AbortQueuedActionRequests();
        ResolveActorMotor()?.CancelPreparedMotion();
        ResolveActorLocomotion()?.CancelSimulation();

        if (_tickClosed)
        {
            ResolveActorAnimation()?.CancelFixedAnimationTick();
            _hitBoxes.Clear();
            _playedActionFrameThisTick = false;
            return;
        }

        _tickClosed = true;
        (_tickActionPlayer ?? ResolveActionPlayer())?.CancelAction();
        ResolveActorAnimation()?.CancelFixedAnimationTick();
        _hitBoxes.Clear();
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
    }

    private ActionPlayer ResolveActionPlayer()
    {
        if (_actionPlayer == null && _actor != null)
            _actionPlayer = _actor.actionPlayer != null
                ? _actor.actionPlayer
                : _actor.GetComponent<ActionPlayer>();

        return _actionPlayer != null && _actionPlayer.isActiveAndEnabled
            ? _actionPlayer
            : null;
    }

    private ActionStateManager ResolveActionStateManager()
    {
        if (_actionStateManager == null && _actor != null)
            _actionStateManager = _actor.actionManager != null
                ? _actor.actionManager
                : _actor.GetComponent<ActionStateManager>();

        return _actionStateManager != null && _actionStateManager.isActiveAndEnabled
            ? _actionStateManager
            : null;
    }

    private ActorMotor ResolveActorMotor()
    {
        if (_actorMotor == null && _actor != null)
            _actorMotor = _actor.actorMotor != null
                ? _actor.actorMotor
                : _actor.GetComponent<ActorMotor>();

        return _actorMotor != null && _actorMotor.isActiveAndEnabled
            ? _actorMotor
            : null;
    }

    private ActorAnimation ResolveActorAnimation()
    {
        if (_actorAnimation == null && _actor != null)
            _actorAnimation = _actor.actorAnimation != null
                ? _actor.actorAnimation
                : _actor.GetComponent<ActorAnimation>();

        return _actorAnimation != null && _actorAnimation.isActiveAndEnabled
            ? _actorAnimation
            : null;
    }

    private ActorLocomotion ResolveActorLocomotion()
    {
        if (_actorLocomotion == null && _actor != null)
            _actorLocomotion = _actor.actorLocomotion != null
                ? _actor.actorLocomotion
                : _actor.GetComponent<ActorLocomotion>();

        return _actorLocomotion != null && _actorLocomotion.isActiveAndEnabled
            ? _actorLocomotion
            : null;
    }

    private bool ShouldFreezeActionArbitration()
    {
        ActionPlayer actionPlayer = ResolveActionPlayer();
        if (actionPlayer == null || actionPlayer.CurrentAction == null)
            return false;

        return actionPlayer.IsPaused || actionPlayer.PlaybackSpeed <= 0.0;
    }
}
