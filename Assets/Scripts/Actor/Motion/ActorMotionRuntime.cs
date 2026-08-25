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
/// ActorMotor compatibility façade over the E3 Translation / Rotation Domain
/// and supporting motion state.
/// ActorMotor 的 first-class Translation / Rotation Domain 与 supporting state
/// 由这里集中保存；旧 MotionRuntime API 暂时转发到这些权威对象。
/// </summary>
public sealed class ActorMotionRuntime
{
    #region === 子运行时与策略状态 ===

    private readonly TranslationDomain _translation = new();
    private readonly MotionChannels _channels;
    private readonly RotationDomain _rotation = new();
    private readonly MotionPolicyState _policy = new();
    private readonly GroundingRuntime _grounding = new();
    private readonly RootMotionBuffer _rootMotion = new();
    private readonly SelfRotationBuffer _rootRotation = new();
    private readonly SelfRotationBuffer _scriptedRotation = new();
    private readonly VelocityReadout _velocity = new();

    private bool _pendingForceUnground;
    private bool _forceUngroundedThisTick;
    private bool _pendingCeilingHit;

    private float _movementTimeScale = 1f;
    private RootMotionApplyMode _rootMotionApplyMode = RootMotionApplyMode.External;
    private bool _animatorRootMotionSuppressed;

    public ActorMotionRuntime()
    {
        _channels = new MotionChannels(_translation);
    }

    #endregion

    #region === 对外只读状态 ===

    public ActorGroundState GroundState => _grounding.State;
    public Vector3 CurrentVelocity => _velocity.CurrentVelocity;
    public float CurrentHorizontalSpeed => _velocity.CurrentHorizontalSpeed;
    public float CurrentVerticalSpeed => _velocity.CurrentVerticalSpeed;
    public bool ForceUngroundedThisTick => _forceUngroundedThisTick;

    public float MovementTimeScale => _movementTimeScale;
    public float GravityScale => _policy.GravityScale;
    public float LocomotionScale => _policy.LocomotionScale;
    public float AirLocomotionScale => _policy.AirLocomotionScale;

    public int JumpCount => _grounding.JumpCount;

    public TranslationDomain Translation => _translation;
    public RotationDomain Rotation => _rotation;
    public MotionPolicyState Policy => _policy;
    public MotionChannels Channels => _channels;
    public bool HasSelfRotationTick => _scriptedRotation.HasTickOwner;
    public Quaternion SelfRotationLocalYawDelta => _scriptedRotation.TickLocalYawDelta;

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
        _policy.SetBaseGravityScale(scale);
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
        _rootRotation.BeginMotorTick();
        _scriptedRotation.BeginMotorTick();
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
        _translation.StepHorizontalDrag(motionDeltaTime, config.HorizontalDrag);
        _translation.StepBallistic(
            motionDeltaTime,
            grounded,
            _pendingCeilingHit,
            _policy.GravityScale,
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
        if (deltaTime <= 0f)
            return Vector3.zero;

        float ts = _movementTimeScale;

        Vector3 characterUp = motor != null ? motor.CharacterUp : Vector3.up;
        Vector3 horizontal;
        if (_translation.TryComposeHorizontalVelocityOwner(ts, out horizontal))
        {
        }
        else if (_rootMotion.HasTrajectoryTick)
        {
            Vector3 localDelta = _rootMotion.TrajectoryLocalPosition;
            localDelta.y = 0f;
            horizontal = tickStartRotation * localDelta / deltaTime;
        }
        else if (ShouldApplyRootMotion && _rootMotion.PendingPosition.sqrMagnitude > 0.0001f)
        {
            horizontal = _rootMotion.PendingPosition / deltaTime * ts;
        }
        else
        {
            horizontal = _translation.ComposeHorizontal(locomotionVelocity, ts);
        }

        horizontal = Vector3.ProjectOnPlane(horizontal, characterUp);
        float vertical = _translation.ComposeVertical(ts);

        if (isGrounded && motor != null)
        {
            horizontal = motor.GetDirectionTangentToSurface(
                horizontal,
                motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
            vertical = 0f;
        }
        else if (isGrounded)
        {
            vertical = 0f;
        }

        return horizontal + characterUp * vertical;
    }

    public Quaternion PrepareRequestedRotation(
        Quaternion tickStartRotation,
        Quaternion locomotionRotation)
    {
        _rotation.Prepare(
            tickStartRotation,
            locomotionRotation,
            _scriptedRotation.HasTickOwner,
            _scriptedRotation.TickLocalYawDelta,
            _rootRotation.HasTickOwner || AppliedRootMotionRotation != Quaternion.identity,
            _rootRotation.HasTickOwner ? _rootRotation.TickLocalYawDelta : AppliedRootMotionRotation);
        return _rotation.RequestedRotation;
    }

    public void PublishSolvedVelocity(
        Vector3 solvedVelocity,
        Vector3 characterUp,
        bool isStableGrounded,
        float smoothingDeltaTime,
        float verticalSmoothTime)
    {
        _velocity.Publish(
            solvedVelocity,
            characterUp,
            isStableGrounded,
            smoothingDeltaTime,
            verticalSmoothTime);
    }

    #endregion

    #region === MotionChannels 门面 ===

    public void AddVerticalImpulse(float speed)
    {
        AddBallisticVerticalVelocity(speed);
    }

    public void AddBallisticVerticalVelocity(float speed)
    {
        _translation.AddBallisticVerticalVelocity(speed);
        if (speed > 0f)
            _pendingForceUnground = true;
    }

    public void SetBallisticVerticalVelocity(float speed)
    {
        _translation.SetBallisticVerticalVelocity(speed);
        if (speed > 0f)
            _pendingForceUnground = true;
    }

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        _translation.AddHorizontalImpulse(velocity);
    }

    public void ClearHorizontalImpulse()
    {
        _translation.ClearHorizontalImpulse();
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        return _translation.BeginHorizontalVelocity();
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        _translation.SetHorizontalVelocity(owner, velocity);
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        _translation.EndHorizontalVelocity(owner);
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return _translation.BeginVerticalVelocity();
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        if (!_translation.SetVerticalVelocity(owner, verticalSpeed))
            return;

        if (_translation.IsTopVerticalVelocityOwner(owner) &&
            verticalSpeed > 0.001f &&
            _grounding.State is ActorGroundState.Grounded or ActorGroundState.JustLanded)
        {
            _pendingForceUnground = true;
        }
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        _translation.EndVerticalVelocity(owner);
    }

    public void ClearVelocityOwners()
    {
        _translation.ClearVelocityOwners();
    }

    public void ApplyMotionHandoff(float horizontalInheritance, float verticalInheritance)
    {
        _translation.ApplyHandoff(horizontalInheritance, verticalInheritance);
    }

    public MotionOwner BeginLocomotionScale(float scale)
    {
        return _policy.BeginLocomotionScale(scale);
    }

    public bool UpdateLocomotionScale(MotionOwner owner, float scale)
    {
        return _policy.UpdateLocomotionScale(owner, scale);
    }

    public bool EndLocomotionScale(MotionOwner owner)
    {
        return _policy.EndLocomotionScale(owner);
    }

    public MotionOwner BeginAirLocomotionScale(float scale)
    {
        return _policy.BeginAirLocomotionScale(scale);
    }

    public bool UpdateAirLocomotionScale(MotionOwner owner, float scale)
    {
        return _policy.UpdateAirLocomotionScale(owner, scale);
    }

    public bool EndAirLocomotionScale(MotionOwner owner)
    {
        return _policy.EndAirLocomotionScale(owner);
    }

    public MotionOwner BeginGravityScale(float scale)
    {
        return _policy.BeginGravityScale(scale);
    }

    public bool UpdateGravityScale(MotionOwner owner, float scale)
    {
        return _policy.UpdateGravityScale(owner, scale);
    }

    public bool EndGravityScale(MotionOwner owner)
    {
        return _policy.EndGravityScale(owner);
    }

    public void ClearPolicyOwners()
    {
        _policy.ClearPolicyOwners();
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
        return BeginScriptedRotation(out owner);
    }

    public bool SubmitSelfRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return SubmitScriptedRotation(owner, localYawDelta);
    }

    public bool EndSelfRotation(MotionOwner owner)
    {
        return EndScriptedRotation(owner);
    }

    public bool BeginRootRotation(out MotionOwner owner)
    {
        return _rootRotation.TryBegin(out owner);
    }

    public bool SubmitRootRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return _rootRotation.Submit(owner, localYawDelta);
    }

    public bool EndRootRotation(MotionOwner owner)
    {
        return _rootRotation.End(owner);
    }

    public bool BeginScriptedRotation(out MotionOwner owner)
    {
        return _scriptedRotation.TryBegin(out owner);
    }

    public bool SubmitScriptedRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return _scriptedRotation.Submit(owner, localYawDelta);
    }

    public bool EndScriptedRotation(MotionOwner owner)
    {
        return _scriptedRotation.End(owner);
    }

    #endregion

    #region === 内部工具 ===

    private bool ShouldApplyRootMotion =>
        _rootMotionApplyMode == RootMotionApplyMode.Managed && !_animatorRootMotionSuppressed;

    #endregion
}
