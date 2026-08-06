using System;
using UnityEngine;

internal sealed class SequenceActionPlaybackSession : IActionPlaybackSession
{
    private readonly Actor _actor;
    private readonly ActionEventContext _eventContext;
    private readonly ActionSequenceContext _sequenceContext = new ActionSequenceContext();
    private ActionSequenceRuntime _runtime;
    private bool _paused;
    private bool _completionPending;
    private bool _disposed;
    private double _speed = 1.0;

    public SequenceActionPlaybackSession(ActionInstance action, Actor actor, ActionEventContext eventContext)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        _actor = actor;
        _eventContext = eventContext;
    }

    public ActionInstance Action { get; }
    public int CurrentFrame => _runtime != null ? Mathf.Max(0, _runtime.CurrentFrame) : 0;
    public int FrameRate => _runtime != null ? _runtime.FrameRate : 0;
    public int TotalFrames => _runtime != null ? _runtime.DurationFrames : 0;
    public double NormalizedTime => _runtime != null ? _runtime.NormalizedTime : 0;
    public bool IsPlaying => !_disposed && _runtime != null && (_runtime.IsPlaying || _completionPending);
    public ActionSequenceRuntimeDiagnostics Diagnostics => _runtime?.Diagnostics;

    public event Action<IActionPlaybackSession> Completed;
    public event Action<IActionPlaybackSession> Interrupted;

    public void Start()
    {
        CreateRuntimeAndContext();
        StepFrameZero();
    }

    public void Tick(float deltaSeconds)
    {
        if (_disposed || _runtime == null)
            return;

        if (_completionPending)
        {
            CompletePending();
            return;
        }

        if (_paused)
            return;

        _sequenceContext.Actor = _actor;
        _runtime.Tick(_sequenceContext, deltaSeconds, (float)_speed);
        Action.UpdateNormalizedTime(NormalizedTime);

        if (_runtime.IsComplete)
            _completionPending = true;
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

        _speed = Math.Max(0.0, speed);
    }

    public void Restart()
    {
        Action.ResetRuntimeData();
        _completionPending = false;
        _paused = false;
        CreateRuntimeAndContext();
        StepFrameZero();
    }

    public void Stop(ActionPlaybackStopMode stopMode)
    {
        if (_runtime == null)
            return;

        if (!_runtime.IsComplete)
            _runtime.Cancel(_sequenceContext);

        _completionPending = false;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void CreateRuntimeAndContext()
    {
        _runtime = new ActionSequenceRuntime(Action.Config.SequenceData);
        _sequenceContext.Actor = _actor;
        _sequenceContext.EventContext = _eventContext;
    }

    private void StepFrameZero()
    {
        _runtime.StepFrame(_sequenceContext);
        Action.UpdateNormalizedTime(NormalizedTime);
        if (_runtime.IsComplete)
            _completionPending = true;
    }

    private void CompletePending()
    {
        if (!_completionPending)
            return;

        _completionPending = false;
        Completed?.Invoke(this);
    }
}
