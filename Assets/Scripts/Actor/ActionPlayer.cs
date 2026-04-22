using UnityEngine;
using UnityEngine.Playables;
using System;

/// <summary>
/// 角色动作播放器 - 管理 Timeline 动画的播放、速度与生命周期。
/// 
/// <para><b>速度控制设计（混合方案）：</b></para>
    /// <para>1. <b>_expectedSpeed（权威期望值）</b>：外部系统（如 <see cref="ActionSpeedEffect"/>）通过 <see cref="SetSpeed(double)"/> 设置的期望播放速度。</para>
/// <para>2. <b>防御性同步</b>：每帧检查 Director 的实际速度是否与期望值一致，若不一致则自动纠正。</para>
/// <para>3. <b>自动继承</b>：切换新动作时，新 Director 会自动以 _expectedSpeed 启动，无需外部重新设置。</para>
/// <para>
/// 这种设计的优势：
/// - Effect 只需设置一次速度，无需每帧更新
/// - 动作切换时无缝继承速度（如 HitStop 期间切换受击动画）
/// - 防御性同步防止外部系统意外修改速度导致不一致
/// </para>
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    [SerializeField] private Actor _actor;

    private PlayableDirector _director;
    
    /// <summary>
    /// 期望播放速度（权威值）。外部系统通过 SetSpeed() 修改，Director 实际速度会跟随此值。
    /// 默认 1.0（正常速度），0.0-1.0 用于 HitStop/HitStick 效果。
    /// </summary>
    private double _expectedSpeed = 1.0;
    
    /// <summary>
    /// 公开读取当前期望速度。注意：Director 实际速度可能短暂不一致（切换动作瞬间），
    /// 但会在下一帧 Update 中被纠正。
    /// </summary>
    public double PlaybackSpeed => _expectedSpeed;

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
        // 注意：不在这里重置 _expectedSpeed。
        // 速度控制现在由外部 Effect（如 ActionSpeedEffect）全权管理。
        // StopAction 只是停止当前动作，不应干预速度状态。
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
        // 注意：不在这里重置 _expectedSpeed。
        // 新 Director 启动后默认速度为 1.0，但会在 Update 中被立即同步为 _expectedSpeed。
        // 这确保 HitStop 期间切换动作能无缝继承当前速度。
        _director.Play();
        _director.Evaluate();
        // 立即同步一次期望速度到新 Director（防御性）
        SyncExpectedSpeedToDirector();
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

    /// <summary>
    /// 设置期望播放速度。外部系统（如 <see cref="ActionSpeedEffect"/>）调用此方法来控制动作速度。
    /// 速度会立即应用到当前 Director，并在后续每帧通过防御性同步保持一致。
    /// </summary>
    /// <param name="speed">期望速度，0.0=完全冻结，1.0=正常速度</param>
    public void SetSpeed(double speed)
    {
        _expectedSpeed = speed;
        // 立即同步到当前 Director
        SyncExpectedSpeedToDirector();
    }

    /// <summary>
    /// 防御性同步：将 _expectedSpeed 同步到 PlayableDirector 的实际速度。
    /// 此方法是核心机制，用于处理：
    /// 1. 动作切换时新 Director 自动继承当前期望速度
    /// 2. 外部系统意外修改 Director 速度后的自动纠正
    /// 3. 确保 Effect 设置一次速度后，整个持续期间速度恒定
    /// </summary>
    private void SyncExpectedSpeedToDirector()
    {
        if (!_director.playableGraph.IsValid()) return;

        var rootPlayable = _director.playableGraph.GetRootPlayable(0);
        double actualSpeed = rootPlayable.GetSpeed();
        
        // 如果实际速度与期望值不一致（考虑浮点误差），进行纠正
        if (Math.Abs(actualSpeed - _expectedSpeed) > 0.001)
        {
            rootPlayable.SetSpeed(_expectedSpeed);
        }
    }

    private void Update()
    {
        // 防御性同步：确保 Director 实际速度与期望值一致
        // 这处理了动作切换、外部干扰等边界情况
        if (CurrentAction != null)
        {
            SyncExpectedSpeedToDirector();
        }

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
            // 注意：不在这里重置 _expectedSpeed。
            // 速度控制现在由外部 Effect（如 ActionSpeedEffect）全权管理。
            // 动作结束时不自动恢复速度，确保 HitStop 等效果能持续到 Effect 主动恢复。
            OnActionFinished?.Invoke(finished);
            return;
        }

        CurrentAction = null;
        OnActionInterrupted?.Invoke(finished);
    }
}
