using UnityEngine;
using UnityEngine.Playables;
using Animancer;

/// <summary>
/// Timeline 的运行时行为逻辑。
/// 继承自你编写的 ActionBehaviourBase。
/// 支持普通单体动画，以及带方向的动作（如八向闪避），且遵循“动作承诺”原则。
/// </summary>
public class AnimancerBehaviour : ActionBehaviourBase
{
    // --- 配置数据 ---
    [Tooltip("Drag a ClipTransition (attacks) or MixerTransition2D (8-way dodge).")]
    public TransitionAsset transitionAsset;

    // --- 运行时状态 ---
    private AnimancerState _currentState;

    #region 基类方法实现

    /// <summary>
    /// 开始播放 (对应基类的 OnClipStart)
    /// </summary>
    protected override void OnClipStart(Playable playable)
    {
        if (transitionAsset == null || transitionAsset.Transition == null) return;
        if (actor == null || actor.animancer == null) return;

        ITransition transition = transitionAsset.Transition;

        // 1. 无论什么类型，先播放！这会返回真正运行时的 AnimancerState
        _currentState = actor.animancer.Play(transition);

        // ==========================================
        // ? 亮点：如果运行时状态是一个 2D 混合树 (如八向闪避)
        // ==========================================
        if (_currentState is MixerState<Vector2> mixerState)
        {
            // 仅仅在动作开始的第一帧，获取一次玩家的摇杆输入
            Vector2 dodgeInput = actor.logicInput != null ? actor.logicInput.MoveInput : Vector2.zero;
            
            // 动作承诺：哪怕玩家摇杆没推到底，也按极限方向翻滚
            if (dodgeInput.sqrMagnitude > 0.01f)
            {
                mixerState.Parameter = dodgeInput.normalized; 
            }
            else
            {
                // 默认向后闪避 (0, -1)
                mixerState.Parameter = new Vector2(0, -1f); 
            }
        }
        // ==========================================

        // 同步时间 (防止跳帧)
        if (_currentState != null)
        {
            _currentState.IsPlaying = true;
            _currentState.Time = (float)playable.GetTime();
        }
    }

    /// <summary>
    /// 每帧更新 (对应基类的 OnClipUpdate)
    /// </summary>
    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // --- 编辑器预览模式 ---
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

        // --- 运行时模式 ---
        if (_currentState == null) return;

        // 仅仅同步 Timeline 速度给 Animancer，【绝对不要】在这里更新 mixer2D.Parameter！
        // 因为动作游戏里，招式一旦出手，就不受摇杆控制了。
        _currentState.Speed = info.effectiveSpeed;
    }

    /// <summary>
    /// 暂停
    /// </summary>
    protected override void OnClipPause()
    {
        if (_currentState != null)
        {
            _currentState.IsPlaying = false;
        }
    }

    /// <summary>
    /// 恢复
    /// </summary>
    protected override void OnClipResume(Playable playable)
    {
        if (_currentState != null)
        {
            _currentState.IsPlaying = true;
        }
    }

    /// <summary>
    /// 结束 (正常播完 或 被打断)
    /// </summary>
    protected override void OnClipStop(bool isFinish)
    {
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
    }

    #endregion
}