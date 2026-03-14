using UnityEngine;
using UnityEngine.Playables;
using System;

[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    private PlayableDirector _director;
    public ActionInstance CurrentAction { get; private set; }

    // 使用事件来通知外部系统动作已完成或被打断
    public event Action<ActionInstance> OnActionFinished;
    public event Action<ActionInstance> OnActionInterrupted;

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.extrapolationMode = DirectorWrapMode.None;
        _director.playableAsset = null; // 确保初始时没有绑定任何 Timeline
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
        // ? 核心魔法：消除一帧延迟！
        // 强制 Timeline 瞬间计算时间轴的当前时刻（第 0 帧）。
        // 这会让你的 Tag 轨道立刻把 Block.Move 塞进黑板，绝不拖延到下一帧！
        _director.Evaluate();
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
        _director.playableAsset = null; // 清理绑定，确保下次播放时能正确触发事件
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
            CurrentAction.UpdateNormalizedTime(normalizedTime);
        }
    }

    /// <summary>
    /// 当Director播放完毕或停止时调用
    /// </summary>
    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (CurrentAction != null)
        {
            // 先把当前动作存下来，防止后续逻辑中 CurrentAction 被覆盖
            ActionInstance actionToNotify = CurrentAction;
            CurrentAction = null;

            // ? 核心分流：利用你已有的 NormalizedTime (给 0.05 的浮点误差容限)
            // 假设你的 ActionInstance 中获取该值的属性叫 NormalizedTime
            if (actionToNotify.RuntimeData.normalizedTime >= 0.95f) 
            {
                OnActionFinished?.Invoke(actionToNotify);
            }
            else
            {
                OnActionInterrupted?.Invoke(actionToNotify);
            }
        }
    }
}