using UnityEngine;
using UnityEngine.Playables;
using System;

[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    private PlayableDirector _director;
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed => _playbackSpeed;

    public ActionInstance CurrentAction { get; private set; }

    /// <summary>动作正常结束（时间走到末尾附近）时触发。</summary>
    public event Action<ActionInstance> OnActionFinished;
    /// <summary>动作被中断或切走时触发。</summary>
    public event Action<ActionInstance> OnActionInterrupted;

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.extrapolationMode = DirectorWrapMode.None;
        _director.playableAsset = null; // 避免编辑器等场景下残留旧 Timeline
    }

    private void OnEnable()
    {
        _director.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        _director.stopped -= HandleDirectorStopped;
    }

    /// <summary>
    /// 播放指定动作：绑定 Timeline、归零时间并 Evaluate 一帧，保证首帧 Track/Tag 生效。
    /// </summary>
    /// <param name="actionAsset">动作资产（需已配置 Timeline）。</param>
    public void Play(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning("Play 失败：ActionAsset 或 Timeline 为空。请在 ActionAsset 上指定 Timeline。", this);
            return;
        }

        CurrentAction = actionAsset.CreateActionInstance();
        _director.playableAsset = CurrentAction.Config.TimelineAsset;
        _director.time = 0;
        _playbackSpeed = 1.0;
        _director.Play();
        // 首帧 Evaluate：避免第一帧仍为「空图」导致 Tag/Block 未就绪。
        // 部分情况下 Director 首帧 time=0 时尚未建好图，显式 Evaluate 可同步状态。
        // 与 Timeline 上 Tag、Block.Move 等轨道的首帧采样一致。
        _director.Evaluate();
    }

    /// <summary>停止播放并清空当前动作引用。</summary>
    public void Stop()
    {
        if (_director.state == PlayState.Playing)
        {
            _director.Stop();
        }
        _director.playableAsset = null;
        _playbackSpeed = 1.0;
        CurrentAction = null;
    }

    public void Pause()
    {
        if (_director.state == PlayState.Playing)
        {
            _director.Pause();
        }
    }

    public void Resume()
    {
        if (_director.state == PlayState.Paused)
        {
            _director.Resume();
        }
    }

    public void SetSpeed(double speed)
    {
        _playbackSpeed = speed;
        if (_director.playableGraph.IsValid())
        {
            _director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }

    private void Update()
    {
        // 播放中把 Director 归一化时间同步到 ActionInstance（供条件等读取）。
        if (CurrentAction != null && _director.state == PlayState.Playing)
        {
            double normalizedTime = _director.duration > 0 ? _director.time / _director.duration : 0;
            CurrentAction.UpdateNormalizedTime(normalizedTime);
        }
    }

    /// <summary>
    /// Director 停止时区分「自然播完」与「被中断」，再派发事件。
    /// </summary>
    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (CurrentAction != null)
        {
            ActionInstance actionToNotify = CurrentAction;

            // 归一化时间接近 1 视为自然结束；阈值略小于 1，避免浮点与最后一帧差异。
            // 与 ActionInstance 内缓存的 NormalizedTime 一致。
            if (actionToNotify.RuntimeData.normalizedTime >= 0.95f)
            {
                // Natural finish: subscriber StopCurrentAction runs OnExit (SelfTag, etc.).
                OnActionFinished?.Invoke(actionToNotify);
                if (CurrentAction == actionToNotify)
                    CurrentAction = null;
            }
            else
            {
                CurrentAction = null;
                OnActionInterrupted?.Invoke(actionToNotify);
            }
        }
    }
}
