using System;
using UnityEngine;

internal sealed class SequenceActionPlaybackSession : IFixedActionPlaybackSession
{
    private readonly Actor _actor;
    private readonly ActionContext _context;
    private readonly ActionSequenceContext _sequenceContext = new ActionSequenceContext();
    private ActionSequenceRuntime _runtime;
    private ActorAnimation _actorAnimation;
    private ActorAnimationActionOwner _animationOwner;
    private bool _paused;
    private bool _disposed;
    private double _speed = 1.0;
    private double _frameAccumulator;

    public SequenceActionPlaybackSession(ActionInstance action, Actor actor, ActionContext context)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        _actor = actor;
        _context = context;
    }

    public ActionInstance Action { get; }
    public int CurrentFrame => _runtime != null ? Mathf.Max(0, _runtime.CurrentFrame) : 0;
    public int FrameRate => _runtime != null ? _runtime.FrameRate : 0;
    public int TotalFrames => _runtime != null ? _runtime.DurationFrames : 0;
    public double NormalizedTime => _runtime != null ? _runtime.NormalizedTime : 0;
    public bool IsPlaying => !_disposed && _runtime != null && _runtime.IsPlaying;
    public bool HasOpenFrame => !_disposed && _runtime != null && _runtime.HasOpenFrame;
    public ActionSequenceRuntimeDiagnostics Diagnostics => _runtime?.Diagnostics;

    public event Action<IActionPlaybackSession> Completed;
    public event Action<IActionPlaybackSession> Interrupted;

    public void Start()
    {
        AcquireActionAnimationOwner();
        CreateRuntimeAndContext();
    }

    public void Tick(float deltaSeconds)
    {
        throw new InvalidOperationException(
            "Sequence playback is fixed-simulation owned and cannot be advanced from Update.");
    }

    public bool TryPlayFrame(float deltaSeconds)
    {
        if (_disposed || _runtime == null || !_runtime.IsPlaying || _runtime.IsComplete)
            return false;

        if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            return false;

        _sequenceContext.Actor = _actor;

        if (_paused || _speed <= 0.0 || deltaSeconds <= 0f)
        {
            SubmitCurrentPose();
            return false;
        }

        double nextAccumulator = _frameAccumulator + deltaSeconds * _speed * CombatSimulationTiming.FrameRate;
        if (nextAccumulator < 1.0)
        {
            _frameAccumulator = nextAccumulator;
            SubmitCurrentPose();
            return false;
        }

        _frameAccumulator = nextAccumulator - 1.0;
        return _runtime.PlayFrame(
            _sequenceContext,
            CombatSimulationTiming.FixedDeltaTime,
            (float)_speed);
    }

    public void FinishFrame()
    {
        if (!HasOpenFrame)
            return;

        _runtime.FinishFrame();
        Action.UpdateNormalizedTime(NormalizedTime);

        if (_runtime.IsComplete)
            Completed?.Invoke(this);
    }

    public void Cancel()
    {
        try
        {
            if (_runtime != null && !_runtime.IsComplete)
                _runtime.Cancel(_sequenceContext);
        }
        finally
        {
            _frameAccumulator = 0.0;
        }
    }

    public void Pause()
    {
        _paused = true;
    }

    public void Resume()
    {
        _paused = false;
    }

    public void SetSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed))
            speed = 1.0;

        if (speed > 1.0 + 0.000001)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Gameplay Sequence speed must be within [0, 1].");
        }

        _speed = Math.Min(1.0, Math.Max(0.0, speed));
    }

    public void Restart()
    {
        if (_runtime != null && !_runtime.IsComplete)
            _runtime.Cancel(_sequenceContext);

        Action.ResetRuntimeData();
        _paused = false;
        ReleaseActionAnimationOwner();
        AcquireActionAnimationOwner();
        CreateRuntimeAndContext();
    }

    public void Stop(ActionPlaybackStopMode stopMode)
    {
        try
        {
            if (_runtime != null && !_runtime.IsComplete)
                _runtime.Cancel(_sequenceContext);
        }
        finally
        {
            _frameAccumulator = 0.0;
            ReleaseSessionOwnership();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            if (_runtime != null && !_runtime.IsComplete)
                _runtime.Cancel(_sequenceContext);
        }
        finally
        {
            _frameAccumulator = 0.0;
            ReleaseSessionOwnership();
        }
    }

    private void CreateRuntimeAndContext()
    {
        _runtime = new ActionSequenceRuntime(Action.Config.SequenceData);
        _frameAccumulator = 0.0;
        _sequenceContext.Actor = _actor;
        _sequenceContext.Context = _context;
        _sequenceContext.HitBoxes = _actor != null ? _actor.HitBoxes : null;
        _sequenceContext.AnimationOwner = _animationOwner;
    }

    private float GetCurrentPoseFrame()
    {
        int currentFrame = _runtime != null ? _runtime.CurrentFrame : -1;
        return currentFrame + 1f + (float)_frameAccumulator;
    }

    private void SubmitCurrentPose()
    {
        if (_runtime == null)
            return;

        if (_runtime.ApplyPoseBaseline(_sequenceContext))
            return;

        _runtime.RefreshPose(_sequenceContext, GetCurrentPoseFrame());
    }

    private void ReleaseSessionOwnership()
    {
        ReleaseActionAnimationOwner();
    }

    private void AcquireActionAnimationOwner()
    {
        if (_animationOwner.IsValid)
            return;

        _actorAnimation = ResolveActorAnimation();
        if (_actorAnimation != null)
            _animationOwner = _actorAnimation.BeginActionOverride();
    }

    private void ReleaseActionAnimationOwner()
    {
        if (!_animationOwner.IsValid)
            return;

        _actorAnimation?.EndActionOverride(_animationOwner);
        _animationOwner = default;
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
}
