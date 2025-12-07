using UnityEngine;
using UnityEngine.Playables;
using System;

[RequireComponent(typeof(PlayableDirector))]
public class ActorActionDirector : MonoBehaviour
{
    private PlayableDirector _director;
    public ActionInstance CurrentAction { get; private set; }

    // 【优化】使用C#事件来通知外部系统动作已完成，而不是直接在内部处理逻辑
    public event Action<ActionInstance> OnActionFinished;

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.extrapolationMode = DirectorWrapMode.None;
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
    /// 公开的核心播放接口
    /// </summary>
    /// <param name="actionAsset">要播放的动作资产</param>
    public void Play(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning("尝试播放一个空的ActionAsset或没有Timeline的ActionAsset。", this);
            return;
        }

        // 创建新的运行时实例
        CurrentAction = actionAsset.CreateActionInstance();

        _director.playableAsset = CurrentAction.Config.TimelineAsset;
        _director.time = 0;
        _director.Play();
    }

    /// <summary>
    /// 停止当前动作
    /// </summary>
    public void Stop()
    {
        if (_director.state == PlayState.Playing)
        {
            _director.Stop();
        }
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
        if (_director.playableGraph.IsValid())
        {
            _director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }

    private void Update()
    {
        // 实时更新当前动作实例的播放进度
        if (CurrentAction != null && _director.state == PlayState.Playing)
        {
            double normalizedTime = _director.duration > 0 ? _director.time / _director.duration : 0;
            CurrentAction.UpdateRuntimeData(normalizedTime, CurrentAction.RuntimeData.phase);
        }
    }

    /// <summary>
    /// 当Director播放完毕时，这个方法会被调用
    /// </summary>
    private void HandleDirectorStopped(PlayableDirector director)
    {
        // 如果当前有动作实例，则触发事件，让状态机去决定下一步做什么
        if (CurrentAction != null)
        {
            OnActionFinished?.Invoke(CurrentAction);
        }
    }
}