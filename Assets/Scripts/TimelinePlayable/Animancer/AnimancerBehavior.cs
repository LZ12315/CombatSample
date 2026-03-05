using UnityEngine;
using UnityEngine.Playables;
using Animancer;

/// <summary>
/// Timeline 的运行时行为逻辑。
/// 负责在 Timeline 播放期间控制 Animancer 播放对应的动画。
/// 支持编辑器预览和运行时方向动画切换。
/// </summary>
public class AnimancerBehaviour : ActionBehaviourBase
{
    // --- 配置数据 ---
    public TransitionAsset transitionAsset;

    // --- 运行时状态 ---
    private AnimancerState _currentState;
    private DirectionalAnimationHandler _directionalHandler;

    #region 基类方法重写 (生命周期实现)

    /// <summary>
    /// 开始播放 (对应之前的 OnEnter)
    /// 基类已确保 actor 不为空
    /// </summary>
    protected override void OnClipStart(Playable playable)
    {
        if (!IsValidTransition()) return;

        ITransition aTransition = transitionAsset.Transition;
        bool isEditorPreview = !Application.isPlaying;

        // 1. 处理方向动画逻辑
        if (isEditorPreview && aTransition is DirectionalClipTransition directional)
        {
            // 编辑器预览：默认向前
            directional.SetDirection(0);
            _currentState = actor.animancer.Play(aTransition, 0.15f);
        }
        else if (!isEditorPreview && aTransition is DirectionalClipTransition dirTransition)
        {
            // 运行时：创建 Handler 并初始化播放
            _directionalHandler = new DirectionalAnimationHandler(actor, dirTransition);
            _currentState = _directionalHandler.Initialize();
        }
        else
        {
            // 2. 普通动画逻辑
            _currentState = actor.animancer.Play(aTransition, 0.15f);
        }

        // 3. 同步时间 (防止从中间开始播放时的跳变)
        if (_currentState != null)
        {
            _currentState.IsPlaying = true;
            _currentState.Time = (float)playable.GetTime();
        }
    }

    /// <summary>
    /// 每帧更新 (对应之前的 OnUpdate)
    /// </summary>
    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // --- 编辑器预览 ---
        if (!Application.isPlaying)
        {
            if (actor != null && actor.animancer != null && _currentState != null)
            {
                _currentState.Speed = 0;
                _currentState.Time = (float)playable.GetTime();
                actor.animancer.Evaluate();
            }
            return;
        }

        // --- 运行时 ---
        if (_currentState == null) return;

        // 1. 同步速度
        _currentState.Speed = info.effectiveSpeed;

        // 2. 更新方向
        if (_directionalHandler != null)
        {
            AnimancerState newState = _directionalHandler.Update();
            if (newState != null)
            {
                _currentState = newState;
                _currentState.Speed = info.effectiveSpeed;
            }
        }
    }

    /// <summary>
    /// 暂停 (Timeline 暂停但未退出)
    /// </summary>
    protected override void OnClipPause()
    {
        if (_currentState != null)
        {
            _currentState.IsPlaying = false;
        }
    }

    /// <summary>
    /// 结束 (正常播放完毕 或 被打断)
    /// </summary>
    protected override void OnClipStop(bool isNormal)
    {
        // 无论是正常结束还是被打断，我们都不再手动调用 Stop()
        // 而是交由 Animancer 的混合机制处理，避免权重警告和 T-Pose
        if (_currentState != null)
        {
            _currentState.IsPlaying = false;
        }
    }

    /// <summary>
    /// 清理引用
    /// </summary>
    protected override void CleanUp()
    {
        _currentState = null;
        _directionalHandler = null;
        // actor 引用由基类管理，无需在此清理
    }

    #endregion

    #region 数据验证

    private bool IsValidTransition()
    {
        if (transitionAsset == null || transitionAsset.Transition == null) return false;

        if (transitionAsset.Transition is ClipTransition c && c.Clip == null) return false;

        // 使用 AnimationSet 属性避免编译歧义
        if (transitionAsset.Transition is DirectionalClipTransition d)
        {
            if (d.AnimationSet == null) return false;
            if (d.AnimationSet.GetClip(0) == null) return false;
        }

        return true;
    }

    #endregion
}