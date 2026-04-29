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





    /// <summary>
    /// 当前动作已播放到第几帧（从 0 开始），每帧 Update 中由 <c>Floor(_director.time * CurrentFrameRate)</c> 计算。
    /// <para>HitStop 冻结 <c>_director.time</c> 时本值自然停滞，语义天然正确。</para>
    /// <para>无当前动作或已 Stop 时归零。</para>
    /// <para>供 <see cref="ActionStateManager"/> 判定 <see cref="CancelWindow"/> 是否命中使用。</para>
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    /// 当前动作的帧率（由其 TimelineAsset.editorSettings.frameRate 在 Bind 时一次性读入并缓存）。
    /// 使用 <c>Mathf.Max(1, Round(frameRate))</c> 兜底防除零。无当前动作时为 0。
    /// </summary>
    public int CurrentFrameRate { get; private set; }

    /// <summary>
    /// 当前动作的总帧数（= Floor(duration * frameRate)）。
    /// 供 <see cref="CancelWindow"/> 解析 <see cref="FrameAnchor.End"/> 锚点使用。
    /// 无当前动作时为 0。
    /// </summary>
    public int TotalFrames { get; private set; }

    /// <summary>当前播放 Action 的启动上下文快照，供 Loop 重播时保留。</summary>
    private ActionEventContext _currentContext;

    /// <summary>显式 Stop/切招：避免仅用 normalizedTime 把 Stop 误判为 Loop 自然循环末尾。</summary>
    private bool _isStoppingCurrentAction;

    private bool _isSwitchingAction;

    /// <summary>切招：旧 Action 退出后再绑定的新动作（避免 Stop 回调与 Bind 交错）。</summary>
    private ActionAsset _pendingActionAsset;

    private ActionEventContext _pendingContext;
    private bool _hasPendingAction;

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

    /// <summary>
    /// 停止当前动作：先 Stop Director，让 Timeline Clip 在 ActionInstance.Actor 仍有效时完成 OnClipStop；
    /// 再由 HandleDirectorStopped / 同步路径执行 OnExit、清 transient、卸 Timeline。
    /// </summary>
    public void StopAction()
    {
        ClearPendingAction();

        if (CurrentAction == null)
            return;

        // Graph 有效时优先 Stop，让 Timeline Clip 走 OnClipStop（Playing / Paused 均可）。
        if (_director.playableGraph.IsValid())
        {
            StopCurrentDirectorForExit(switching: false);
            return;
        }

        // 未在播放（极少见）：stopped 不会触发，手动退出（与旧版 StopAction 一致：不触发 Finished/Interrupted 事件）。
        ExitCurrentActionSynchronously();
    }

    /// <summary>播放指定动作：若有当前动作则排队 pending，先 Stop 旧 Director；stopped 回调完成 OnExit 后再绑定新 Timeline。</summary>
    public void BeginAction(ActionAsset actionAsset, ActionEventContext context = default)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning("播放失败：ActionAsset 或 Timeline 为空。请在 ActionAsset 上指定 Timeline。", this);
            return;
        }

        if (CurrentAction != null)
        {
            _pendingActionAsset = actionAsset;
            _pendingContext = context;
            _hasPendingAction = true;

            if (!_isStoppingCurrentAction)
                ExitCurrentActionForSwitch();
            return;
        }

        _currentContext = context;
        TryBindAndPlayTimeline(actionAsset);
    }

    private void ClearPendingAction()
    {
        _hasPendingAction = false;
        _pendingActionAsset = null;
        _pendingContext = default;
    }

    /// <summary>旧 Action OnExit 完成后启动排队中的新动作。</summary>
    private void TryPlayPendingActionAfterExit()
    {
        if (!_hasPendingAction || _pendingActionAsset == null)
        {
            ClearPendingAction();
            return;
        }

        var asset = _pendingActionAsset;
        var ctx = _pendingContext;
        ClearPendingAction();

        _currentContext = ctx;
        TryBindAndPlayTimeline(asset);
    }

    private void ExitCurrentActionForSwitch()
    {
        StopCurrentDirectorForExit(switching: true);
    }

    private void StopCurrentDirectorForExit(bool switching)
    {
        if (CurrentAction == null)
            return;

        _isStoppingCurrentAction = true;
        _isSwitchingAction = switching;

        if (_director.playableGraph.IsValid())
        {
            _director.Stop();
            return;
        }

        // Graph 已无效：无法收到 stopped，直接同步完成退出。
        bool treatAsInterrupted = ComputeExitShouldInterrupt(switching, CurrentAction);
        ExitCurrentActionAfterClips(treatAsInterrupted);
        _isStoppingCurrentAction = false;
        _isSwitchingAction = false;
        if (switching)
            TryPlayPendingActionAfterExit();
    }

    private static bool ComputeExitShouldInterrupt(bool switching, ActionInstance finished)
    {
        if (switching)
            return true;
        if (finished == null)
            return true;
        return !(finished.RuntimeData.normalizedTime >= 0.95f && !finished.Config.IsLoop);
    }

    private void ExitCurrentActionAfterClips(bool treatAsInterrupted)
    {
        if (CurrentAction == null)
            return;

        var finished = CurrentAction;
        CompleteActionExit(finished, treatAsInterrupted);
    }

    private void CompleteActionExit(ActionInstance finished, bool treatAsInterrupted)
    {
        finished.OnExit();
        _actor?.ClearTransientTags();
        CurrentAction = null;
        if (_director.playableAsset != null)
            _director.playableAsset = null;
        CurrentFrame = 0;
        CurrentFrameRate = 0;
        TotalFrames = 0;
        _currentContext = default;

        if (treatAsInterrupted)
            OnActionInterrupted?.Invoke(finished);
        else
            OnActionFinished?.Invoke(finished);
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
        // 缓存帧率（兜底 1 防除零），并把当前帧号归零。供 ASM 判定 CancelWindow 使用。
        var ts = CurrentAction.Config.TimelineAsset;
        CurrentFrameRate = Mathf.Max(1, Mathf.RoundToInt((float)ts.editorSettings.frameRate));
        TotalFrames = Mathf.FloorToInt((float)(ts.duration * CurrentFrameRate));
        CurrentFrame = 0;
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

            // 计算当前帧号（HitStop 时 _director.time 冻结 → 帧号自动冻结，符合预期）
            CurrentFrame = Mathf.FloorToInt((float)(_director.time * CurrentFrameRate));
        }


    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (CurrentAction == null)
            return;

        ActionInstance finished = CurrentAction;

        bool forceExit = _isStoppingCurrentAction;
        bool switching = _isSwitchingAction;
        _isStoppingCurrentAction = false;
        _isSwitchingAction = false;

        if (forceExit)
        {
            bool treatAsInterrupted = ComputeExitShouldInterrupt(switching, finished);
            CompleteActionExit(finished, treatAsInterrupted);
            TryPlayPendingActionAfterExit();
            return;
        }

        if (finished.RuntimeData.normalizedTime >= 0.95f)
        {
            if (finished.Config.IsLoop)
            {
                // Loop 重播：直接让 Director 从头播放，不打断 ActionInstance 和动画状态。
                // 不执行 OnExit/OnEnter，避免 Movement 状态闪烁和动画重启。
                finished.ResetRuntimeData();
                _director.time = 0;
                CurrentFrame = 0;
                _director.Play();
                return;
            }

            CompleteActionExit(finished, treatAsInterrupted: false);
            return;
        }

        // 中断：必须 OnExit，否则 MotionConfig / Clip 状态会泄漏。
        CompleteActionExit(finished, treatAsInterrupted: true);
    }

    /// <summary>同步退出（Director 未在播放时 StopAction 兜底）。不派发 Finished/Interrupted，与旧行为一致。</summary>
    private void ExitCurrentActionSynchronously()
    {
        var inst = CurrentAction;
        if (inst == null)
            return;

        inst.OnExit();
        _actor?.ClearTransientTags();
        CurrentAction = null;
        if (_director.playableAsset != null)
            _director.playableAsset = null;
        CurrentFrame = 0;
        CurrentFrameRate = 0;
        TotalFrames = 0;
        _currentContext = default;
    }
}
