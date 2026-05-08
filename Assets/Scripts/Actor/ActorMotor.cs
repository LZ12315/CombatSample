using System;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// 角色运动权威入口。
/// 负责实现 ICharacterController、持有 ActorMotionRuntime，
/// 并调度 locomotion、facing、root motion 与 KCC 速度解算。
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    #region === Inspector 配置 ===

    [SerializeField] private Actor actor;
    [SerializeField] private Animator animator;

    [SerializeField, Tooltip("默认转向速度（度/秒）。")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Locomotion 基础速度（米/秒）。")]
    private float _locomotionBaseSpeed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("空中控制倍率。0=无控制，1=与地面相同。只影响 Locomotion 通道。")]
    private float _airControlFactor = 0.4f;

    [SerializeField, Tooltip("水平冲量阻尼系数（1/秒）。越高衰减越快。")]
    private float _horizontalDrag = 5f;

    [SerializeField, Tooltip("垂直冲量空中阻尼（1/秒）。0=不衰减，5 约等于 0.14 秒半衰期。")]
    private float _verticalImpulseAirDrag;

    [SerializeField, Range(0.01f, 0.5f), Tooltip("落地时垂直速度读数的平滑时间。越小越快归零。")]
    private float _verticalSmoothTime = 0.1f;

    [SerializeField, Tooltip("RootMotion Y 轴位移死区，小幅抖动会先累计再释放。")]
    private float _rootMotionYDeadZone = 0.5f;

    [Header("跳跃能力")]
    [SerializeField, Tooltip("最大跳跃次数。2 = 二段跳。")]
    private int _maxJumpCount = 2;

    [SerializeField, Tooltip("碰撞过滤层。只有这些层上的 Collider 会参与角色碰撞。~0 = 全部。")]
    private LayerMask _collisionMask = ~0;

    #endregion

    #region === 运行时对象 ===

    private readonly LocomotionRuntime _locomotion = new();
    private readonly FacingRuntime _facing = new();

    public KinematicCharacterMotor Motor { get; private set; }
    public ActorMotionRuntime MotionRuntime { get; } = new();

    #endregion

    #region === 单帧桥接状态 ===

    private Vector3 _motorFrameStartWorldPosition;
    private Vector3 _requestedVelocity;
    private bool _hasRequestedVelocity;
    private bool _pausedThisTick;

    #endregion

    #region === 对外运动 API ===

    public float LocomotionBaseSpeed => _locomotionBaseSpeed;
    public int MaxJumpCount => _maxJumpCount;

    public LocomotionIntent LocomotionIntent => _locomotion.Intent;

    public void SetLocomotionIntent(in LocomotionIntent intent)
    {
        _locomotion.SetIntent(intent);
    }

    public void SetLocomotionSuppressed(bool suppressed)
    {
        _locomotion.SetSuppressed(suppressed);
    }

    public void SetFacingOverride(Vector3 worldDirection, float angularSpeed = -1f)
    {
        _facing.SetOverride(worldDirection, angularSpeed);
    }

    public void ClearFacingOverride()
    {
        _facing.ClearOverride();
    }

    public void SnapFacing(Vector3 worldDirection)
    {
        _facing.Snap(worldDirection);
    }

    public void SetRootMotionApplyMode(RootMotionApplyMode mode)
    {
        MotionRuntime.SetRootMotionApplyMode(mode);
    }

    public void AddAnimatorRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        MotionRuntime.AddAnimatorDelta(deltaPosition, deltaRotation, _rootMotionYDeadZone);
    }

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        MotionRuntime.AddHorizontalImpulse(velocity);
    }

    public void ClearHorizontalImpulse()
    {
        MotionRuntime.ClearHorizontalImpulse();
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        return MotionRuntime.BeginHorizontalVelocity();
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        MotionRuntime.SetHorizontalVelocity(owner, velocity);
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        MotionRuntime.EndHorizontalVelocity(owner);
    }

    public void AddVerticalImpulse(float upwardSpeed)
    {
        MotionRuntime.AddVerticalImpulse(upwardSpeed);
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return MotionRuntime.BeginVerticalVelocity();
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        MotionRuntime.SetVerticalVelocity(owner, verticalSpeed);
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        MotionRuntime.EndVerticalVelocity(owner);
    }

    public void ClearVelocityOwners()
    {
        MotionRuntime.ClearVelocityOwners();
    }

    public void ApplyMotionHandoff(float horizontalInheritance, float verticalInheritance)
    {
        MotionRuntime.ApplyMotionHandoff(horizontalInheritance, verticalInheritance);
    }

    public void SetGravityScale(float scale)
    {
        MotionRuntime.SetGravityScale(scale);
    }

    public void SetMovementTimeScale(float scale)
    {
        MotionRuntime.SetMovementTimeScale(scale);
    }

    public float MovementTimeScale => MotionRuntime.MovementTimeScale;

    public Vector3 CurrentVelocity => MotionRuntime.CurrentVelocity;
    public float CurrentHorizontalSpeed => MotionRuntime.CurrentHorizontalSpeed;
    public float CurrentVerticalSpeed => MotionRuntime.CurrentVerticalSpeed;

    public ActorGroundState GroundState => MotionRuntime.GroundState;

    public bool IsGrounded =>
        GroundState is ActorGroundState.Grounded or ActorGroundState.JustLanded;

    public bool IsAirborne => !IsGrounded;

    public event Action OnLanded
    {
        add => MotionRuntime.OnLanded += value;
        remove => MotionRuntime.OnLanded -= value;
    }

    public event Action OnLeftGround
    {
        add => MotionRuntime.OnLeftGround += value;
        remove => MotionRuntime.OnLeftGround -= value;
    }

    public int JumpCount => MotionRuntime.JumpCount;

    public bool CanJump()
    {
        return MotionRuntime.CanJump(_maxJumpCount);
    }

    public void ConsumeJump()
    {
        MotionRuntime.ConsumeJump();
    }

    #endregion

    #region === Unity 生命周期 ===

    private void Awake()
    {
        actor = actor != null ? actor : GetComponent<Actor>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();

        Motor = GetComponent<KinematicCharacterMotor>();
        Motor.CharacterController = this;

        if (actor != null)
        {
            actor.actorMotor = this;
            if (actor.kccMotor == null)
                actor.kccMotor = Motor;
        }

        _facing.Initialize(transform.rotation);
    }

    private void Update()
    {
        float dt = Time.deltaTime * MovementTimeScale;

        _facing.Tick(
            dt,
            rotateSpeed,
            _locomotion.Intent,
            _locomotion.HasIntent,
            _locomotion.IsSuppressed);

        _locomotion.Tick(
            _locomotionBaseSpeed,
            _airControlFactor,
            IsAirborne);
    }

    private void OnAnimatorMove()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            return;

        AddAnimatorRootMotionDelta(
            animator.deltaPosition,
            animator.deltaRotation);
    }

    #endregion

    #region === ICharacterController ===

    public void BeforeCharacterUpdate(float deltaTime)
    {
        _motorFrameStartWorldPosition = transform.position;
        _requestedVelocity = Vector3.zero;
        _hasRequestedVelocity = false;
        _pausedThisTick = false;

        MotionRuntime.BeginMotorTick();
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        MotionRuntime.ApplyKccGrounding(
            Motor.GroundingStatus.IsStableOnGround,
            Motor.LastGroundingStatus.IsStableOnGround);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        currentRotation = _facing.PendingRotation;

        var rootMotionRotation = MotionRuntime.AppliedRootMotionRotation;
        if (rootMotionRotation != Quaternion.identity)
            currentRotation = rootMotionRotation * currentRotation;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        float effDt = deltaTime * MotionRuntime.MovementTimeScale;
        if (effDt <= 0.0001f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        // 主动离地请求需要在 KCC 计算速度前消费。
        if (MotionRuntime.ConsumeForceUngroundRequest())
        {
            Motor.ForceUnground(0.1f);
            MotionRuntime.MarkForcedUngroundedThisTick();
        }

        // 本帧接地判断 = KCC 稳定接地状态 + 本地强制离地覆盖。
        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        ActorMotionRuntimeConfig config = GetRuntimeConfig();
        MotionRuntime.StepChannels(deltaTime, grounded, config);

        currentVelocity = MotionRuntime.ComposeKccVelocity(
            Motor,
            _locomotion.CachedVelocity,
            grounded,
            deltaTime);

        _requestedVelocity = currentVelocity;
        _hasRequestedVelocity = true;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        Vector3 solvedVelocity = ComputeSolvedVelocity(deltaTime);
        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        ActorMotionRuntimeConfig config = GetRuntimeConfig();
        MotionRuntime.PublishSolvedVelocity(
            solvedVelocity,
            grounded,
            Time.fixedDeltaTime,
            config.VerticalSmoothTime);

        MotionRuntime.EndMotorTick();
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return (_collisionMask & (1 << coll.gameObject.layer)) != 0;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            MotionRuntime.SignalCeilingHit();
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    #endregion

    #region === 内部工具 ===

    private ActorMotionRuntimeConfig GetRuntimeConfig()
    {
        return new ActorMotionRuntimeConfig(
            _horizontalDrag,
            _verticalImpulseAirDrag,
            _verticalSmoothTime,
            _rootMotionYDeadZone);
    }

    private Vector3 ComputeSolvedVelocity(float deltaTime)
    {
        if (_pausedThisTick || deltaTime <= 0f)
            return Vector3.zero;

        Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
        Vector3 finalVelocity = solvedDelta / deltaTime;

        if (finalVelocity.sqrMagnitude < 0.000001f && _hasRequestedVelocity)
            finalVelocity = Motor.BaseVelocity;

        return finalVelocity;
    }

    private void SetPausedVelocity(ref Vector3 currentVelocity)
    {
        currentVelocity = Vector3.zero;
        _requestedVelocity = Vector3.zero;
        _hasRequestedVelocity = true;
        _pausedThisTick = true;
    }

    #endregion
}
