using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Animancer;


public class AnimancerBehaviour : ActionBehaviourBase
{
    public TransitionAsset transitionAsset = null;
    private AnimancerState animateState;

    // DirectionTransition配置参数 //
    private float _directionChangeThreshold = 0.1f; // 方向变化阈值
    private float _blendDuration = 0.25f; // 方向切换混合时间 可能越大越自然
    private PhaseAwareAnimancerPlayer _phaseAwarePlayer; // 动作相位管理类
    private Vector2 _lastMoveInput = Vector2.zero; // 最后输入
    private int _currentDirection = -1; // 目前Direction

    protected override void OnClipPlay(Playable playable)
    {
        if (transitionAsset == null) return;

        CleanUp();
        CreateAnimate();
    }

    void CreateAnimate()
    {
        // 初始化方向动画
        if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
        {
            _phaseAwarePlayer = new PhaseAwareAnimancerPlayer(actor.animancer);
            animateState = actor.animancer.Play(directionalTransition.Clip, 0.15f);
        }
        // 初始化普通动画
        else if (transitionAsset.Transition is ClipTransition clipTransition)
        {
            animateState = actor.animancer.Play(clipTransition, 0.15f);
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if(animateState == null) return;

        animateState.Speed = info.effectiveSpeed;
        UpdateAnimationData();

        if(!Application.isPlaying)
            AnimancerSimulate(playable);
    }

    void UpdateAnimationData()
    {
        if (transitionAsset.Transition is DirectionalClipTransition)
        {
            // 如果是方向动画，持续更新
            _phaseAwarePlayer?.Update(Time.deltaTime);
            UpdateDirectionalAnimation();
        }
    }

    void AnimancerSimulate(Playable playable)
    {
        if (animateState == null) return;

        animateState.Speed = 0;

        double currentTime = playable.GetTime();
        double duration = playable.GetDuration();
        double normalizedTime = currentTime / duration;
        animateState.NormalizedTime = (float)normalizedTime;

        actor.animancer.Evaluate();
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();

        // 暂停时也可以考虑清理资源 但不使用默认清理逻辑(CleanUp方法)
        animateState = null;
        _currentDirection = -1;
        _phaseAwarePlayer = null;
    }

    protected override void OnClipFinish(bool isNromal)
    {
        CleanUp();

        if (!Application.isPlaying)
            OnEditorFinish();
    }

    void OnEditorFinish()
    {
        if (animateState == null) return;

        animateState.Speed = 0;
        animateState.NormalizedTime = 0;
    }

    protected override void CleanUp()
    {
        base.CleanUp();

        animateState = null;
        _currentDirection = -1;
        _phaseAwarePlayer = null;
    }

    #region 相位匹配辅助方法

    private void UpdateDirectionalAnimation(bool forceUpdate = false)
    {
        var directionalTransition = transitionAsset.Transition as DirectionalClipTransition;

        Vector2 moveInput = actor.logicInput.MoveInput;

        // 检查输入是否有显著变化或者是否为强制更新
        if (!forceUpdate && Vector2.Distance(moveInput, _lastMoveInput) < _directionChangeThreshold)
            return;

        // 计算新方向
        int newDirection = CalculateDirection(moveInput);

        // 方向未变化时跳过
        if (newDirection == _currentDirection)
            return;

        _currentDirection = newDirection;
        directionalTransition.SetDirection(_currentDirection);

        // 获取新动画
        var newClip = directionalTransition.Clip;
        if (newClip == null) return;

        // 使用相位匹配播放
        _phaseAwarePlayer.NormalPhaseTransition(newClip, _blendDuration);
    }

    private int CalculateDirection(Vector2 input)
    {
        // 无输入时保持当前方向
        if (input.sqrMagnitude < 0.01f)
            return (_currentDirection > 0) ? _currentDirection : 0;

        return (int)DirectionalAnimationSet8.VectorToDirection(input);
    }

    #endregion

}
