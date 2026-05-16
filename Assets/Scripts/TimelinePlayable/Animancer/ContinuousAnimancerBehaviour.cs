using UnityEngine;
using UnityEngine.Playables;
using Animancer;

/// <summary>
/// 播放 Animancer 动画并每帧持续注入 Mixer 参数的 Timeline Behaviour。
/// 适用于 Locomotion 等需要持续响应外部数据驱动动画混合的场景。
/// 支持多种 TransitionAsset 类型（ClipTransition、MixerTransition2D、LinearMixerTransition 等）。
/// </summary>
public class ContinuousAnimancerBehaviour : ActionBehaviourBase
{
    // --- 配置数据 ---
    public TransitionAsset transitionAsset;
    public ContinuousParameterSource parameterSource;

    // --- 运行时状态 ---
    private AnimancerState _currentState;

    #region 基类方法实现

    /// <summary>
    /// 开始播放：播放动画，初始化参数为零值。
    /// </summary>
    protected override void OnClipStart(Playable playable)
    {
        if (transitionAsset == null || transitionAsset.Transition == null) return;
        if (actor == null || actor.animancer == null) return;

        ITransition transition = transitionAsset.Transition;

        // 播放动画
        _currentState = actor.animancer.Play(transition);

        // 初始化 Mixer 参数为零值（防止第一帧闪烁）
        if (_currentState is MixerState<Vector2> mixer2D)
            mixer2D.Parameter = Vector2.zero;
        else if (_currentState is MixerState<float> mixer1D)
            mixer1D.Parameter = 0f;

        // 同步时间
        if (_currentState != null)
        {
            _currentState.IsPlaying = true;
            _currentState.Time = (float)playable.GetTime();
        }
    }

    /// <summary>
    /// 每帧更新：同步速度 + 持续注入 Mixer 参数。
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

        // 每帧注入参数
        switch (parameterSource)
        {
            case ContinuousParameterSource.LocomotionIntent:
                UpdateMixerFromLocomotionIntent();
                break;
            case ContinuousParameterSource.CharacterVelocity:
                UpdateMixerFromVelocity();
                break;
            case ContinuousParameterSource.VerticalVelocity:
                UpdateMixerFromVerticalVelocity();
                break;
        }
    }

    protected override void OnClipPause()
    {
        if (_currentState != null)
            _currentState.IsPlaying = false;
    }

    protected override void OnClipResume(Playable playable)
    {
        if (_currentState != null)
            _currentState.IsPlaying = true;
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (_currentState != null)
            _currentState.IsPlaying = false;
    }

    protected override void CleanUp()
    {
        _currentState = null;
    }

    #endregion

    #region 参数注入方法

    /// <summary>
    /// LocomotionIntent 模式：从移动意图计算 Mixer 参数。
    /// 以 FacingDirection 为参考系（锁定时稳定），将移动方向投影到本地空间。
    /// </summary>
    private void UpdateMixerFromLocomotionIntent()
    {
        if (actor?.actorMotor == null) return;

        var intent = actor.actorMotor.LocomotionIntent;

        if (_currentState is MixerState<Vector2> mixer2D)
        {
            // 确定参考系：优先用 FacingDirection（锁定时=朝敌人方向，恒定稳定）
            Vector3 referenceForward = intent.FacingDirection;
            referenceForward.y = 0f;

            if (referenceForward.sqrMagnitude < 0.0001f)
            {
                // 自由移动时没有显式朝向 → fallback 到角色当前朝向
                referenceForward = actor.transform.forward;
            }
            referenceForward.Normalize();

            // 将移动方向投影到参考系的本地空间
            Vector3 moveDir = intent.WorldMoveDirection;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude < 0.0001f || intent.MoveStrength < 0.01f)
            {
                mixer2D.Parameter = Vector2.zero;
                return;
            }

            moveDir.Normalize();
            Quaternion refRotation = Quaternion.LookRotation(referenceForward, Vector3.up);
            Vector3 localDir = Quaternion.Inverse(refRotation) * moveDir;

            Vector2 param = new Vector2(localDir.x, localDir.z) * intent.MoveStrength;
            // Clamp 防止超出 Mixer 范围
            if (param.sqrMagnitude > 1f) param.Normalize();
            mixer2D.Parameter = param;
        }
        else if (_currentState is MixerState<float> mixer1D)
        {
            // 1D Mixer：直接用 MoveStrength 作为参数
            mixer1D.Parameter = intent.MoveStrength;
        }
    }

    /// <summary>
    /// CharacterVelocity 模式：从角色实际物理速度推算 Mixer 参数。
    /// 将世界速度转到角色本地空间，归一化后写入 Mixer。
    /// </summary>
    private void UpdateMixerFromVelocity()
    {
        if (actor?.actorMotor == null) return;

        Vector3 velocity = actor.actorMotor.CurrentVelocity;
        velocity.y = 0f; // 只看水平速度

        if (_currentState is MixerState<Vector2> mixer2D)
        {
            Vector3 localVel = actor.transform.InverseTransformDirection(velocity);
            float maxSpeed = actor.actorMotor.LocomotionBaseSpeed;
            if (maxSpeed > 0.001f)
            {
                Vector2 param = new Vector2(localVel.x / maxSpeed, localVel.z / maxSpeed);
                if (param.sqrMagnitude > 1f) param.Normalize();
                mixer2D.Parameter = param;
            }
            else
            {
                mixer2D.Parameter = Vector2.zero;
            }
        }
        else if (_currentState is MixerState<float> mixer1D)
        {
            float speed = velocity.magnitude;
            float maxSpeed = actor.actorMotor.LocomotionBaseSpeed;
            mixer1D.Parameter = maxSpeed > 0.001f ? speed / maxSpeed : 0f;
        }
    }

    /// <summary>
    /// VerticalVelocity 模式：直接注入角色竖直速度（y 轴，原始 m/s 值）。
    /// 仅对 1D LinearMixer 有意义；其他类型 Mixer 保持不写入，避免语义错乱。
    /// 典型场景：跳跃/下落按 y 速度分段混合（Threshold 直接配实际 m/s）。
    /// </summary>
    private void UpdateMixerFromVerticalVelocity()
    {
        if (actor?.actorMotor == null) return;

        if (_currentState is MixerState<float> mixer1D)
        {
            mixer1D.Parameter = actor.actorMotor.CurrentVelocity.y;
        }
        // 2D Mixer 等其他类型：此模式无对应语义，跳过。
    }

    #endregion
}
