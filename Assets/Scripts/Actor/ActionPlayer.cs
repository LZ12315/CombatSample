using UnityEngine;
using UnityEngine.Playables;
using System;

[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    [SerializeField] private Actor _actor;

    private PlayableDirector _director;
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed => _playbackSpeed;

    public ActionInstance CurrentAction { get; private set; }

    /// <summary>当前播放 Action 的启动上下文快照，供 Loop 重播时保留。</summary>
    private ActionEventContext _currentContext;

    /// <summary>动作正常结束（时间走到末尾附近）且已完成 OnExit / 清 transient / 卸 Timeline 后触发。Loop 重播不会触发。</summary>
    public event Action<ActionInstance> OnActionFinished;
    /// <summary>动作被中断或切走时触发（非本组件 StopAction 先清空引用的情况）。</summary>
    public event Action<ActionInstance> OnActionInterrupted;

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.extrapolationMode = DirectorWrapMode.None;
        _director.playableAsset = null;
        if (_actor == null)
            _actor = GetComponentInParent<Actor>();
    }

    private void OnEnable()
    {
        _director.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        _director.stopped -= HandleDirectorStopped;
    }

    /// <summary>停止当前动作：OnExit、清空 transient、卸 Timeline。先于 Director.Stop 置空 CurrentAction，避免 stopped 回调重复处理。</summary>
    public void StopAction()
    {
        var inst = CurrentAction;
        if (inst == null)
            return;

        CurrentAction = null;
        inst.OnExit();
        _actor?.ClearTransientTags();

        if (_director.state == PlayState.Playing)
            _director.Stop();

        _director.playableAsset = null;
        _playbackSpeed = 1.0;
        _currentContext = default;
    }

    /// <summary>播放指定动作：先 StopAction，再绑定 Timeline、OnEnter。</summary>
    public void BeginAction(ActionAsset actionAsset, ActionEventContext context = default)
    {
        StopAction();
        _currentContext = context;
        TryBindAndPlayTimeline(actionAsset);
    }

    private bool TryBindAndPlayTimeline(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning("播放失败：ActionAsset 或 Timeline 为空。请在 ActionAsset 上指定 Timeline。", this);
            return false;
        }

        CurrentAction = actionAsset.CreateActionInstance();
        // 必须在 Play/Evaluate 之前调用 OnEnter，
        // 因为 Evaluate 会立即触发 OnClipStart，此时 Timeline Clip 需要访问 Actor。
        CurrentAction.OnEnter(_actor, _currentContext);
        _director.playableAsset = CurrentAction.Config.TimelineAsset;
        _director.time = 0;
        _playbackSpeed = 1.0;
        _director.Play();
        _director.Evaluate();
        return true;
    }

    public void Pause()
    {
        if (_director.state == PlayState.Playing)
            _director.Pause();
    }

    public void Resume()
    {
        if (_director.state == PlayState.Paused)
            _director.Resume();
    }

    public void SetSpeed(double speed)
    {
        _playbackSpeed = speed;
        if (_director.playableGraph.IsValid())
            _director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
    }

    private void Update()
    {
        if (CurrentAction != null && _director.state == PlayState.Playing)
        {
            double normalizedTime = _director.duration > 0 ? _director.time / _director.duration : 0;
            CurrentAction.UpdateNormalizedTime(normalizedTime);
        }
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (CurrentAction == null)
            return;

        ActionInstance finished = CurrentAction;

        if (finished.RuntimeData.normalizedTime >= 0.95f)
        {
            if (finished.Config.IsLoop)
            {
                // Loop 重播：直接让 Director 从头播放，不打断 ActionInstance 和动画状态。
                // 不执行 OnExit/OnEnter，避免 Movement 状态闪烁和动画重启。
                finished.ResetRuntimeData();
                _director.time = 0;
                _director.Play();
                return;
            }

            finished.OnExit();
            _actor?.ClearTransientTags();
            CurrentAction = null;
            _director.playableAsset = null;
            _playbackSpeed = 1.0;
            OnActionFinished?.Invoke(finished);
            return;
        }

        CurrentAction = null;
        OnActionInterrupted?.Invoke(finished);
    }
}
