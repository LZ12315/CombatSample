using System;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// 角色运动权威入口。
/// 持有 LocomotionRunner、Translation / Rotation Domain 与 supporting state，
/// 在 Motion Phase 产出 requested motion，并作为 KCC adapter 发布 world solve 结果。
/// </summary>
[RequireComponent(typeof(KinematicCharacterMotor))]
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    #region === Inspector 配置 ===

    [SerializeField] private Actor actor;

    [SerializeField, Tooltip("默认转向速度（度/秒）。")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Locomotion 基础速度（米/秒）。")]
    private float _locomotionBaseSpeed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("空中控制倍率。0=无控制，1=与地面相同。只影响 Locomotion 通道。")]
    private float _airControlFactor = 0.4f;

    [SerializeField, Tooltip("水平冲量阻尼系数（1/秒）。越高衰减越快。")]
    private float _horizontalDrag = 5f;

    [SerializeField, Tooltip("Ballistic 垂直速度的兼容空中阻尼（1/秒）。保留字段名以兼容现有序列化数据。")]
    private float _verticalImpulseAirDrag;

    [SerializeField, Range(0.01f, 0.5f), Tooltip("落地时垂直速度读数的平滑时间。越小越快归零。")]
    private float _verticalSmoothTime = 0.1f;

    [Header("跳跃能力")]
    [SerializeField, Tooltip("最大跳跃次数。2 = 二段跳。")]
    private int _maxJumpCount = 2;

    [SerializeField, Tooltip("碰撞过滤层。只有这些层上的 Collider 会参与角色碰撞。~0 = 全部。")]
    private LayerMask _collisionMask = ~0;

    [Header("Actor 互推")]
    [SerializeField, Range(0.1f, 100f), Tooltip("互推质量。值越大越难被挤动，值越小越容易被推开。默认 1。")]
    private float _actorPushMass = 1f;

    [SerializeField, Tooltip("是否可以参与 Actor 互推分离。关闭后该 Actor 不会被水平推开，但也不会让另一方穿模。")]
    private bool _canBeActorPushed = true;

    #endregion

    #region === 运行时对象 ===

    private readonly LocomotionRuntime _locomotion = new();
    private readonly FacingRuntime _facing = new();
    private readonly LocomotionRunner _locomotionRunner = new();
    private LocomotionTuning _effectiveLocomotionTuning = LocomotionTuning.Default;

    /// <summary>基础移动时间缩放。外部临时效果不直接写入 MotionRuntime，而是通过 modifier 叠加。</summary>
    private float _baseMovementTimeScale = 1f;
    private readonly SpeedModifierStack _movementTimeScaleModifiers = new();

    public KinematicCharacterMotor Motor { get; private set; }
    public ActorMotionRuntime MotionRuntime { get; } = new();
    public CapsuleCollider Capsule { get; private set; }

    // Actor collision properties
    public float ActorPushMass => _actorPushMass;
    public bool CanBeActorPushed => _canBeActorPushed;

    #endregion

    #region === Actor 识别 ===

    /// <summary>
    /// Returns the ActorMotor on the collider's root GameObject, or null if none.
    /// Uses GetComponentInParent to handle colliders on child GameObjects (e.g. hitboxes).
    /// </summary>
    public static ActorMotor GetActorMotor(Collider collider)
    {
        if (collider == null) return null;
        return collider.GetComponentInParent<ActorMotor>();
    }

    #endregion

    #region === 单帧桥接状态 ===

    private Vector3 _motorFrameStartWorldPosition;
    private Quaternion _motorFrameStartWorldRotation = Quaternion.identity;
    private Vector3 _requestedVelocity;
    private Quaternion _requestedRotation = Quaternion.identity;
    private Vector3 _actualWorldPosition;
    private Quaternion _actualWorldRotation = Quaternion.identity;
    private bool _motionPrepared;
    private bool _motionFrameOpen;
    private bool _preparedBySimulationRuntime;
    private bool _solvedGrounded;
    private float _simulationDeltaTime;
    private bool _kccPaused;

    #endregion

    #region === 对外运动 API ===

    public float LocomotionBaseSpeed => _effectiveLocomotionTuning.MoveSpeed;
    public int MaxJumpCount => _maxJumpCount;

    public LocomotionIntent LocomotionIntent => _locomotion.Intent;
    internal LocomotionIntent PendingLocomotionIntent => _locomotion.PendingIntent;
    internal bool HasPendingLocomotionIntent => _locomotion.HasPendingIntent;

    public void SetLocomotionIntent(in LocomotionIntent intent)
    {
        _locomotion.SetIntent(intent);
    }

    internal void ClearPendingLocomotionIntent()
    {
        _locomotion.ClearPendingIntent();
    }

    internal void ApplyLocomotionTuning(LocomotionTuning tuning)
    {
        _effectiveLocomotionTuning = LocomotionTuning.Sanitize(tuning);
    }

    internal void RestoreCompatibilityLocomotionTuning()
    {
        _effectiveLocomotionTuning = LocomotionTuning.Sanitize(new LocomotionTuning
        {
            MoveSpeed = _locomotionBaseSpeed,
            AirControlFactor = _airControlFactor,
            RotateSpeed = rotateSpeed,
        });
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
        MotionRuntime.AddAnimatorDelta(deltaPosition, deltaRotation);
    }

    public void SetAnimatorRootMotionSuppressed(bool suppressed)
    {
        MotionRuntime.SetAnimatorRootMotionSuppressed(suppressed);
    }

    public MotionOwner BeginTrajectoryRootMotion()
    {
        return MotionRuntime.BeginTrajectoryRootMotion();
    }

    public bool SubmitTrajectoryRootMotion(MotionOwner owner, Vector3 localPositionDelta)
    {
        return MotionRuntime.SubmitTrajectoryRootMotion(owner, localPositionDelta);
    }

    public void EndTrajectoryRootMotion(MotionOwner owner)
    {
        MotionRuntime.EndTrajectoryRootMotion(owner);
    }

    public bool TryBeginSelfRotation(out MotionOwner owner)
    {
        return MotionRuntime.TryBeginSelfRotation(out owner);
    }

    public bool SubmitSelfRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return MotionRuntime.SubmitSelfRotation(owner, localYawDelta);
    }

    public void EndSelfRotation(MotionOwner owner)
    {
        if (MotionRuntime.EndSelfRotation(owner))
            SyncFacingToCurrentRotation();
    }

    public bool BeginRootRotation(out MotionOwner owner)
    {
        return MotionRuntime.BeginRootRotation(out owner);
    }

    public bool SubmitRootRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return MotionRuntime.SubmitRootRotation(owner, localYawDelta);
    }

    public void EndRootRotation(MotionOwner owner)
    {
        if (MotionRuntime.EndRootRotation(owner))
            SyncFacingToCurrentRotation();
    }

    public bool BeginScriptedRotation(out MotionOwner owner)
    {
        return MotionRuntime.BeginScriptedRotation(out owner);
    }

    public bool SubmitScriptedRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return MotionRuntime.SubmitScriptedRotation(owner, localYawDelta);
    }

    public void EndScriptedRotation(MotionOwner owner)
    {
        if (MotionRuntime.EndScriptedRotation(owner))
            SyncFacingToCurrentRotation();
    }

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        MotionRuntime.AddHorizontalImpulse(ProjectPlanar(velocity));
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
        MotionRuntime.SetHorizontalVelocity(owner, ProjectPlanar(velocity));
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        MotionRuntime.EndHorizontalVelocity(owner);
    }

    public void AddVerticalImpulse(float upwardSpeed)
    {
        MotionRuntime.AddVerticalImpulse(upwardSpeed);
    }

    public void AddBallisticVerticalVelocity(float velocity)
    {
        MotionRuntime.AddBallisticVerticalVelocity(velocity);
    }

    public void SetBallisticVerticalVelocity(float velocity)
    {
        MotionRuntime.SetBallisticVerticalVelocity(velocity);
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

    public MotionOwner BeginLocomotionScale(float scale)
    {
        return MotionRuntime.BeginLocomotionScale(scale);
    }

    public bool UpdateLocomotionScale(MotionOwner owner, float scale)
    {
        return MotionRuntime.UpdateLocomotionScale(owner, scale);
    }

    public bool EndLocomotionScale(MotionOwner owner)
    {
        return MotionRuntime.EndLocomotionScale(owner);
    }

    public MotionOwner BeginAirLocomotionScale(float scale)
    {
        return MotionRuntime.BeginAirLocomotionScale(scale);
    }

    public bool UpdateAirLocomotionScale(MotionOwner owner, float scale)
    {
        return MotionRuntime.UpdateAirLocomotionScale(owner, scale);
    }

    public bool EndAirLocomotionScale(MotionOwner owner)
    {
        return MotionRuntime.EndAirLocomotionScale(owner);
    }

    public MotionOwner BeginGravityScale(float scale)
    {
        return MotionRuntime.BeginGravityScale(scale);
    }

    public bool UpdateGravityScale(MotionOwner owner, float scale)
    {
        return MotionRuntime.UpdateGravityScale(owner, scale);
    }

    public bool EndGravityScale(MotionOwner owner)
    {
        return MotionRuntime.EndGravityScale(owner);
    }

    /// <summary>
    /// 设置基础移动时间缩放。临时 SpeedVFX / HitStop 不应直接调用此方法。
    /// </summary>
    public void SetMovementTimeScale(float scale)
    {
        _baseMovementTimeScale = SanitizeMovementTimeScale(scale);
        RefreshMovementTimeScale();
    }

    public SpeedModifierToken AddMovementTimeScaleModifier(
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        SpeedModifierToken token = _movementTimeScaleModifiers.Add(scale, blendMode, debugName);
        RefreshMovementTimeScale();
        return token;
    }

    public bool UpdateMovementTimeScaleModifier(
        SpeedModifierToken token,
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        bool updated = _movementTimeScaleModifiers.Update(token, scale, blendMode, debugName);
        if (updated)
            RefreshMovementTimeScale();

        return updated;
    }

    public bool RemoveMovementTimeScaleModifier(SpeedModifierToken token)
    {
        bool removed = _movementTimeScaleModifiers.Remove(token);
        if (removed)
            RefreshMovementTimeScale();

        return removed;
    }

    public void ClearMovementTimeScaleModifiers()
    {
        if (_movementTimeScaleModifiers.Count == 0)
            return;

        _movementTimeScaleModifiers.Clear();
        RefreshMovementTimeScale();
    }

    public float MovementTimeScale => MotionRuntime.MovementTimeScale;
    public float BaseMovementTimeScale => _baseMovementTimeScale;
    public float ExternalMovementTimeScale => _movementTimeScaleModifiers.Value;

    public Vector3 CurrentVelocity => MotionRuntime.CurrentVelocity;
    public Vector3 ActualSolvedVelocity => MotionRuntime.CurrentVelocity;
    public Vector3 RequestedVelocity => _requestedVelocity;
    public Quaternion RequestedRotation => _requestedRotation;
    public Vector3 ActualWorldPosition => _actualWorldPosition;
    public Quaternion ActualWorldRotation => _actualWorldRotation;
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

    // Public debug access for Editor
    public LocomotionRuntime DebugLocomotion => _locomotion;
    public FacingRuntime DebugFacing => _facing;
    public MotionChannels DebugChannels => MotionRuntime.Channels;
    public TranslationDomain Translation => MotionRuntime.Translation;
    public RotationDomain Rotation => MotionRuntime.Rotation;
    public MotionPolicyState MotionPolicy => MotionRuntime.Policy;
    public float DebugBaseSpeed => _effectiveLocomotionTuning.MoveSpeed;
    public float DebugAirControlFactor => _effectiveLocomotionTuning.AirControlFactor;
    public float DebugRotateSpeed => _effectiveLocomotionTuning.RotateSpeed;

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

        Motor = GetComponent<KinematicCharacterMotor>();
        Capsule = GetComponent<CapsuleCollider>();
        if (Motor == null)
        {
            Debug.LogError($"[ActorMotor] Missing KinematicCharacterMotor on '{name}'.", this);
            return;
        }
        Motor.CharacterController = this;
        _requestedRotation = Motor.TransientRotation;
        PublishResolvedWorldPose();
        RestoreCompatibilityLocomotionTuning();

        if (actor != null)
            actor.actorMotor = this;

        _facing.Initialize(transform.rotation);
        RefreshMovementTimeScale();
    }

    private void OnEnable()
    {
        ActorCollisionResolver.Register(this);
    }

    private void OnDisable()
    {
        CancelPreparedMotion();
        ActorCollisionResolver.Unregister(this);
        ClearMovementTimeScaleModifiers();
    }

    #endregion

    #region === ICharacterController ===

    /// <summary>
    /// Motion Phase entry。所有 gameplay motion state evolution 与 compose 都在
    /// KCC World Phase 前完成；KCC callbacks 只消费这里准备好的请求。
    /// </summary>
    public void PrepareMotion(float simulationDeltaTime)
    {
        PrepareMotionInternal(simulationDeltaTime, true);
    }

    private void PrepareMotionInternal(float simulationDeltaTime, bool preparedBySimulationRuntime)
    {
        if (Motor == null)
            return;

        if (_motionFrameOpen)
            CancelPreparedMotion();

        _motorFrameStartWorldPosition = Motor.TransientPosition;
        _motorFrameStartWorldRotation = Motor.TransientRotation;
        _requestedVelocity = Vector3.zero;
        _requestedRotation = _motorFrameStartWorldRotation;
        _simulationDeltaTime = simulationDeltaTime;
        _preparedBySimulationRuntime = preparedBySimulationRuntime;
        _kccPaused = simulationDeltaTime <= 0f;

        MotionRuntime.BeginMotorTick();

        if (MotionRuntime.ConsumeForceUngroundRequest())
        {
            Motor.ForceUnground(0.1f);
            MotionRuntime.MarkForcedUngroundedThisTick();
        }

        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;
        float motionDeltaTime = Mathf.Max(0f, simulationDeltaTime) * MovementTimeScale;

        _locomotionRunner.Prepare(
            _locomotion,
            _facing,
            motionDeltaTime,
            _effectiveLocomotionTuning.MoveSpeed,
            _effectiveLocomotionTuning.AirControlFactor,
            _effectiveLocomotionTuning.RotateSpeed,
            !grounded,
            MotionRuntime.LocomotionScale,
            MotionRuntime.AirLocomotionScale);

        MotionRuntime.StepChannels(motionDeltaTime, grounded, GetRuntimeConfig());
        _requestedRotation = MotionRuntime.PrepareRequestedRotation(
            _motorFrameStartWorldRotation,
            _facing.PendingRotation);

        if (!_kccPaused)
        {
            _requestedVelocity = MotionRuntime.ComposeKccVelocity(
                Motor,
                _locomotion.CachedVelocity,
                grounded,
                _motorFrameStartWorldRotation,
                simulationDeltaTime);
        }

        _motionPrepared = true;
        _motionFrameOpen = true;
    }

    public void CancelPreparedMotion()
    {
        if (!_motionFrameOpen)
            return;

        if (_motionPrepared)
            MotionRuntime.EndMotorTick();

        _motionPrepared = false;
        _motionFrameOpen = false;
        _preparedBySimulationRuntime = false;
        _requestedVelocity = Vector3.zero;
        _requestedRotation = Motor != null ? Motor.TransientRotation : transform.rotation;
        _kccPaused = false;
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // Compatibility path for isolated KCC usage. In the combat runtime the
        // ActorSimulationRuntime Motion Phase always prepares before World.
        if (!_motionPrepared)
            PrepareMotionInternal(deltaTime, false);
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        MotionRuntime.ApplyKccGrounding(
            Motor.GroundingStatus.IsStableOnGround,
            Motor.LastGroundingStatus.IsStableOnGround);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        currentRotation = _requestedRotation;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        currentVelocity = _kccPaused ? Vector3.zero : _requestedVelocity;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        _solvedGrounded = Motor.GroundingStatus.IsStableOnGround &&
                          !MotionRuntime.ForceUngroundedThisTick;

        MotionRuntime.EndMotorTick();
        _motionPrepared = false;
        PublishResolvedWorldPose();

        if (!_preparedBySimulationRuntime)
            PublishWorldResult();
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        // Filter out other KCC-driven actors so they don't block each other
        // as if they were solid walls. Actor-actor separation is handled by
        // ActorCollisionResolver after all motors tick.
        ActorMotor otherMotor = GetActorMotor(coll);
        if (otherMotor != null && otherMotor != this)
            return false;

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
        ref HitStabilityReport hitStabilityReport)
    {
        // Actor-on-actor: prevent treating another actor as stable ground or valid step.
        // This stops characters from standing on each other's heads.
        ActorMotor otherMotor = GetActorMotor(hitCollider);
        if (otherMotor != null && otherMotor != this)
        {
            hitStabilityReport.IsStable = false;
            hitStabilityReport.ValidStepDetected = false;
            hitStabilityReport.LedgeDetected = false;
        }
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    /// <summary>
    /// World Phase result entry。此时 KCC 与 ActorCollisionResolver 均已结束，
    /// 因而发布的是最终 world result，而不是 compose request。
    /// </summary>
    public void PublishWorldResult()
    {
        if (!_motionFrameOpen)
            return;

        if (_motionPrepared)
        {
            _solvedGrounded = Motor != null && Motor.GroundingStatus.IsStableOnGround &&
                              !MotionRuntime.ForceUngroundedThisTick;
            MotionRuntime.EndMotorTick();
            _motionPrepared = false;
        }

        Vector3 solvedVelocity = ComputeSolvedVelocity(_simulationDeltaTime);
        ActorMotionRuntimeConfig config = GetRuntimeConfig();
        MotionRuntime.PublishSolvedVelocity(
            solvedVelocity,
            Motor != null ? Motor.CharacterUp : Vector3.up,
            _solvedGrounded,
            Mathf.Max(0f, _simulationDeltaTime),
            config.VerticalSmoothTime);

        PublishResolvedWorldPose();
        _motionFrameOpen = false;
        _preparedBySimulationRuntime = false;
        _kccPaused = false;
    }

    internal void PublishResolvedWorldPose()
    {
        _actualWorldPosition = Motor != null ? Motor.TransientPosition : transform.position;
        _actualWorldRotation = Motor != null ? Motor.TransientRotation : transform.rotation;
    }

    #endregion

    #region === 内部工具 ===

    private ActorMotionRuntimeConfig GetRuntimeConfig()
    {
        return new ActorMotionRuntimeConfig(
            _horizontalDrag,
            _verticalImpulseAirDrag,
            _verticalSmoothTime);
    }

    private Vector3 ComputeSolvedVelocity(float deltaTime)
    {
        if (_kccPaused || deltaTime <= 0f)
            return Vector3.zero;

        Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
        Vector3 finalVelocity = solvedDelta / deltaTime;

        return finalVelocity;
    }

    private void RefreshMovementTimeScale()
    {
        MotionRuntime.SetMovementTimeScale(_baseMovementTimeScale * _movementTimeScaleModifiers.Value);
    }

    private void SyncFacingToCurrentRotation()
    {
        Quaternion rotation = Motor != null ? Motor.TransientRotation : transform.rotation;
        _facing.SyncTo(rotation);
    }

    private Vector3 ProjectPlanar(Vector3 velocity)
    {
        Vector3 characterUp = Motor != null ? Motor.CharacterUp : Vector3.up;
        return Vector3.ProjectOnPlane(velocity, characterUp);
    }

    private static float SanitizeMovementTimeScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            return 1f;

        return Mathf.Max(0f, scale);
    }

#if UNITY_EDITOR
    public string GetMovementTimeScaleModifierDebugText()
    {
        return _movementTimeScaleModifiers.GetDebugText();
    }
#endif

    #endregion
}
