using Animancer;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class AnimancerAsset : PlayableAsset
{
    public TransitionAsset transitionAsset = null;

    public override double duration
    {
        get
        {
            if (transitionAsset == null) return base.duration;

            if (transitionAsset.Transition is ClipTransition clipTransition)
            {
                if (clipTransition.Clip != null)
                    return clipTransition.Clip.length;
            }
            else if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
            {
                if (directionalTransition.AnimationSet != null &&
                    directionalTransition.AnimationSet.GetClip(0) != null)
                {
                    // 使用第一个方向的长度作为基准
                    return directionalTransition.AnimationSet.GetClip(0).length;
                }
            }

            return base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerClip>.Create(graph);
        AnimancerClip clip = playable.GetBehaviour();

        clip.transitionAsset = transitionAsset;

        return playable;
    }
}

public class AnimancerClip : ActionClipBase
{
    public TransitionAsset transitionAsset = null;

    // 动画状态管理
    private AnimancerState _animateState;
    private bool _isDirectionalAnimation;

    // DirectionTransition配置参数
    public float _directionChangeThreshold = 0.1f; // 方向变化阈值
    public float _blendDuration = 0.25f; // 方向切换混合时间
    private PhaseAwareAnimancerPlayer _phaseAwarePlayer; // 动作相位管理
    private Vector2 _lastMoveInput = Vector2.zero;
    private int _currentDirection = -1;

    protected override void OnClipPlay()
    {
        base.OnClipPlay();

        if (transitionAsset == null) return;

        // 初始化方向动画
        if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
        {
            _phaseAwarePlayer = new PhaseAwareAnimancerPlayer(actor.animancer);
            _isDirectionalAnimation = true;
            //UpdateDirectionalAnimation(); //待解决 动作切换时卡顿
            _lastMoveInput = actor.logicInput.MoveInput;
            _currentDirection = CalculateDirection(_lastMoveInput);
            directionalTransition.SetDirection(_currentDirection);
            actor.animancer.Play(directionalTransition.Clip, 0.15f);
        }
        // 处理普通动画
        else if (transitionAsset.Transition is ClipTransition clipTransition)
        {
            _animateState = actor.animancer.Play(clipTransition);
            _isDirectionalAnimation = false;
        }
    }

    protected override void OnClipFrame(Playable playable)
    {
        base.OnClipFrame(playable);

        // 如果是方向动画，持续更新
        if (_isDirectionalAnimation)
        {
            _phaseAwarePlayer?.Update(Time.deltaTime);
            UpdateDirectionalAnimation();
        }
    }

    protected override void OnClipFinish()
    {
        base.OnClipFinish();
        Cleanup();
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();
        // 暂停时也可以考虑清理资源
        Cleanup();
    }

    private void Cleanup()
    {
        _animateState = null;
        _currentDirection = -1;
        _isDirectionalAnimation = false;
        _phaseAwarePlayer = null;
    }

    private void UpdateDirectionalAnimation(bool forceUpdate = false)
    {
        var directionalTransition = transitionAsset.Transition as DirectionalClipTransition;

        Vector2 moveInput = actor.logicInput.MoveInput;

        // 检查输入是否有显著变化或者是否为强制更新
        if (!forceUpdate && Vector2.Distance(moveInput, _lastMoveInput) < _directionChangeThreshold)
            return;

        _lastMoveInput = moveInput;

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
}