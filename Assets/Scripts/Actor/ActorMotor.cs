using System;
using KinematicCharacterController;
using UnityEngine;

[RequireComponent(typeof(KinematicCharacterMotor))]
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    [SerializeField] private Actor actor;

    [SerializeField, Tooltip("Default locomotion turn speed in degrees per second.")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Compatibility default locomotion speed for actors without ActorLocomotion.")]
    private float _locomotionBaseSpeed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("Compatibility default air-control factor.")]
    private float _airControlFactor = 0.4f;

    [SerializeField, Tooltip("Horizontal impulse damping, in 1/second.")]
    private float _horizontalDrag = 5f;

    [SerializeField, Tooltip("Ballistic vertical velocity air drag, in 1/second.")]
    private float _verticalImpulseAirDrag;

    [SerializeField, Range(0.01f, 0.5f), Tooltip("Vertical velocity readout smoothing time on landing.")]
    private float _verticalSmoothTime = 0.1f;

    [Header("Jump")]
    [SerializeField, Tooltip("Maximum jump count. 2 means double jump.")]
    private int _maxJumpCount = 2;

    [SerializeField, Tooltip("Collision mask for KCC movement.")]
    private LayerMask _collisionMask = ~0;

    [Header("Actor Push")]
    [SerializeField, Range(0.1f, 100f)]
    private float _actorPushMass = 1f;

    [SerializeField]
    private bool _canBeActorPushed = true;

    private readonly LocomotionRunner _locomotion = new();
    private readonly TranslationDomain _translation = new();
    private readonly RotationDomain _rotation = new();
    private readonly MotionPolicyState _policy = new();
    private readonly GroundingRuntime _grounding = new();
    private readonly VelocityReadout _velocity = new();

    private LocomotionTuning _effectiveLocomotionTuning = LocomotionTuning.Default;
    private float _baseMovementTimeScale = 1f;
    private float _movementTimeScale = 1f;
    private readonly SpeedModifierStack _movementTimeScaleModifiers = new();

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
    private bool _pendingForceUnground;
    private bool _forceUngroundedThisTick;
    private bool _pendingCeilingHit;

    public KinematicCharacterMotor Motor { get; private set; }
    public CapsuleCollider Capsule { get; private set; }

    public float ActorPushMass => _actorPushMass;
    public bool CanBeActorPushed => _canBeActorPushed;
    public float LocomotionBaseSpeed => _effectiveLocomotionTuning.MoveSpeed;
    public int MaxJumpCount => _maxJumpCount;

    public LocomotionIntent LocomotionIntent => _locomotion.Intent;
    internal LocomotionIntent PendingLocomotionIntent => _locomotion.PendingIntent;
    internal bool HasPendingLocomotionIntent => _locomotion.HasPendingIntent;

    public Vector3 CurrentVelocity => _velocity.CurrentVelocity;
    public Vector3 ActualSolvedVelocity => _velocity.CurrentVelocity;
    public Vector3 RequestedVelocity => _requestedVelocity;
    public Quaternion RequestedRotation => _requestedRotation;
    public Vector3 ActualWorldPosition => _actualWorldPosition;
    public Quaternion ActualWorldRotation => _actualWorldRotation;
    public float CurrentHorizontalSpeed => _velocity.CurrentHorizontalSpeed;
    public float CurrentVerticalSpeed => _velocity.CurrentVerticalSpeed;
    public ActorGroundState GroundState => _grounding.State;
    public bool IsGrounded => GroundState is ActorGroundState.Grounded or ActorGroundState.JustLanded;
    public bool IsAirborne => !IsGrounded;
    public int JumpCount => _grounding.JumpCount;

    public float MovementTimeScale => _movementTimeScale;
    public float BaseMovementTimeScale => _baseMovementTimeScale;
    public float ExternalMovementTimeScale => _movementTimeScaleModifiers.Value;

    public TranslationDomain Translation => _translation;
    public RotationDomain Rotation => _rotation;
    public MotionPolicyState MotionPolicy => _policy;
    public float DebugBaseSpeed => _effectiveLocomotionTuning.MoveSpeed;
    public float DebugAirControlFactor => _effectiveLocomotionTuning.AirControlFactor;
    public float DebugRotateSpeed => _effectiveLocomotionTuning.RotateSpeed;
    public Vector3 DebugLocomotionVelocity => _locomotion.CachedVelocity;
    public float DebugLocomotionTargetYaw => _locomotion.TargetRotationYaw;

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

    public static ActorMotor GetActorMotor(Collider collider)
    {
        return collider != null ? collider.GetComponentInParent<ActorMotor>() : null;
    }

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

    public MotionOwner BeginTrajectoryRootMotion()
    {
        return _translation.BeginTrajectoryRootMotion();
    }

    public bool SubmitTrajectoryRootMotion(MotionOwner owner, Vector3 localPositionDelta)
    {
        localPositionDelta.y = 0f;
        return _translation.SubmitTrajectoryRootMotion(owner, localPositionDelta);
    }

    public void EndTrajectoryRootMotion(MotionOwner owner)
    {
        _translation.EndTrajectoryRootMotion(owner);
    }

    public bool BeginRootRotation(out MotionOwner owner)
    {
        return _rotation.BeginRootRotation(out owner);
    }

    public bool SubmitRootRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return _rotation.SubmitRootRotation(owner, localYawDelta);
    }

    public void EndRootRotation(MotionOwner owner)
    {
        if (_rotation.EndRootRotation(owner))
            SyncLocomotionRotationToCurrentPose();
    }

    public bool BeginScriptedRotation(out MotionOwner owner)
    {
        return _rotation.BeginScriptedRotation(out owner);
    }

    public bool SubmitScriptedRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return _rotation.SubmitScriptedRotation(owner, localYawDelta);
    }

    public void EndScriptedRotation(MotionOwner owner)
    {
        if (_rotation.EndScriptedRotation(owner))
            SyncLocomotionRotationToCurrentPose();
    }

    public bool TryBeginSelfRotation(out MotionOwner owner)
    {
        return BeginScriptedRotation(out owner);
    }

    public bool SubmitSelfRotation(MotionOwner owner, Quaternion localYawDelta)
    {
        return SubmitScriptedRotation(owner, localYawDelta);
    }

    public void EndSelfRotation(MotionOwner owner)
    {
        EndScriptedRotation(owner);
    }

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        _translation.AddHorizontalImpulse(ProjectPlanar(velocity));
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        return _translation.BeginHorizontalVelocity();
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        _translation.SetHorizontalVelocity(owner, ProjectPlanar(velocity));
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        _translation.EndHorizontalVelocity(owner);
    }

    public void AddVerticalImpulse(float upwardSpeed)
    {
        AddBallisticVerticalVelocity(upwardSpeed);
    }

    public void AddBallisticVerticalVelocity(float velocity)
    {
        _translation.AddBallisticVerticalVelocity(velocity);
        if (velocity > 0f)
            _pendingForceUnground = true;
    }

    public void SetBallisticVerticalVelocity(float velocity)
    {
        _translation.SetBallisticVerticalVelocity(velocity);
        if (velocity > 0f)
            _pendingForceUnground = true;
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return _translation.BeginVerticalVelocity();
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        if (!_translation.SetVerticalVelocity(owner, verticalSpeed))
            return;

        if (_translation.IsTopVerticalVelocityOwner(owner)
            && verticalSpeed > 0.001f
            && GroundState is ActorGroundState.Grounded or ActorGroundState.JustLanded)
        {
            _pendingForceUnground = true;
        }
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        _translation.EndVerticalVelocity(owner);
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

    public bool CanJump()
    {
        return _grounding.CanJump(_maxJumpCount);
    }

    public void ConsumeJump()
    {
        _grounding.ConsumeJump();
    }

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

        _locomotion.Initialize(transform.rotation);
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

        _forceUngroundedThisTick = false;
        _translation.BeginMotionTick();
        _rotation.BeginMotionTick();

        if (ConsumeForceUngroundRequest())
        {
            Motor.ForceUnground(0.1f);
            MarkForcedUngroundedThisTick();
        }

        bool grounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;
        float motionDeltaTime = Mathf.Max(0f, simulationDeltaTime) * MovementTimeScale;

        _locomotion.Prepare(
            motionDeltaTime,
            _effectiveLocomotionTuning.MoveSpeed,
            _effectiveLocomotionTuning.AirControlFactor,
            _effectiveLocomotionTuning.RotateSpeed,
            !grounded,
            _policy.LocomotionScale,
            _policy.AirLocomotionScale);

        _translation.StepHorizontalDrag(motionDeltaTime, _horizontalDrag);
        _translation.StepBallistic(
            motionDeltaTime,
            grounded,
            _pendingCeilingHit,
            _policy.GravityScale,
            _verticalImpulseAirDrag);
        _pendingCeilingHit = false;

        _rotation.Prepare(_motorFrameStartWorldRotation, _locomotion.PendingRotation);
        _requestedRotation = _rotation.RequestedRotation;

        if (!_kccPaused)
        {
            _requestedVelocity = ComposeKccVelocity(
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

        _motionPrepared = false;
        _motionFrameOpen = false;
        _preparedBySimulationRuntime = false;
        _requestedVelocity = Vector3.zero;
        _requestedRotation = Motor != null ? Motor.TransientRotation : transform.rotation;
        _kccPaused = false;
        _forceUngroundedThisTick = false;
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        if (!_motionPrepared)
            PrepareMotionInternal(deltaTime, false);
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        _grounding.ApplyKccGrounding(
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
        _solvedGrounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;
        _motionPrepared = false;
        PublishResolvedWorldPose();

        if (!_preparedBySimulationRuntime)
            PublishWorldResult();
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        ActorMotor otherMotor = GetActorMotor(coll);
        if (otherMotor != null && otherMotor != this)
            return false;

        return (_collisionMask & (1 << coll.gameObject.layer)) != 0;
    }

    public void OnGroundHit(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            _pendingCeilingHit = true;
    }

    public void ProcessHitStabilityReport(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        Vector3 atCharacterPosition,
        Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport)
    {
        ActorMotor otherMotor = GetActorMotor(hitCollider);
        if (otherMotor != null && otherMotor != this)
        {
            hitStabilityReport.IsStable = false;
            hitStabilityReport.ValidStepDetected = false;
            hitStabilityReport.LedgeDetected = false;
        }
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    public void PublishWorldResult()
    {
        if (!_motionFrameOpen)
            return;

        if (_motionPrepared)
        {
            _solvedGrounded = Motor != null
                              && Motor.GroundingStatus.IsStableOnGround
                              && !_forceUngroundedThisTick;
            _motionPrepared = false;
        }

        Vector3 solvedVelocity = ComputeSolvedVelocity(_simulationDeltaTime);
        _velocity.Publish(
            solvedVelocity,
            Motor != null ? Motor.CharacterUp : Vector3.up,
            _solvedGrounded,
            Mathf.Max(0f, _simulationDeltaTime),
            _verticalSmoothTime);

        PublishResolvedWorldPose();
        _motionFrameOpen = false;
        _preparedBySimulationRuntime = false;
        _kccPaused = false;
        _forceUngroundedThisTick = false;
    }

    internal void PublishResolvedWorldPose()
    {
        _actualWorldPosition = Motor != null ? Motor.TransientPosition : transform.position;
        _actualWorldRotation = Motor != null ? Motor.TransientRotation : transform.rotation;
    }

    private Vector3 ComposeKccVelocity(
        Vector3 locomotionVelocity,
        bool isGrounded,
        Quaternion tickStartRotation,
        float deltaTime)
    {
        if (deltaTime <= 0f)
            return Vector3.zero;

        float timeScale = _movementTimeScale;
        Vector3 characterUp = Motor != null ? Motor.CharacterUp : Vector3.up;
        Vector3 horizontal;
        if (_translation.TryComposeHorizontalVelocityOwner(timeScale, out horizontal))
        {
        }
        else if (_translation.HasTrajectoryRootMotionTick)
        {
            Vector3 localDelta = _translation.TrajectoryRootMotionLocalPosition;
            localDelta.y = 0f;
            horizontal = tickStartRotation * localDelta / deltaTime;
        }
        else
        {
            horizontal = _translation.ComposeHorizontal(locomotionVelocity, timeScale);
        }

        horizontal = Vector3.ProjectOnPlane(horizontal, characterUp);
        float vertical = _translation.ComposeVertical(timeScale);

        if (isGrounded && Motor != null)
        {
            horizontal = Motor.GetDirectionTangentToSurface(
                horizontal,
                Motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
            vertical = 0f;
        }
        else if (isGrounded)
        {
            vertical = 0f;
        }

        return horizontal + characterUp * vertical;
    }

    private bool ConsumeForceUngroundRequest()
    {
        bool result = _pendingForceUnground;
        _pendingForceUnground = false;
        return result;
    }

    private void MarkForcedUngroundedThisTick()
    {
        _forceUngroundedThisTick = true;
        _grounding.ForceUngroundNow();
    }

    private Vector3 ComputeSolvedVelocity(float deltaTime)
    {
        if (_kccPaused || deltaTime <= 0f)
            return Vector3.zero;

        return (Motor.TransientPosition - _motorFrameStartWorldPosition) / deltaTime;
    }

    private void RefreshMovementTimeScale()
    {
        _movementTimeScale = SanitizeMovementTimeScale(
            _baseMovementTimeScale * _movementTimeScaleModifiers.Value);
    }

    private void SyncLocomotionRotationToCurrentPose()
    {
        Quaternion rotation = Motor != null ? Motor.TransientRotation : transform.rotation;
        _locomotion.SyncRotation(rotation);
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
}
