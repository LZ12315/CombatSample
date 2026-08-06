using System;

internal enum ActionPlaybackStopMode
{
    Explicit,
    Disable,
    Completed,
    Interrupted,
}

internal interface IActionPlaybackSession : IDisposable
{
    ActionInstance Action { get; }
    int CurrentFrame { get; }
    int FrameRate { get; }
    int TotalFrames { get; }
    double NormalizedTime { get; }
    bool IsPlaying { get; }

    event Action<IActionPlaybackSession> Completed;
    event Action<IActionPlaybackSession> Interrupted;

    void Start();
    void Tick(float deltaSeconds);
    void Pause();
    void Resume();
    void SetSpeed(double speed);
    void Restart();
    void Stop(ActionPlaybackStopMode stopMode);
}
