using System;
using UnityEngine;

/// <summary>
/// 角色移动业务层 — 负责意图处理、速度通道组合、朝向管线和 RootMotion 捕获。
///
/// 不再直接调用物理 API（CC.Move）。所有物理执行委托给 ActorMotor（ICharacterController 桥接层），
/// ActorMotor 在 KCC 的 FixedUpdate 回调中读取本文件的 MovementState 并写入 KCC BaseVelocity / Rotation。
///
/// 职责分离：
///   - Update(): 朝向管线、Locomotion 换算、RootMotion 累积、调试速度记录
///   - KCC 回调（via ActorMotor）: 通道演化、速度组合、地面状态桥接、帧末重置
///
/// 会原样保留的（业务层职责）：
///   - LocomotionIntent / SetLocomotionIntent / SetLocomotionSuppressed
///   - 朝向三层管线（Override / Intent / Snap）
///   - RootMotion 的 Managed / External 模式
///   - AddHorizontalImpulse / AddVerticalImpulse + drag 衰减
///   - ActionMotionConfig 压制机制、gravityScale 覆盖
///   - MovementTimeScale（HitStop 等与 Timeline 同步）
/// </summary>
public class ActorMovement : MonoBehaviour
{
    private readonly MotionChannels _channels = new MotionChannels();

    #region === 序列化字段 ===

    public Actor actor;
    public Animator animator;

    [SerializeField, Tooltip("默认旋转速度（度/秒）")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Locomotion 基础移动速度（米/秒）")]
    private float _locomotionBaseSpeed = 5f;

    [SerializeField, Range(0f, 1f), Tooltip("空中 Locomotion 控制系数（0=完全失控，1=与地面一致）。只影响 Locomotion 通道，不影响 Velocity / Impulse。")]
    private float _airControlFactor = 0.4f;

    [SerializeField, Tooltip("水平冲量阻力系数（1/秒）。值越大衰减越快，5 ≈ 0.2 秒衰减到 37%。")]
    private float _horizontalDrag = 5f;

    [SerializeField, Tooltip("垂直冲量在空中的衰减系数（1/秒）。0=不衰减，2≈0.35 秒半衰期，5≈0.14 秒半衰期。")]
    private float _verticalImpulseAirDrag = 0f;

    /// <summary>Locomotion 基础移动速度，供 AnimancerBehaviour 读取以归一化 Mixer 参数。</summary>
    public float LocomotionBaseSpeed => _locomotionBaseSpeed;

    #endregion

    #region === RootMotion 数据层 ===

    public enum RootMotionApplyMode
    {
        Managed,
        External
    }

    private RootMotionApplyMode _rootMotionApplyMode = RootMotionApplyMode.Managed;
    private Vector3 _pendingRootMotionPosition;
    private Quaternion _pendingRootMotionRotation = Quaternion.identity;

    public Vector3 RootMotionDelta => _pendingRootMotionPosition;
    public Quaternion RootMotionRotationDelta => _pendingRootMotionRotation;

    public void SetRootMotionApplyMode(RootMotionApplyMode mode)
    {
        _rootMotionApplyMode = mode;
    }

    #endregion

    #region === 意图层 ===

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

    #region === 水平 / 垂直位移通道 ===

    [Header("Debug (ReadOnly)")]
    [SerializeField, Tooltip("当前帧实际移动速度（米/秒，世界空间）。运行时只读，仅调试观察用。")]
    private Vector3 _currentVelocity = Vector3.zero;

    [SerializeField, Tooltip("水平速度大小（米/秒）= √(vx²+vz²)。运行时只读，仅调试观察用。")]
    private float _currentHorizontalSpeed;

    [SerializeField, Tooltip("垂直速度（米/秒，正上负下）= vy。运行时只读，仅调试观察用。")]
    private float _currentVerticalSpeed;

    public Vector3 CurrentVelocity => _currentVelocity;
    public float CurrentHorizontalSpeed => _currentHorizontalSpeed;
    public float CurrentVerticalSpeed => _currentVerticalSpeed;

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

    #endregion

    #region === 垂直位移通道 ===

    private float _gravityScale = 1f;
    private float _movementTimeScale = 1f;

    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    public float MovementTimeScale => _movementTimeScale;

    public void SetMovementTimeScale(float scale)
    {
        _movementTimeScale = Mathf.Max(0f, scale);
    }

    public void AddVerticalImpulse(float upwardSpeed)
    {
        _channels.AddVerticalImpulse(upwardSpeed);

        if (upwardSpeed > 0f)
            _pendingForceUnground = true;
    }

    public MotionOwner BeginVerticalVelocity()
    {
        return _channels.BeginVerticalVelocity();
    }

    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        _channels.SetVerticalVelocity(owner, verticalSpeed);
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

    #region === 地面状态 ===

    public enum GroundState
    {
        Grounded,
        JustLeftGround,
        Airborne,
        JustLanded
    }

    [SerializeField, Tooltip("当前地面状态（运行时只读，调试观察用）")]
    private GroundState _groundState = GroundState.Grounded;

    public GroundState groundState => _groundState;
    public bool IsGrounded => _groundState == GroundState.Grounded || _groundState == GroundState.JustLanded;
    public bool IsAirborne => !IsGrounded;

    public event Action OnLanded;
    public event Action OnLeftGround;

    /// <summary>由 ActorMotor.PostGroundingUpdate 调用，桥接 KCC 地面状态。</summary>
    internal void ApplyGroundingUpdate(bool isStableNow, bool wasStable)
    {
        // 过渡态 → 稳态
        if (_groundState == GroundState.JustLanded)
            _groundState = GroundState.Grounded;
        else if (_groundState == GroundState.JustLeftGround)
            _groundState = GroundState.Airborne;

        if (isStableNow && !wasStable)
        {
            _groundState = GroundState.JustLanded;
            jumpCount = 0;
            OnLanded?.Invoke();
        }
        else if (!isStableNow && wasStable)
        {
            _groundState = GroundState.JustLeftGround;
            OnLeftGround?.Invoke();
        }
    }

    #endregion

    #region === 运动能力状态 ===

    [Header("Jump Ability")]
    [SerializeField, Tooltip("最大跳跃次数。2 = 支持二段跳。")]
    private int _maxJumpCount = 2;

    public int jumpCount { get; private set; }
    public int maxJumpCount => _maxJumpCount;
    public bool CanJump() => jumpCount < _maxJumpCount;
    public void ConsumeJump() => jumpCount++;

    #endregion

    #region === 朝向管线 ===

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

    /// <summary>由 ActorMotor.UpdateRotation 调用，读取当前帧的期望旋转。</summary>
    internal Quaternion ConsumePendingRotation()
    {
        return _pendingRotation;
    }

    /// <summary>由 ActorMotor.UpdateRotation 调用，读取累积的 RootMotion 旋转增量。</summary>
    internal Quaternion ConsumePendingRootRotation()
    {
        return _pendingRootMotionRotation;
    }

    #endregion

    #region === MovementState（供 ActorMotor 读取） ===

    public struct MovementState
    {
        public Vector3 HorizontalVelocity;
        public float VerticalVelocity;
        public Vector3 RootMotionDelta;
        public bool IsRootMotionManaged;
        public bool ShouldForceUnground;
    }

    internal MovementState GetMovementState()
    {
        return new MovementState
        {
            HorizontalVelocity = _channels.ComposeHorizontal(ComputeLocomotionVelocity()),
            VerticalVelocity = _channels.ComposeVertical(),
            RootMotionDelta = _pendingRootMotionPosition,
            IsRootMotionManaged = _rootMotionApplyMode == RootMotionApplyMode.Managed,
            ShouldForceUnground = _pendingForceUnground,
        };
    }

    /// <summary>由 ActorMotor.UpdateVelocity 调用，在 KCC 回调中以 FixedUpdate dt 演化通道。</summary>
    internal void StepChannels(float deltaTime, bool isGrounded, bool isAirborne)
    {
        float dt = deltaTime * _movementTimeScale;
        if (dt <= 0f) return;

        _channels.StepGravity(dt, isGrounded, _gravityScale);
        _channels.StepHorizontalDrag(dt, _horizontalDrag);
        _channels.StepVerticalImpulseDecay(dt, isAirborne, isGrounded, _pendingCeilingHit, _verticalImpulseAirDrag);
        _pendingCeilingHit = false;
    }

    /// <summary>由 ActorMotor.AfterCharacterUpdate 调用，重置每 tick 累积的状态。</summary>
    internal void SignalMotorFrameEnd()
    {
        _pendingRootMotionPosition = Vector3.zero;
        _pendingRootMotionRotation = Quaternion.identity;
        _pendingForceUnground = false;
    }

    /// <summary>由 ActorMotor.OnMovementHit 调用，通知天花板碰撞。</summary>
    internal void SignalCeilingHit()
    {
        _pendingCeilingHit = true;
    }

    #endregion

    #region === 内部状态 ===

    private bool _pendingForceUnground;
    private bool _pendingCeilingHit;

    // Y 轴死区逻辑参数
    private float _yPositionDeadZone = 0.5f;
    private float _accumulatedYDelta;

    #endregion

    #region === 生命周期 ===

    private void Start()
    {
        _targetRotation = transform.rotation;
        _pendingRotation = transform.rotation;
    }

    private void OnAnimatorMove()
    {
        if (animator == null) return;

        _pendingRootMotionPosition += ProcessRootMotionDeadZone(animator.deltaPosition);
        _pendingRootMotionRotation = animator.deltaRotation * _pendingRootMotionRotation;
    }

    private void Update()
    {
        float dt = Time.deltaTime * _movementTimeScale;

        // 1. 朝向管线
        PerformRotation(dt);

        // 2. 记录速度（供动画系统 / 调试读取）
        Vector3 horizontal = _channels.ComposeHorizontal(ComputeLocomotionVelocity());
        float vertical = _channels.ComposeVertical();
        _currentVelocity = horizontal + Vector3.up * vertical;
        _currentHorizontalSpeed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude;
        _currentVerticalSpeed = _currentVelocity.y;

        // 3. 帧末清理
        _hasLocomotionIntent = false;
    }

    #endregion

    #region === 内部计算 ===

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

    private Vector3 ProcessRootMotionDeadZone(Vector3 rawDelta)
    {
        float currentYDelta = rawDelta.y;
        if (Mathf.Abs(currentYDelta) < _yPositionDeadZone)
        {
            _accumulatedYDelta += currentYDelta;
            if (Mathf.Abs(_accumulatedYDelta) >= _yPositionDeadZone)
            {
                rawDelta.y = _accumulatedYDelta;
                _accumulatedYDelta = 0;
            }
            else
            {
                rawDelta.y = 0;
            }
        }
        else
        {
            _accumulatedYDelta = 0;
        }
        return rawDelta;
    }

    #endregion
}
