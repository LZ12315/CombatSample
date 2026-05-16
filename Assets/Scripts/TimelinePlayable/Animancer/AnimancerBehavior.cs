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
    public AnimancerParameterMode parameterMode;
    public Vector2 fallbackVector2;
    public float fallbackFloat;

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
        InitializeMixerParameter();

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

        // 同步 Timeline 速度给 Animancer
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

    private void InitializeMixerParameter()
    {
        if (_currentState is MixerState<Vector2> mixer2D)
        {
            switch (parameterMode)
            {
                case AnimancerParameterMode.ContextDirection2D:
                    mixer2D.Parameter = TryGetContextDirection2D(out var dir2D) ? dir2D : fallbackVector2;
                    break;
                case AnimancerParameterMode.SerializedFallback:
                    mixer2D.Parameter = fallbackVector2;
                    break;
                case AnimancerParameterMode.LocomotionIntent:
                    mixer2D.Parameter = TryGetLocomotionIntent2D(out var intent2D) ? intent2D : fallbackVector2;
                    break;
            }
        }
        else if (_currentState is MixerState<float> mixer1D)
        {
            switch (parameterMode)
            {
                case AnimancerParameterMode.ContextMagnitude:
                    mixer1D.Parameter = TryGetContextMagnitude(out var magnitude) ? magnitude : fallbackFloat;
                    break;
                case AnimancerParameterMode.SerializedFallback:
                    mixer1D.Parameter = fallbackFloat;
                    break;
                case AnimancerParameterMode.LocomotionIntent:
                    mixer1D.Parameter = TryGetLocomotionIntentMagnitude(out var intentMagnitude) ? intentMagnitude : fallbackFloat;
                    break;
            }
        }
    }

    private bool TryGetContextDirection2D(out Vector2 parameter)
    {
        parameter = fallbackVector2;
        if (actionInstance == null || actor == null)
            return false;

        Vector3 direction = actionInstance.EventContext.Direction;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 localDirection = actor.transform.InverseTransformDirection(direction.normalized);
        parameter = new Vector2(localDirection.x, localDirection.z);
        if (parameter.sqrMagnitude <= 0.0001f)
            return false;

        parameter.Normalize();
        return true;
    }

    private bool TryGetContextMagnitude(out float magnitude)
    {
        magnitude = fallbackFloat;
        if (actionInstance == null)
            return false;

        magnitude = actionInstance.EventContext.Magnitude;
        return Mathf.Abs(magnitude) > 0.001f;
    }

    private bool TryGetLocomotionIntent2D(out Vector2 parameter)
    {
        parameter = fallbackVector2;
        if (actor?.actorMotor == null)
            return false;

        var intent = actor.actorMotor.LocomotionIntent;

        Vector3 referenceForward = intent.FacingDirection;
        referenceForward.y = 0f;

        if (referenceForward.sqrMagnitude < 0.0001f)
        {
            referenceForward = actor.transform.forward;
        }
        referenceForward.Normalize();

        Vector3 moveDir = intent.WorldMoveDirection;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.0001f || intent.MoveStrength < 0.01f)
            return false;

        moveDir.Normalize();
        Quaternion refRotation = Quaternion.LookRotation(referenceForward, Vector3.up);
        Vector3 localDir = Quaternion.Inverse(refRotation) * moveDir;

        parameter = new Vector2(localDir.x, localDir.z) * intent.MoveStrength;
        if (parameter.sqrMagnitude > 1f)
            parameter.Normalize();

        return true;
    }

    private bool TryGetLocomotionIntentMagnitude(out float magnitude)
    {
        magnitude = fallbackFloat;
        if (actor?.actorMotor == null)
            return false;

        magnitude = actor.actorMotor.LocomotionIntent.MoveStrength;
        return true;
    }

    #endregion
}