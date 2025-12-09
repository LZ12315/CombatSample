using UnityEngine;
using Animancer;

/// <summary>
/// 辅助类：负责在运行时根据 Input 切换 DirectionalClipTransition 的方向。
/// 包含输入阈值检测和相位同步逻辑，确保动画切换丝滑。
/// </summary>
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
        // 构造函数不做播放操作
    }

    /// <summary>
    /// 初始化并播放初始方向的动画
    /// </summary>
    public AnimancerState Initialize()
    {
        return UpdateDirectionInternal(forceUpdate: true);
    }

    /// <summary>
    /// 运行时更新方向
    /// </summary>
    public AnimancerState Update()
    {
        if (_actor == null || _actor.logicInput == null) return null;

        Vector2 moveInput = _actor.logicInput.MoveInput;

        if (Vector2.Distance(moveInput, _lastMoveInput) < DirectionChangeThreshold)
            return null;

        _lastMoveInput = moveInput;
        int newDirection = CalculateDirection(moveInput);

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

        // 直接播放目标 Clip
        var targetClip = _transition.AnimationSet.GetClip(_currentDirection);

        if (targetClip != null)
        {
            if (!forceUpdate)
            {
                // 运行时切换：带相位同步
                var oldState = _actor.animancer.States.Current;
                var newState = _actor.animancer.Play(targetClip, _transition.FadeDuration);

                if (oldState != null)
                {
                    newState.NormalizedTime = oldState.NormalizedTime;
                }
                return newState;
            }
            else
            {
                // 初始化：直接播放
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