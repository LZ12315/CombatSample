using Animancer;
using UnityEngine;
using System.Collections;

public class GaitPhaseTracker
{
    private float _currentPhase; // 当前步态相位 (0-1)
    private float _phaseVelocity; // 用于平滑相位变化

    public void Update(float deltaTime, AnimancerState currentState)
    {
        if (currentState == null || !currentState.IsPlaying)
            return;

        // 计算基础相位 (基于动画播放进度)
        float rawPhase = currentState.NormalizedTime % 1f;

        // 应用平滑处理 (避免帧率波动影响)
        _currentPhase = Mathf.SmoothDamp(_currentPhase, rawPhase,
                                        ref _phaseVelocity, 0.05f);
    }

    public float CurrentPhase => _currentPhase;

    // 获取当前步态周期的关键点
    public bool IsLeftFootDown => _currentPhase > 0.25f && _currentPhase < 0.35f;
    public bool IsRightFootDown => _currentPhase > 0.8f && _currentPhase < 0.9f;
    public bool IsTransitionSafe => IsLeftFootDown || IsRightFootDown;
}

public class PhaseAwareAnimancerPlayer
{
    private readonly AnimancerComponent _animancer;
    private readonly GaitPhaseTracker _phaseTracker;

    public PhaseAwareAnimancerPlayer(AnimancerComponent animancer)
    {
        _animancer = animancer;
        _phaseTracker = new GaitPhaseTracker();
    }

    public void Update(float deltaTime)
    {
        _phaseTracker.Update(deltaTime, _animancer.States.Current);
    }

    public AnimancerState Play(AnimationClip clip, float blendTime = 0.15f)
    {
        // 获取当前步态相位
        float currentPhase = _phaseTracker.CurrentPhase;

        // 计算新动画的起始时间
        float startTime = CalculatePhaseAlignedTime(clip, currentPhase);

        // 获取或创建状态但不立即播放
        var state = _animancer.States.GetOrCreate(clip);
        // 先设置时间位置
        state.Time = startTime;
        // 然后应用平滑过渡
        _animancer.Play(state, blendTime);

        return state;
    }

    private float CalculatePhaseAlignedTime(AnimationClip clip, float referencePhase)
    {
        // 确保相位在0-1范围内
        float normalizedPhase = referencePhase % 1f;

        // 计算时间位置
        return normalizedPhase * clip.length;
    }

    // 一般过渡方法（直接切换）
    public void NormalPhaseTransition(AnimationClip clip, float blendTime = 0.15f)
    {
        Play(clip, blendTime);
    }

    // 安全过渡方法（在脚步落地时切换）
    public void SafeTransition(AnimationClip clip, float blendTime = 0.15f)
    {
        if (_phaseTracker.IsTransitionSafe)
            Play(clip, blendTime);
        else
        {
            // 延迟到安全点切换
            _animancer.StartCoroutine(DelayedTransition(clip, blendTime));
        }
    }

    private IEnumerator DelayedTransition(AnimationClip clip, float blendTime)
    {
        // 等待下一个安全过渡点
        while (!_phaseTracker.IsTransitionSafe)
        {
            yield return null;
        }

        Play(clip, blendTime);
    }
}
