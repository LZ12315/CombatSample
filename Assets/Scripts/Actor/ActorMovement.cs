using System;
using UnityEngine;

/// <summary>
/// Character movement facade — keeps serialized config, locomotion intent,
/// and facing pipeline. Runtime state is delegated to ActorMotor.MotionRuntime
/// via BindMotor / ResolveMotor.
///
/// External callers continue to use actor.movement.* unchanged.
/// </summary>
public class ActorMovement : MonoBehaviour
{
    #region === Serialized Config (stays on ActorMovement) ===

    public Actor actor;
    public Animator animator;

    [SerializeField, Tooltip("Default rotation speed (degrees/sec).")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Locomotion base speed (m/s).")]
    private float _locomotionBaseSpeed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("Air control factor (0=no control, 1=same as ground). Affects locomotion channel only.")]
    private float _airControlFactor = 0.4f;

    [SerializeField, Tooltip("Horizontal impulse drag coefficient (1/s). Higher = faster decay.")]
    private float _horizontalDrag = 5f;

    [SerializeField, Tooltip("Vertical impulse air drag (1/s). 0=no decay, 5≈0.14s half-life.")]
    private float _verticalImpulseAirDrag;

    [SerializeField, Range(0.01f, 0.5f), Tooltip("Vertical smoothing time on landing. Smaller = faster snap to 0.")]
    private float _verticalSmoothTime = 0.1f;

    [SerializeField, Tooltip("Y-axis dead zone for root motion delta accumulation.")]
    private float _rootMotionYDeadZone = 0.5f;

    [Header("Jump Ability")]
    [SerializeField, Tooltip("Max jump count. 2 = double jump.")]
    private int _maxJumpCount = 2;

    /// <summary>Locomotion base speed for AnimancerBehaviour mixer normalisation.</summary>
    public float LocomotionBaseSpeed => _locomotionBaseSpeed;

    public int maxJumpCount => _maxJumpCount;

    #endregion

    #region === Runtime Config Snapshot ===

    public ActorMotionRuntimeConfig GetRuntimeConfig()
    {
        return new ActorMotionRuntimeConfig(
            _horizontalDrag,
            _verticalImpulseAirDrag,
            _verticalSmoothTime,
            _rootMotionYDeadZone);
    }

    #endregion

    #region === Motor Binding ===

    private ActorMotor _boundMotor;

    // Buffered event subscribers that arrived before BindMotor.
    private Action _bufferedOnLanded;
    private Action _bufferedOnLeftGround;

    // Buffered strategy state that arrived before BindMotor.
    private float? _bufferedGravityScale;
    private float? _bufferedMovementTimeScale;
    private RootMotionApplyMode? _bufferedRootMotionApplyMode;

    internal void BindMotor(ActorMotor motor)
    {
        _boundMotor = motor;
        ReplayBufferedState();
    }

    private void ReplayBufferedState()
    {
        if (_boundMotor == null) return;
        var runtime = _boundMotor.MotionRuntime;
        if (runtime == null) return;

        if (_bufferedOnLanded != null)
        {
            foreach (Delegate d in _bufferedOnLanded.GetInvocationList())
                runtime.OnLanded += (Action)d;
            _bufferedOnLanded = null;
        }

        if (_bufferedOnLeftGround != null)
        {
            foreach (Delegate d in _bufferedOnLeftGround.GetInvocationList())
                runtime.OnLeftGround += (Action)d;
            _bufferedOnLeftGround = null;
        }

        if (_bufferedGravityScale.HasValue)
        {
            runtime.SetGravityScale(_bufferedGravityScale.Value);
            _bufferedGravityScale = null;
        }

        if (_bufferedMovementTimeScale.HasValue)
        {
            runtime.SetMovementTimeScale(_bufferedMovementTimeScale.Value);
            _bufferedMovementTimeScale = null;
        }

        if (_bufferedRootMotionApplyMode.HasValue)
        {
            runtime.SetRootMotionApplyMode(_bufferedRootMotionApplyMode.Value);
            _bufferedRootMotionApplyMode = null;
        }
    }

    private ActorMotor ResolveMotor()
    {
        if (_boundMotor != null)
            return _boundMotor;

        _boundMotor = actor != null && actor.actorMotor != null
            ? actor.actorMotor
            : GetComponent<ActorMotor>();

        return _boundMotor;
    }

    /// <summary>
    /// Returns the resolved MotionRuntime, or null if either ActorMotor
    /// or its MotionRuntime is not yet available.
    /// </summary>
    private ActorMotionRuntime ResolveRuntime()
    {
        var motor = ResolveMotor();
        if (motor == null) return null;
        return motor.MotionRuntime;
    }

    #endregion

    #region === Locomotion Intent ===

    private LocomotionIntent _locomotionIntent = LocomotionIntent.Idle;
    private bool _hasLocomotionIntent;
    private bool _locomotionSuppressed;

    public LocomotionIntent LocomotionIntent => _locomotionIntent;

    public void SetLocomotionIntent(in LocomotionIntent intent)
    {
        _locomotionIntent = intent;
        _hasLocomotionIntent = true;
    }

    public void SetLocomotionSuppressed(bool suppressed)
    {
        _locomotionSuppressed = suppressed;
    }

    #endregion

    #region === RootMotion Data (facade) ===

    public enum RootMotionApplyMode
    {
        /// <summary>由 ActorMotionRuntime/KCC 应用动画根位移和根旋转。</summary>
        Managed,

        /// <summary>ActorMotionRuntime 不应用动画根位移和根旋转，由外部系统或程序运动负责。</summary>
        External
    }

    public void SetRootMotionApplyMode(RootMotionApplyMode mode)
    {
        var rt = ResolveRuntime();
        if (rt != null)
            rt.SetRootMotionApplyMode(mode);
        else
            _bufferedRootMotionApplyMode = mode;
    }

    #endregion

    #region === Impulse Channels (facade → runtime) ===

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        ResolveRuntime()?.AddHorizontalImpulse(velocity);
    }

    public void ClearHorizontalImpulse()
    {
        ResolveRuntime()?.ClearHorizontalImpulse();
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        return ResolveRuntime()?.BeginHorizontalVelocity() ?? default;
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        ResolveRuntime()?.SetHorizontalVelocity(owner, velocity);
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        ResolveRuntime()?.EndHorizontalVelocity(owner);
    }

    public void AddVerticalImpulse(float upwardSpeed)
    {
        ResolveRuntime()?.AddVerticalImpulse(upwardSpeed);
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return ResolveRuntime()?.BeginVerticalVelocity() ?? default;
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        ResolveRuntime()?.SetVerticalVelocity(owner, verticalSpeed);
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        ResolveRuntime()?.EndVerticalVelocity(owner);
    }

    public void ClearVelocityOwners()
    {
        ResolveRuntime()?.ClearVelocityOwners();
    }

    public void ApplyMotionHandoff(float horizontalInheritance, float verticalInheritance)
    {
        ResolveRuntime()?.ApplyMotionHandoff(horizontalInheritance, verticalInheritance);
    }

    #endregion

    #region === Gravity / Time Scale Strategy (facade → runtime) ===

    public void SetGravityScale(float scale)
    {
        var rt = ResolveRuntime();
        if (rt != null)
            rt.SetGravityScale(scale);
        else
            _bufferedGravityScale = scale;
    }

    public void SetMovementTimeScale(float scale)
    {
        scale = Mathf.Max(0f, scale);
        var rt = ResolveRuntime();
        if (rt != null)
            rt.SetMovementTimeScale(scale);
        else
            _bufferedMovementTimeScale = scale;
    }

    public float MovementTimeScale
    {
        get
        {
            if (_bufferedMovementTimeScale.HasValue)
                return _bufferedMovementTimeScale.Value;
            return ResolveRuntime()?.MovementTimeScale ?? 1f;
        }
    }

    #endregion

    #region === Velocity Readout (facade → runtime) ===

    public Vector3 CurrentVelocity =>
        ResolveRuntime()?.CurrentVelocity ?? Vector3.zero;

    public float CurrentHorizontalSpeed =>
        ResolveRuntime()?.CurrentHorizontalSpeed ?? 0f;

    public float CurrentVerticalSpeed =>
        ResolveRuntime()?.CurrentVerticalSpeed ?? 0f;

    #endregion

    #region === Ground State (facade → runtime) ===

    public enum GroundState
    {
        Grounded,
        JustLeftGround,
        Airborne,
        JustLanded
    }

    public GroundState groundState =>
        ResolveRuntime()?.GroundState ?? GroundState.Grounded;

    public bool IsGrounded
    {
        get
        {
            var state = ResolveRuntime()?.GroundState ?? GroundState.Grounded;
            return state == GroundState.Grounded || state == GroundState.JustLanded;
        }
    }

    public bool IsAirborne => !IsGrounded;

    public event Action OnLanded
    {
        add
        {
            var runtime = ResolveRuntime();
            if (runtime != null)
                runtime.OnLanded += value;
            else
                _bufferedOnLanded += value;
        }
        remove
        {
            var runtime = ResolveRuntime();
            if (runtime != null)
                runtime.OnLanded -= value;
            else
                _bufferedOnLanded -= value;
        }
    }

    public event Action OnLeftGround
    {
        add
        {
            var runtime = ResolveRuntime();
            if (runtime != null)
                runtime.OnLeftGround += value;
            else
                _bufferedOnLeftGround += value;
        }
        remove
        {
            var runtime = ResolveRuntime();
            if (runtime != null)
                runtime.OnLeftGround -= value;
            else
                _bufferedOnLeftGround -= value;
        }
    }

    #endregion

    #region === Jump State (facade → runtime) ===

    public int jumpCount =>
        ResolveRuntime()?.JumpCount ?? 0;

    public bool CanJump()
    {
        var runtime = ResolveRuntime();
        return runtime != null && runtime.CanJump(_maxJumpCount);
    }

    public void ConsumeJump()
    {
        ResolveRuntime()?.ConsumeJump();
    }

    #endregion

    #region === Facing Pipeline (stays on ActorMovement for phase 1) ===

    private Vector3 _overrideFacingDirection;
    private bool _hasFacingOverride;
    private float _overrideAngularSpeed = -1f;

    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _pendingRotation = Quaternion.identity;

    public void SetFacingOverride(Vector3 worldDirection, float angularSpeed = -1f)
    {
        if (worldDirection.sqrMagnitude < 0.001f) return;
        _overrideFacingDirection = worldDirection;
        _hasFacingOverride = true;
        _overrideAngularSpeed = angularSpeed;
    }

    public void ClearFacingOverride()
    {
        _hasFacingOverride = false;
        _overrideAngularSpeed = -1f;
    }

    public void SnapFacing(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.001f) return;
        _targetRotation = Quaternion.LookRotation(worldDirection, Vector3.up);
        _pendingRotation = _targetRotation;
    }

    internal Quaternion GetPendingRotation()
    {
        return _pendingRotation;
    }

    #endregion

    #region === Lifecycle ===

    private void Start()
    {
        _targetRotation = transform.rotation;
        _pendingRotation = transform.rotation;
    }

    private void OnAnimatorMove()
    {
        if (animator == null) return;

        var runtime = ResolveRuntime();
        if (runtime == null) return;

        runtime.AddAnimatorDelta(
            animator.deltaPosition,
            animator.deltaRotation,
            GetRuntimeConfig().RootMotionYDeadZone);
    }

    private void Update()
    {
        float dt = Time.deltaTime * MovementTimeScale;

        PerformRotation(dt);
        _cachedLocomotionVelocity = ComputeLocomotionVelocity();
        _hasLocomotionIntent = false;
    }

    #endregion

    #region === Internal Computation ===

    private Vector3 _cachedLocomotionVelocity;

    internal Vector3 GetCachedLocomotionVelocity()
    {
        return _cachedLocomotionVelocity;
    }

    private Vector3 ComputeLocomotionVelocity()
    {
        if (_locomotionSuppressed || !_hasLocomotionIntent)
            return Vector3.zero;

        Vector3 dir = _locomotionIntent.WorldMoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        dir.Normalize();
        float speed = _locomotionIntent.MoveStrength * _locomotionBaseSpeed;
        if (IsAirborne) speed *= _airControlFactor;
        return dir * speed;
    }

    private void PerformRotation(float dt)
    {
        if (_hasFacingOverride)
        {
            _targetRotation = Quaternion.LookRotation(_overrideFacingDirection, Vector3.up);
        }
        else if (!_locomotionSuppressed && _hasLocomotionIntent)
        {
            Vector3 face = _locomotionIntent.FacingDirection;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f)
            {
                face = _locomotionIntent.WorldMoveDirection;
                face.y = 0f;
            }
            if (face.sqrMagnitude > 0.0001f)
                _targetRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }

        float angularSpeed = (_hasFacingOverride && _overrideAngularSpeed >= 0f)
            ? _overrideAngularSpeed
            : rotateSpeed;

        _pendingRotation = Quaternion.RotateTowards(
            _pendingRotation,
            _targetRotation,
            angularSpeed * dt
        );
    }

    #endregion
}
