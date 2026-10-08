using System;
using UnityEngine;

/// <summary>
/// 角色动作播放器 - 管理 Action 播放载体、速度与生命周期。
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
public class ActionPlayer : MonoBehaviour
{
    [SerializeField] private Actor _actor;

    private IActionPlaybackSession _session;
    private IFixedActionPlaybackSession _fixedTickSession;
    private bool _isFinalizingAction;
    private bool _isPaused;

    /// <summary>
    /// 当前 Action 自己的基础播放速度。临时慢速效果不应写入这里。
    /// </summary>
    private double _baseSpeed = 1.0;

    private readonly SpeedModifierStack _externalSpeedModifiers = new();

    /// <summary>
    /// 当前最终播放速度。实际播放载体会被防御性同步到这个值。
    /// </summary>
    public double PlaybackSpeed => _baseSpeed * _externalSpeedModifiers.Value;
    internal bool IsPaused => _isPaused;

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
    /// 当前动作已播放到第几帧（从 0 开始）。无当前动作或已 Stop 时归零。
    /// <para>供 <see cref="ActionStateManager"/> 判定 <see cref="CancelWindow"/> 是否命中使用。</para>
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    /// 当前动作的帧率。无当前动作时为 0。
    /// </summary>
    public int CurrentFrameRate { get; private set; }

    /// <summary>
    /// 当前动作的总帧数。供 <see cref="CancelWindow"/> 解析 <see cref="FrameAnchor.End"/> 锚点使用。
    /// 无当前动作时为 0。
    /// </summary>
    public int TotalFrames { get; private set; }

    /// <summary>当前播放 Action 的启动上下文快照，供 Loop 重播时保留。</summary>
    private ActionContext _currentContext;

    /// <summary>动作正常结束且已完成 OnExit / 清 transient / 释放播放载体后触发。Loop 重播不会触发。</summary>
    public event Action<ActionInstance> OnActionFinished;
    /// <summary>动作被播放载体异常中断时触发。显式 StopAction / 切换 Action 不触发。</summary>
    public event Action<ActionInstance> OnActionInterrupted;

    private void Awake()
    {
        if (_actor == null)
            _actor = GetComponentInParent<Actor>();
    }

    private void OnDisable()
    {
        StopActionForDisable();
        ClearExternalSpeedModifiers();
    }

    /// <summary>停止当前动作：先停止播放载体触发 Clip 清理，再执行 Action 退出与状态恢复。</summary>
    public void StopAction()
    {
        var action = CurrentAction;
        if (action == null)
        {
            _isPaused = false;
            return;
        }

        IActionPlaybackSession session = _session;
        SafeStopSession(session, ActionPlaybackStopMode.Explicit);
        FinalizeCurrentAction(action, session);
    }

    /// <summary>播放指定动作：先停止当前动作，再启动 ActionRuntime 会话。</summary>
    public void BeginAction(ActionAsset actionAsset, ActionContext context)
    {
        if (!TryBeginAction(actionAsset, context, out string failureReason))
            Debug.LogWarning(failureReason, this);
    }

    /// <summary>
    /// Starts an Action after basic input checks; runtime initialization owns its own validation and cleanup.
    /// A false result never consumes entry input and never reports a successful start.
    /// </summary>
    public bool TryBeginAction(ActionAsset actionAsset, ActionContext context, out string failureReason)
    {
        failureReason = null;
        if (!TryValidateActionAsset(actionAsset, out failureReason))
            return false;

        StopAction();
        _currentContext = context;
        _isPaused = false;

        ActionInstance action = actionAsset.CreateActionInstance();
        CurrentAction = action;

        IActionPlaybackSession session = null;
        try
        {
            action.OnEnter(_actor, _currentContext);
            session = CreateSession(action, _currentContext);
            BindSession(session);
            session.SetSpeed(PlaybackSpeed);
            session.Start();
            SyncPublicPlaybackState();
            return true;
        }
        catch (Exception exception)
        {
            failureReason = $"Action '{actionAsset.name}' failed to start: {exception.Message}";
            SafeStopSession(session, ActionPlaybackStopMode.Interrupted);
            FinalizeCurrentAction(action, session);
            return false;
        }
    }

    public void Pause()
    {
        _isPaused = true;
        _session?.Pause();
    }

    public void Resume()
    {
        _isPaused = false;
        _session?.Resume();
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
        SyncPlaybackSpeedToSession();
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
        SyncPlaybackSpeedToSession();
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
            SyncPlaybackSpeedToSession();

        return updated;
    }

    public bool RemoveExternalSpeedModifier(SpeedModifierToken token)
    {
        bool removed = _externalSpeedModifiers.Remove(token);
        if (removed)
            SyncPlaybackSpeedToSession();

        return removed;
    }

    public void ClearExternalSpeedModifiers()
    {
        if (_externalSpeedModifiers.Count == 0)
            return;

        _externalSpeedModifiers.Clear();
        SyncPlaybackSpeedToSession();
    }

    private void Update()
    {
        IActionPlaybackSession session = _session;
        if (CurrentAction == null || session == null)
            return;

        if (session is IFixedActionPlaybackSession)
            return;

        try
        {
            session.SetSpeed(PlaybackSpeed);
            session.Tick(Time.deltaTime);
            if (session == _session)
                SyncPublicPlaybackState();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            HandleSessionException(CurrentAction, session);
        }
    }

    internal bool PlayActionFrame(float deltaSeconds)
    {
        if (_fixedTickSession != null)
            return false;

        if (_session is not IFixedActionPlaybackSession fixedSession || CurrentAction == null)
            return false;

        try
        {
            fixedSession.SetSpeed(PlaybackSpeed);
            bool openedFrame = fixedSession.TryPlayFrame(deltaSeconds);
            if (ReferenceEquals(fixedSession, _session))
                SyncPublicPlaybackState();
            if (!openedFrame)
                return false;

            _fixedTickSession = fixedSession;
            return true;
        }
        catch (Exception exception)
        {
            HandleFixedSessionException(fixedSession, exception);
            return false;
        }
    }

    internal void FinishActionFrame()
    {
        IFixedActionPlaybackSession fixedSession = _fixedTickSession;
        _fixedTickSession = null;

        if (fixedSession == null || !fixedSession.HasOpenFrame)
            return;

        try
        {
            fixedSession.FinishFrame();
            if (ReferenceEquals(fixedSession, _session))
                SyncPublicPlaybackState();
        }
        catch (Exception exception)
        {
            HandleFixedSessionException(fixedSession, exception);
        }
    }

    internal void CancelAction()
    {
        IFixedActionPlaybackSession fixedSession = _fixedTickSession;
        _fixedTickSession = null;

        if (fixedSession == null)
            return;

        try
        {
            fixedSession.Cancel();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (ReferenceEquals(fixedSession, _session) && CurrentAction == fixedSession.Action)
            HandleSessionException(CurrentAction, fixedSession);
    }

    private bool TryValidateActionAsset(ActionAsset actionAsset, out string warning)
    {
        if (actionAsset == null)
        {
            warning = "Action playback failed: ActionAsset is null.";
            return false;
        }

        if (actionAsset.Timeline == null)
        {
            warning = "Action playback failed: Action is missing ActionTimelineData.";
            return false;
        }

        if (!TryValidateFixedPlaybackSpeed(out warning))
            return false;

        warning = null;
        return true;
    }

    private bool TryValidateFixedPlaybackSpeed(out string warning)
    {
        double speed = PlaybackSpeed;
        if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0d || speed > 1d)
        {
            warning = $"Fixed Action playback speed must be within [0, 1], current value is {speed}.";
            return false;
        }

        warning = null;
        return true;
    }

    private IActionPlaybackSession CreateSession(ActionInstance action, ActionContext context)
    {
        return new ActionRuntimePlaybackSession(action, _actor, context);
    }

    private void BindSession(IActionPlaybackSession session)
    {
        UnbindSession(_session);
        _session = session;
        if (_session == null)
            return;

        _session.Completed += HandleSessionCompleted;
        _session.Interrupted += HandleSessionInterrupted;
    }

    private void UnbindSession(IActionPlaybackSession session)
    {
        if (session == null)
            return;

        session.Completed -= HandleSessionCompleted;
        session.Interrupted -= HandleSessionInterrupted;
    }

    private void DisposeSession(IActionPlaybackSession session)
    {
        if (session == null)
            return;

        if (ReferenceEquals(_fixedTickSession, session))
            _fixedTickSession = null;

        UnbindSession(session);
        try
        {
            session.Dispose();
        }
        finally
        {
            if (_session == session)
                _session = null;
        }
    }

    private void HandleSessionCompleted(IActionPlaybackSession session)
    {
        if (session == null || session != _session || CurrentAction == null || _isFinalizingAction)
            return;

        ActionInstance finished = CurrentAction;

        if (finished.Config.IsLoop)
        {
            try
            {
                session.Restart();
                session.SetSpeed(PlaybackSpeed);
                SyncPublicPlaybackState();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                HandleSessionException(finished, session);
            }
            return;
        }

        FinalizeCurrentAction(finished, session);
        OnActionFinished?.Invoke(finished);
    }

    private void HandleSessionInterrupted(IActionPlaybackSession session)
    {
        if (session == null || session != _session || CurrentAction == null || _isFinalizingAction)
            return;

        ActionInstance interrupted = CurrentAction;
        FinalizeCurrentAction(interrupted, session);
        OnActionInterrupted?.Invoke(interrupted);
    }

    private void HandleSessionException(ActionInstance action, IActionPlaybackSession session)
    {
        if (action == null)
        {
            DisposeSession(session);
            ResetPublicPlaybackState();
            return;
        }

        SafeStopSession(session, ActionPlaybackStopMode.Interrupted);
        FinalizeCurrentAction(action, session);
        OnActionInterrupted?.Invoke(action);
    }

    private void HandleFixedSessionException(IFixedActionPlaybackSession session, Exception exception)
    {
        Debug.LogException(exception, this);

        if (ReferenceEquals(_fixedTickSession, session))
            _fixedTickSession = null;

        if (ReferenceEquals(session, _session) && CurrentAction == session.Action)
            HandleSessionException(CurrentAction, session);
        else
            SafeStopSession(session, ActionPlaybackStopMode.Interrupted);
    }

    private void StopActionForDisable()
    {
        var action = CurrentAction;
        if (action == null)
            return;

        IActionPlaybackSession session = _session;
        SafeStopSession(session, ActionPlaybackStopMode.Disable);
        FinalizeCurrentAction(action, session);
    }

    private void SafeStopSession(IActionPlaybackSession session, ActionPlaybackStopMode stopMode)
    {
        if (session == null)
            return;

        try
        {
            session.Stop(stopMode);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void FinalizeCurrentAction(ActionInstance action, IActionPlaybackSession session)
    {
        if (action == null || _isFinalizingAction)
            return;

        _isFinalizingAction = true;
        try
        {
            if (CurrentAction == action)
                CurrentAction = null;

            _isPaused = false;
            action.OnExit();

            _currentContext = default;
            ResetPublicPlaybackState();
        }
        finally
        {
            _isFinalizingAction = false;
            DisposeSession(session);
        }
    }

    /// <summary>
    /// 防御性同步：将最终 PlaybackSpeed 同步到当前播放载体。
    /// </summary>
    private void SyncPlaybackSpeedToSession()
    {
        IActionPlaybackSession session = _session;
        if (session == null)
            return;

        try
        {
            session.SetSpeed(PlaybackSpeed);
        }
        catch (Exception exception)
        {
            if (session is IFixedActionPlaybackSession fixedSession)
                HandleFixedSessionException(fixedSession, exception);
            else
                throw;
        }
    }

    private void SyncPublicPlaybackState()
    {
        IActionPlaybackSession session = _session;
        if (session == null || CurrentAction == null)
        {
            ResetPublicPlaybackState();
            return;
        }

        CurrentFrame = session.CurrentFrame;
        CurrentFrameRate = session.FrameRate;
        TotalFrames = session.TotalFrames;
        CurrentAction.UpdateNormalizedTime(session.NormalizedTime);
    }

    private void ResetPublicPlaybackState()
    {
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
