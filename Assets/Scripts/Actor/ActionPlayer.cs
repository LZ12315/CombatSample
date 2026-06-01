using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 角色动作播放器 - 管理 Timeline 动画的播放、速度与生命周期。
/// 
/// <para><b>速度控制设计：</b></para>
/// <para>1. <b>_baseSpeed</b>：当前 Action 自己的基础播放速度。</para>
/// <para>2. <b>_externalSpeedModifiers</b>：SpeedVFX / HitStop / Buff 等外部临时倍率。</para>
/// <para>3. <b>PlaybackSpeed</b>：最终速度 = base speed × external modifiers。</para>
/// <para>
/// 外部临时效果不得直接覆盖基础速度，应通过 AddExternalSpeedModifier / RemoveExternalSpeedModifier
/// 申请和释放 token。这样多个 SpeedVFX 重叠时不会互相把旧快照恢复错。
/// </para>
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    [SerializeField] private Actor _actor;

    private PlayableDirector _director;

    /// <summary>
    /// 当前 Action 自己的基础播放速度。临时慢速效果不应写入这里。
    /// </summary>
    private double _baseSpeed = 1.0;

    private readonly SpeedModifierStack _externalSpeedModifiers = new();

    /// <summary>
    /// 当前最终播放速度。Director 实际速度会被防御性同步到这个值。
    /// </summary>
    public double PlaybackSpeed => _baseSpeed * _externalSpeedModifiers.Value;

    /// <summary>
    /// 当前 Action 基础速度，不含外部临时修正。
    /// </summary>
    public double BaseSpeed => _baseSpeed;

    /// <summary>
    /// 外部临时速度倍率。
    /// </summary>
    public float ExternalSpeedScale => _externalSpeedModifiers.Value;

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

    /// <summary>动作正常结束（时间走到末尾附近）且已完成 OnExit / 清 transient / 卸 Timeline 后触发。Loop 重播不会触发。</summary>
    public event Action<ActionInstance> OnActionFinished;
    /// <summary>动作被中断或切走时触发（非本组件 StopAction 先清空引用的情况）。</summary>
    public event Action<ActionInstance> OnActionInterrupted;

    /// <summary>
    /// StopAction 主动停止 Director 时，stopped 回调会同步/异步进入。
    /// 该标记用于避免回调重复执行 Action 退出流程。
    /// </summary>
    private bool _isStoppingAction;

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
        ClearExternalSpeedModifiers();
    }

    /// <summary>停止当前动作：先停 Timeline 触发 Clip 清理，再执行 Action 退出与状态恢复。</summary>
    public void StopAction()
    {
        var inst = CurrentAction;
        if (inst == null)
            return;

        _isStoppingAction = true;
        try
        {
            if (_director.playableAsset != null)
                _director.Stop();
        }
        finally
        {
            _isStoppingAction = false;
        }

        FinalizeCurrentAction(inst, clearTimeline: true);
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
        // 缓存帧率（兜底 1 防除零），并把当前帧号归零。供 ASM 判定 CancelWindow 使用。
        var ts = CurrentAction.Config.TimelineAsset;
        CurrentFrameRate = Mathf.Max(1, Mathf.RoundToInt((float)ts.editorSettings.frameRate));
        TotalFrames = Mathf.FloorToInt((float)(ts.duration * CurrentFrameRate));
        CurrentFrame = 0;
        // 不在这里清外部速度修正。SpeedVFX / HitStop 可能需要跨 Action 继续生效。
        _director.Play();
        _director.Evaluate();
        // 新 Director 默认速度为 1，立即同步最终速度。
        SyncPlaybackSpeedToDirector();
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
    /// 设置当前 Action 的基础播放速度。临时慢速效果不应调用此方法。
    /// </summary>
    public void SetSpeed(double speed)
    {
        SetBaseSpeed(speed);
    }

    /// <summary>
    /// 设置当前 Action 的基础播放速度。最终速度仍会叠加外部修正。
    /// </summary>
    public void SetBaseSpeed(double speed)
    {
        _baseSpeed = SanitizeSpeed(speed);
        SyncPlaybackSpeedToDirector();
    }

    /// <summary>
    /// 添加一个外部临时速度修正，返回的 token 必须在效果结束/中断时释放。
    /// </summary>
    public SpeedModifierToken AddExternalSpeedModifier(
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        SpeedModifierToken token = _externalSpeedModifiers.Add(scale, blendMode, debugName);
        SyncPlaybackSpeedToDirector();
        return token;
    }

    public bool UpdateExternalSpeedModifier(
        SpeedModifierToken token,
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        bool updated = _externalSpeedModifiers.Update(token, scale, blendMode, debugName);
        if (updated)
            SyncPlaybackSpeedToDirector();

        return updated;
    }

    public bool RemoveExternalSpeedModifier(SpeedModifierToken token)
    {
        bool removed = _externalSpeedModifiers.Remove(token);
        if (removed)
            SyncPlaybackSpeedToDirector();

        return removed;
    }

    public void ClearExternalSpeedModifiers()
    {
        if (_externalSpeedModifiers.Count == 0)
            return;

        _externalSpeedModifiers.Clear();
        SyncPlaybackSpeedToDirector();
    }

    /// <summary>
    /// 防御性同步：将最终 PlaybackSpeed 同步到 PlayableDirector 的实际速度。
    /// </summary>
    private void SyncPlaybackSpeedToDirector()
    {
        if (_director == null || !_director.playableGraph.IsValid())
            return;

        if (_director.playableGraph.GetRootPlayableCount() == 0)
            return;

        var rootPlayable = _director.playableGraph.GetRootPlayable(0);
        double expectedSpeed = PlaybackSpeed;
        double actualSpeed = rootPlayable.GetSpeed();

        // 如果实际速度与期望值不一致（考虑浮点误差），进行纠正。
        if (Math.Abs(actualSpeed - expectedSpeed) > 0.001)
        {
            rootPlayable.SetSpeed(expectedSpeed);
        }
    }

    private void Update()
    {
        // 防御性同步：确保 Director 实际速度与最终 PlaybackSpeed 一致。
        // 这处理了动作切换、外部干扰等边界情况。
        if (CurrentAction != null)
        {
            SyncPlaybackSpeedToDirector();
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
        if (_isStoppingAction)
            return;

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
                CurrentFrame = 0;
                _director.Play();
                SyncPlaybackSpeedToDirector();
                return;
            }

            FinalizeCurrentAction(finished, clearTimeline: true);
            // 不在动作结束时清外部速度修正。外部 Effect 持有 token，必须由它自己释放。
            OnActionFinished?.Invoke(finished);
            return;
        }

        FinalizeCurrentAction(finished, clearTimeline: false);
        OnActionInterrupted?.Invoke(finished);
    }

    private void FinalizeCurrentAction(ActionInstance action, bool clearTimeline)
    {
        if (action == null)
            return;

        if (CurrentAction == action)
            CurrentAction = null;

        action.OnExit();
        _actor?.ClearTransientTags();

        if (clearTimeline)
            _director.playableAsset = null;

        _currentContext = default;
        CurrentFrame = 0;
        CurrentFrameRate = 0;
        TotalFrames = 0;
    }

    private static double SanitizeSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed))
            return 1.0;

        return Math.Max(0.0, speed);
    }

#if UNITY_EDITOR
    public string GetExternalSpeedModifierDebugText()
    {
        return _externalSpeedModifiers.GetDebugText();
    }
#endif
}
