using System;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// ActorMotor 每帧传给运行时的配置快照。
/// 配置由 ActorMotor 的序列化字段持有，ActorMotionRuntime 只消费快照，
/// 避免 plain C# runtime 反向依赖 MonoBehaviour。
/// </summary>
public readonly struct ActorMotionRuntimeConfig
{
    public readonly float HorizontalDrag;
    public readonly float VerticalImpulseAirDrag;
    public readonly float VerticalSmoothTime;

    public ActorMotionRuntimeConfig(
        float horizontalDrag,
        float verticalImpulseAirDrag,
        float verticalSmoothTime)
    {
        HorizontalDrag = horizontalDrag;
        VerticalImpulseAirDrag = verticalImpulseAirDrag;
        VerticalSmoothTime = verticalSmoothTime;
    }
}

/// <summary>
/// ActorMotor 持有的纯 C# 运动运行时。
/// 它聚合 KCC tick 期间会变化的运动状态，并协调 MotionChannels、
/// GroundingRuntime、RootMotionBuffer 与 VelocityReadout。
/// </summary>
public sealed class ActorMotionRuntime
{
    #region === 子运行时与策略状态 ===

    private readonly MotionChannels _channels = new();
    private readonly GroundingRuntime _grounding = new();
    private readonly RootMotionBuffer _rootMotion = new();
    private readonly SelfRotationBuffer _selfRotation = new();
    private readonly VelocityReadout _velocity = new();

    private bool _pendingForceUnground;
    private bool _forceUngroundedThisTick;
    private bool _pendingCeilingHit;

    private float _movementTimeScale = 1f;
    private float _gravityScale = 1f;
    private RootMotionApplyMode _rootMotionApplyMode = RootMotionApplyMode.External;
    private bool _animatorRootMotionSuppressed;

    #endregion

    #region === 对外只读状态 ===

    public ActorGroundState GroundState => _grounding.State;
    public Vector3 CurrentVelocity => _velocity.CurrentVelocity;
    public float CurrentHorizontalSpeed => _velocity.CurrentHorizontalSpeed;
    public float CurrentVerticalSpeed => _velocity.CurrentVerticalSpeed;
    public bool ForceUngroundedThisTick => _forceUngroundedThisTick;

    public float MovementTimeScale => _movementTimeScale;
    public float GravityScale => _gravityScale;

    public int JumpCount => _grounding.JumpCount;

    public MotionChannels Channels => _channels;
    public bool HasSelfRotationTick => _selfRotation.HasTickOwner;
    public Quaternion SelfRotationLocalYawDelta => _selfRotation.TickLocalYawDelta;

    /// <summary>
    /// 当前 RootMotion 策略允许 ActorMotor 应用的根旋转。
    /// External 模式下返回 identity，避免动画根旋转与外部旋转重复叠加。
    /// </summary>
    public Quaternion AppliedRootMotionRotation =>
        ShouldApplyRootMotion ? _rootMotion.PendingRotation : Quaternion.identity;

    #endregion

    #region === 接地事件转发 ===

    public event Action OnLanded
    {
        add => _grounding.OnLanded += value;
        remove => _grounding.OnLanded -= value;
    }

    public event Action OnLeftGround
    {
        add => _grounding.OnLeftGround += value;
        remove => _grounding.OnLeftGround -= value;
    }

    #endregion

    #region === 策略设置 ===

    public void SetMovementTimeScale(float scale)
    {
        _movementTimeScale = Mathf.Max(0f, scale);
    }

    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    public void SetRootMotionApplyMode(RootMotionApplyMode mode)
    {
        _rootMotionApplyMode = mode;
    }

    public void SetAnimatorRootMotionSuppressed(bool suppressed)
    {
        _animatorRootMotionSuppressed = suppressed;
        if (suppressed)
            _rootMotion.ClearAnimator();
    }

    #endregion

    #region === Motor Tick 生命周期 ===

    public void BeginMotorTick()
    {
        _forceUngroundedThisTick = false;
        _rootMotion.BeginMotorTick();
        _selfRotation.BeginMotorTick();
    }

    public void EndMotorTick()
    {
        _forceUngroundedThisTick = false;
    }

    #endregion

    #region === KCC Tick 驱动 ===

    public bool ConsumeForceUngroundRequest()
    {
        bool result = _pendingForceUnground;
        _pendingForceUnground = false;
        return result;
    }

    public void MarkForcedUngroundedThisTick()
    {
        _forceUngroundedThisTick = true;
        _grounding.ForceUngroundNow();
    }

    public void SignalCeilingHit()
    {
        _pendingCeilingHit = true;
    }

    /// <summary>
    /// 推进内部运动通道（重力、冲量阻尼等）。
    /// motionDeltaTime = realDeltaTime * MovementTimeScale，
    /// 确保 HitStop / HitStick 期间运动内部演化与输出速度同步减慢。
    /// </summary>
    public void StepChannels(
        float motionDeltaTime,
        bool grounded,
        ActorMotionRuntimeConfig config)
    {
        _channels.StepGravity(motionDeltaTime, grounded, _gravityScale);
        _channels.StepHorizontalDrag(motionDeltaTime, config.HorizontalDrag);
        _channels.StepVerticalImpulse(
            motionDeltaTime,
            !grounded,
            grounded,
            _pendingCeilingHit,
            config.VerticalImpulseAirDrag);

        _pendingCeilingHit = false;
    }

    /// <summary>
    /// 合成送给 KCC 的请求速度。
    /// deltaTime 是 KCC 传入的真实 tick delta，用于 RootMotion
    /// 位移→速度换算；不在此处参与 time scale 语义。
    /// </summary>
    public Vector3 ComposeKccVelocity(
        KinematicCharacterMotor motor,
        Vector3 locomotionVelocity,
        bool isGrounded,
        Quaternion tickStartRotation,
        float deltaTime)
    {
        float ts = _movementTimeScale;

        if (!_rootMotion.HasTrajectoryTick
            && ShouldApplyRootMotion
            && _rootMotion.PendingPosition.sqrMagnitude > 0.0001f)
        {
            Vector3 legacyRootVelocity = _rootMotion.PendingPosition / deltaTime * ts;
            if (isGrounded)
                legacyRootVelocity = motor.GetDirectionTangentToSurface(
                    legacyRootVelocity,
                    motor.GroundingStatus.GroundNormal) * legacyRootVelocity.magnitude;
            return legacyRootVelocity;
        }

        Vector3 horizontal;
        if (_channels.TryComposeHorizontalVelocityOwner(ts, out horizontal))
        {
            horizontal.y = 0f;
        }
        else if (_rootMotion.HasTrajectoryTick)
        {
            Vector3 localDelta = _rootMotion.TrajectoryLocalPosition;
            localDelta.y = 0f;
            horizontal = tickStartRotation * localDelta / deltaTime;
            horizontal.y = 0f;
            horizontal += _channels.HorizontalImpulseVelocity * ts;
        }
        else
        {
            horizontal = _channels.ComposeHorizontal(locomotionVelocity, ts);
        }

        float vertical = _channels.ComposeVertical(ts);

        if (isGrounded)
        {
            horizontal = motor.GetDirectionTangentToSurface(
                horizontal,
                motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
            vertical = 0f;
        }

        Vector3 characterUp = motor != null ? motor.CharacterUp : Vector3.up;
        return horizontal + characterUp * vertical;
    }

    public void PublishSolvedVelocity(
        Vector3 solvedVelocity,
        bool isStableGrounded,
        float smoothingDeltaTime,
        float verticalSmoothTime)
    {
        _velocity.Publish(
            solvedVelocity,
            isStableGrounded,
            smoothingDeltaTime,
            verticalSmoothTime);
    }

    #endregion

    #region === MotionChannels 门面 ===

    public void AddVerticalImpulse(float speed)
    {
        _channels.ApplyVerticalImpulse(speed);
        if (speed > 0f)
            _pendingForceUnground = true;
    }

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        _channels.AddHorizontalImpulse(velocity);
    }

    public void ClearHorizontalImpulse()
    {
        _channels.ClearHorizontalImpulse();
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        return _channels.BeginHorizontalVelocity();
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        _channels.SetHorizontalVelocity(owner, velocity);
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        _channels.EndHorizontalVelocity(owner);
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return _channels.BeginVerticalVelocity();
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        if (!_channels.SetVerticalVelocity(owner, verticalSpeed))
            return;

        if (_channels.IsTopVerticalVelocityOwner(owner) &&
            verticalSpeed > 0.001f &&
            _grounding.State is ActorGroundState.Grounded or ActorGroundState.JustLanded)
        {
            _pendingForceUnground = true;
        }
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        _channels.EndVerticalVelocity(owner);
    }

    public void ClearVelocityOwners()
    {
        _channels.ClearVelocityOwners();
    }

    public void ApplyMotionHandoff(float horizontalInheritance, float verticalInheritance)
    {
        _channels.ApplyHandoff(horizontalInheritance, verticalInheritance);
    }

    #endregion

    #region === GroundingRuntime 门面 ===

    public void ApplyKccGrounding(bool isStableNow, bool wasStable)
    {
        _grounding.ApplyKccGrounding(isStableNow, wasStable);
    }

    public bool CanJump(int maxJumpCount)
    {
        return _grounding.CanJump(maxJumpCount);
    }

    public void ConsumeJump()
    {
        _grounding.ConsumeJump();
    }

    #endregion

    #region === RootMotionBuffer 门面 ===

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation)
    {
        if (_animatorRootMotionSuppressed)
            return;

        _rootMotion.AddAnimatorDelta(deltaPosition, deltaRotation);
    }

    public MotionOwner BeginTrajectoryRootMotion()
    {
        _rootMotion.ClearAnimator();
        return _rootMotion.BeginTrajectory();
    }

    public bool SubmitTrajectoryRootMotion(MotionOwner owner, Vector3 localPositionDelta)
    {
        return _rootMotion.SubmitTrajectory(owner, localPositionDelta);
    }

    public void EndTrajectoryRootMotion(MotionOwner owner)
    {
        _rootMotion.EndTrajectory(owner);
    }

    #endregion

    #region === SelfRotationBuffer 门面 ===

    public bool TryBeginSelfRotation(out MotionOwner owner)
    {
        return _selfRotation.TryBegin(out owner);
    }

    public bool SubmitSelfRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return _selfRotation.Submit(owner, localYawDelta);
    }

    public bool EndSelfRotation(MotionOwner owner)
    {
        return _selfRotation.End(owner);
    }

    #endregion

    #region === 内部工具 ===

    private bool ShouldApplyRootMotion =>
        _rootMotionApplyMode == RootMotionApplyMode.Managed && !_animatorRootMotionSuppressed;

    #endregion
}
