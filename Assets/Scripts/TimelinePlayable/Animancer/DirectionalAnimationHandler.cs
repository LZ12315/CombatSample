using UnityEngine;
using Animancer;

public class DirectionalAnimationHandler
{
    private readonly Actor _actor;
    private readonly DirectionalClipTransition _transition;

    private const float DirectionChangeThreshold = 0.1f;
    private Vector2 _lastMoveInput;
    private int _currentDirection = -1;

    public DirectionalAnimationHandler(Actor actor, DirectionalClipTransition transition)
    {
        _actor = actor;
        _transition = transition;

        // 初始化：强制计算一次方向
        UpdateDirectionInternal(forceUpdate: true);
    }

    public AnimancerState Update()
    {
        if (_actor == null || _actor.logicInput == null) return null;

        Vector2 moveInput = _actor.logicInput.MoveInput;

        // 检查输入是否有显著变化
        if (Vector2.Distance(moveInput, _lastMoveInput) < DirectionChangeThreshold)
            return null;

        _lastMoveInput = moveInput;
        int newDirection = CalculateDirection(moveInput);

        // 如果方向没有变化，直接返回
        if (newDirection == _currentDirection)
            return null;

        return UpdateDirectionInternal(false, newDirection);
    }

    private AnimancerState UpdateDirectionInternal(bool forceUpdate, int newDirection = 0)
    {
        if (forceUpdate)
        {
            if (_actor.logicInput != null)
                newDirection = CalculateDirection(_actor.logicInput.MoveInput);
            else
                newDirection = 0;
        }

        _currentDirection = newDirection;
        _transition.SetDirection(_currentDirection);

        // 如果不是初始化，我们需要直接播放目标Clip来强制刷新
        if (!forceUpdate)
        {
            // 【修正】使用 AnimationSet 属性
            var targetClip = _transition.AnimationSet.GetClip(_currentDirection);
            if (targetClip != null)
            {
                return _actor.animancer.Play(targetClip, _transition.FadeDuration);
            }
        }

        return null;
    }

    private int CalculateDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
            return (_currentDirection == -1) ? 0 : _currentDirection;

        return (int)DirectionalAnimationSet8.VectorToDirection(input);
    }
}