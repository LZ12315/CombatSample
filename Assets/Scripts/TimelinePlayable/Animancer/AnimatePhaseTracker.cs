using UnityEngine;
using Animancer;

/// <summary>
/// 一个纯C#对象，用于追踪Animancer当前主状态的步态相位。
/// 它需要被外部代码手动更新。
/// </summary>
public class GaitPhaseTracker
{
    // --- 公共API ---
    public float CurrentPhase { get; private set; }

    public bool IsSafeToTransition => IsLeftFootDown || IsRightFootDown;
    public bool IsLeftFootDown => CurrentPhase >= 0f && CurrentPhase < 0.1f || CurrentPhase >= 0.5f && CurrentPhase < 0.6f;
    public bool IsRightFootDown => CurrentPhase >= 0.25f && CurrentPhase < 0.35f || CurrentPhase >= 0.75f && CurrentPhase < 0.85f;

    /// <summary>
    /// 手动更新相位。应在每帧调用。
    /// </summary>
    /// <param name="currentState">要追踪的Animancer状态</param>
    public void Update(AnimancerState currentState)
    {
        if (currentState != null && currentState.IsPlaying && currentState.Length > 0)
        {
            CurrentPhase = currentState.NormalizedTime;
        }
    }
}

/// <summary>
/// 一个静态辅助类，用于执行基于相位的动画过渡。
/// </summary>
public static class PhaseMatcher
{
    /// <summary>
    /// 以相位匹配的方式，将Animancer过渡到一个新的动画片段。
    /// </summary>
    /// <param name="animancer">目标Animancer组件。</param>
    /// <param name="clipToPlay">要播放的新动画片段。</param>
    /// <param name="fadeDuration">过渡的淡入淡出时间。</param>
    /// <param name="referencePhase">用于对齐的参考相位 (0-1)。</param>
    /// <returns>播放的新动画状态。</returns>
    public static AnimancerState Transition(
        AnimancerComponent animancer,
        AnimationClip clipToPlay,
        float fadeDuration,
        float referencePhase)
    {
        if (clipToPlay == null)
        {
            Debug.LogError("尝试进行相位匹配的AnimationClip为空！");
            return null;
        }

        // 1. 获取或创建新动画的状态
        var state = animancer.States.GetOrCreate(clipToPlay);

        // 2. 根据参考相位，计算新动画应该从哪个时间点开始播放
        state.Time = referencePhase * clipToPlay.length;

        // 3. 命令Animancer以指定的淡入时间播放这个已经设置好时间的状态
        animancer.Play(state, fadeDuration);

        return state;
    }

    /// <summary>
    /// 重载方法，方便直接使用GaitPhaseTracker作为相位参考。
    /// </summary>
    public static AnimancerState Transition(
        AnimancerComponent animancer,
        AnimationClip clipToPlay,
        float fadeDuration,
        GaitPhaseTracker phaseTracker)
    {
        // 直接从phaseTracker获取当前相位作为参考
        return Transition(animancer, clipToPlay, fadeDuration, phaseTracker.CurrentPhase);
    }
}