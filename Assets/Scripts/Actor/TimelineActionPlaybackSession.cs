using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

internal sealed class TimelineActionPlaybackSession : IActionPlaybackSession
{
    private readonly PlayableDirector _director;
    private bool _stopping;
    private bool _disposed;
    private int _currentFrame;
    private int _frameRate;
    private int _totalFrames;

    public TimelineActionPlaybackSession(ActionInstance action, PlayableDirector director)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        _director = director ?? throw new ArgumentNullException(nameof(director));
    }

    public ActionInstance Action { get; }
    public int CurrentFrame => _currentFrame;
    public int FrameRate => _frameRate;
    public int TotalFrames => _totalFrames;
    public double NormalizedTime => _director.duration > 0 ? _director.time / _director.duration : 0;
    public bool IsPlaying => !_disposed && _director.state == PlayState.Playing;

    public event Action<IActionPlaybackSession> Completed;
    public event Action<IActionPlaybackSession> Interrupted;

    public void Start()
    {
        TimelineAsset timeline = Action.Config.TimelineAsset;
        _director.stopped += HandleDirectorStopped;
        _director.playableAsset = timeline;
        _director.time = 0;
        _frameRate = Mathf.Max(1, Mathf.RoundToInt((float)timeline.editorSettings.frameRate));
        _totalFrames = Mathf.FloorToInt((float)(timeline.duration * _frameRate));
        _currentFrame = 0;
        _director.Play();
        _director.Evaluate();
    }

    public void Tick(float deltaSeconds)
    {
        if (_disposed || _director.state != PlayState.Playing)
            return;

        Action.UpdateNormalizedTime(NormalizedTime);
        _currentFrame = Mathf.FloorToInt((float)(_director.time * _frameRate));
    }

    public void Pause()
    {
        if (!_disposed && _director.state == PlayState.Playing)
            _director.Pause();
    }

    public void Resume()
    {
        if (!_disposed && _director.state == PlayState.Paused)
            _director.Resume();
    }

    public void SetSpeed(double speed)
    {
        if (_disposed || !_director.playableGraph.IsValid())
            return;

        if (_director.playableGraph.GetRootPlayableCount() == 0)
            return;

        Playable rootPlayable = _director.playableGraph.GetRootPlayable(0);
        if (Math.Abs(rootPlayable.GetSpeed() - speed) > 0.001)
            rootPlayable.SetSpeed(speed);
    }

    public void Restart()
    {
        Action.ResetRuntimeData();
        _director.time = 0;
        _currentFrame = 0;
        _director.Play();
    }

    public void Stop(ActionPlaybackStopMode stopMode)
    {
        if (_disposed)
            return;

        _stopping = true;
        try
        {
            if (_director.playableAsset != null)
                _director.Stop();
        }
        finally
        {
            _stopping = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _director.stopped -= HandleDirectorStopped;
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (_disposed || _stopping)
            return;

        if (Action.RuntimeData.normalizedTime >= 0.95f || NormalizedTime >= 0.95)
            Completed?.Invoke(this);
        else
            Interrupted?.Invoke(this);
    }
}
