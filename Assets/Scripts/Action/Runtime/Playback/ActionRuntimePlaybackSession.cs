using System;
using UnityEngine;

/// <summary>
/// Adapts one-at-a-time ActionRuntime executions to the existing fixed simulation pipeline.
/// ActionRuntime remains the sole owner of ActionRuntime tags, animation and gameplay-item resources.
/// </summary>
internal sealed class ActionRuntimePlaybackSession : IFixedActionPlaybackSession
{
    private readonly Actor _actor;
    private readonly ActionContext _context;
    private ActionRuntime _runtime;
    private bool _paused;
    private bool _disposed;
    private bool _pendingRestart;
    private double _speed = 1.0;

    public ActionRuntimePlaybackSession(ActionInstance action, Actor actor, ActionContext context)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        _actor = actor;
        _context = context;
    }

    public ActionInstance Action { get; }
    public int CurrentFrame => _pendingRestart
        ? 0
        : _runtime != null ? Mathf.Max(0, _runtime.CurrentFrame) : 0;
    public int FrameRate => ActionTimelineData.FrameRate;
    public int TotalFrames => Action.Config.Timeline != null
        ? Action.Config.Timeline.DurationFrames
        : 0;
    public double NormalizedTime
    {
        get
        {
            if (_pendingRestart || _runtime == null)
                return 0d;
            if (_runtime.State == ActionRuntimeState.Completed)
                return 1d;

            return TotalFrames > 0
                ? Math.Min(1d, Math.Max(0d, _runtime.ContinuousPosition / TotalFrames))
                : 0d;
        }
    }
    public bool IsPlaying => !_disposed
        && (_pendingRestart || (_runtime != null && _runtime.IsPlaying));
    public bool HasOpenFrame => !_disposed && _runtime != null && _runtime.HasOpenFrame;

    public event Action<IActionPlaybackSession> Completed;
    public event Action<IActionPlaybackSession> Interrupted
    {
        add { }
        remove { }
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_runtime != null || _pendingRestart)
            throw new InvalidOperationException("ActionRuntime Action playback session has already started.");

        CreateAndBeginRuntime();
    }

    public void Tick(float deltaSeconds)
    {
        throw new InvalidOperationException(
            "ActionRuntime Action playback is fixed-simulation owned and cannot be advanced from Update.");
    }

    public bool TryPlayFrame(float deltaSeconds)
    {
        if (_disposed)
            return false;

        // Begin opens frame zero. It must pass through the current tick's world/hit/finish phases
        // before any attempt to advance continuous action time.
        if (HasOpenFrame)
            return true;

        if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            return false;

        if (_paused || _speed <= 0d || deltaSeconds <= 0f)
        {
            _runtime?.RefreshCurrentAnimationPose();
            return false;
        }

        if (_pendingRestart)
        {
            _pendingRestart = false;
            CreateAndBeginRuntime();
            return HasOpenFrame;
        }

        if (_runtime == null || !_runtime.IsPlaying)
            return false;

        _runtime.Advance();
        return HasOpenFrame;
    }

    public void FinishFrame()
    {
        if (!HasOpenFrame)
            return;

        bool completed = _runtime.Finish();
        Action.UpdateNormalizedTime(completed ? 1d : NormalizedTime);
        if (completed)
            Completed?.Invoke(this);
    }

    public void Cancel()
    {
        _pendingRestart = false;
        AbortRuntime();
    }

    public void Pause()
    {
        _paused = true;
        _runtime?.Pause();
    }

    public void Resume()
    {
        _paused = false;
        _runtime?.Resume();
    }

    public void SetSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0d || speed > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "ActionRuntime speed must be within [0, 1].");
        }

        _speed = speed;
        _runtime?.SetSpeed((float)speed);
    }

    public void Restart()
    {
        ThrowIfDisposed();
        StopRuntimeNormallyOrAbort();
        _runtime = null;
        _pendingRestart = true;
        Action.ResetRuntimeData();
    }

    public void Stop(ActionPlaybackStopMode stopMode)
    {
        if (_disposed)
            return;

        _pendingRestart = false;
        StopRuntimeNormallyOrAbort();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pendingRestart = false;
        StopRuntimeNormallyOrAbort();
        Completed = null;
    }

    private void CreateAndBeginRuntime()
    {
        var runtime = new ActionRuntime(Action.Config, _actor, _context);
        _runtime = runtime;
        try
        {
            runtime.SetSpeed((float)_speed);
            if (_paused)
                runtime.Pause();
            runtime.Begin();
            if (_paused)
                runtime.Pause();
        }
        catch
        {
            runtime.Abort();
            throw;
        }
    }

    private void StopRuntimeNormallyOrAbort()
    {
        ActionRuntime runtime = _runtime;
        if (runtime == null || !runtime.IsPlaying)
            return;

        if (runtime.HasOpenFrame || !runtime.Interrupt())
            runtime.Abort();
    }

    private void AbortRuntime()
    {
        if (_runtime != null && _runtime.IsPlaying)
            _runtime.Abort();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ActionRuntimePlaybackSession));
    }
}
