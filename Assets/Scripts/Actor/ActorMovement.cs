using UnityEngine;

/// <summary>
/// 角色移动执行层 — 唯一调用 CC.Move 的地方。
/// 
/// 意图层：接收 LocomotionIntent（由 ActorLogicInput / AI 每帧写入），内部换算速度和朝向。
/// 位移管线：多源叠加模型（locomotion 内部换算 / impulse / rootMotion / 重力，求和执行）。
/// 朝向管线：优先级覆盖模型（Override 层 > Intent 默认层 + Snap 机制）。
/// 压制机制：ActionInstance 可压制 Locomotion 通道（移动 + 朝向），Impulse/重力不受影响。
/// RootMotion：始终缓存并公开（位移+旋转），Managed 时自动应用，External 时仅供读取。
/// </summary>
public class ActorMovement : MonoBehaviour
{
    #region === 序列化字段 ===

    public Actor actor;
    public Animator animator;

    [SerializeField, Tooltip("默认旋转速度（度/秒）")]
    private float rotateSpeed = 600f;

    [SerializeField, Tooltip("Locomotion 基础移动速度（米/秒）")]
    private float _locomotionBaseSpeed = 5f;

    /// <summary>Locomotion 基础移动速度，供 AnimancerBehaviour 读取以归一化 Mixer 参数。</summary>
    public float LocomotionBaseSpeed => _locomotionBaseSpeed;

    #endregion

    #region === RootMotion 数据层 ===

    /// <summary>RootMotion 是否由 Movement 自动应用到最终位移。</summary>
    public enum RootMotionApplyMode
    {
        /// <summary>Movement 自动将 RootMotionDelta 叠加到最终位移。适用于：攻击动作、受击动画、特殊演出。</summary>
        Managed,
        /// <summary>Movement 不自动应用 RootMotion，只缓存数据供外部读取。适用于：Locomotion 等外部控制场景。</summary>
        External
    }

    private RootMotionApplyMode _rootMotionApplyMode = RootMotionApplyMode.Managed;
    private Vector3 _cachedRootMotionDelta = Vector3.zero;
    private Quaternion _cachedRootMotionRotationDelta = Quaternion.identity;

    /// <summary>当前帧 RootMotion 位移增量（世界空间）。任何外部系统都可以读取。</summary>
    public Vector3 RootMotionDelta => _cachedRootMotionDelta;

    /// <summary>当前帧 RootMotion 旋转增量。任何外部系统都可以读取。</summary>
    public Quaternion RootMotionRotationDelta => _cachedRootMotionRotationDelta;

    /// <summary>设置 RootMotion 应用模式。Managed = 自动应用，External = 仅缓存供外部读取。</summary>
    public void SetRootMotionApplyMode(RootMotionApplyMode mode)
    {
        _rootMotionApplyMode = mode;
    }

    #endregion

    #region === 意图层 ===

    private LocomotionIntent _locomotionIntent = LocomotionIntent.Idle;
    private bool _hasLocomotionIntent;
    private bool _locomotionSuppressed;

    /// <summary>当前帧的 Locomotion 意图（只读）。供条件系统和 ASM 读取。</summary>
    public LocomotionIntent LocomotionIntent => _locomotionIntent;

    /// <summary>
    /// 每帧由 ActorLogicInput / AI 写入移动意图。
    /// Movement 内部根据意图换算 Locomotion 速度和朝向。
    /// </summary>
    public void SetLocomotionIntent(in LocomotionIntent intent)
    {
        _locomotionIntent = intent;
        _hasLocomotionIntent = true;
    }

    /// <summary>
    /// 压制 Locomotion 通道（移动 + 朝向）。
    /// 由 ActionInstance.OnEnter 设置 true，OnExit 设置 false。
    /// </summary>
    public void SetLocomotionSuppressed(bool suppressed)
    {
        _locomotionSuppressed = suppressed;
    }

    #endregion

    #region === 位移速度通道 ===

    private Vector3 _impulseVelocity = Vector3.zero;
    private Vector3 _currentVelocity = Vector3.zero;

    /// <summary>当前帧的实际移动速度（米/秒）。供 AnimancerBehaviour 读取以驱动 Mixed2D 参数。</summary>
    public Vector3 CurrentVelocity => _currentVelocity;

    /// <summary>ImpulseClip 每帧写入的冲量速度（世界空间，单位：米/秒）。帧末自动清零。</summary>
    public void SetImpulseVelocity(Vector3 velocity)
    {
        _impulseVelocity = velocity;
    }

    #endregion

    #region === 重力 ===

    private Vector3 _gravityVelocity = Vector3.zero;
    private float _gravityScale = 1f;

    /// <summary>设置重力缩放。1.0=正常, 0=无重力(浮空), 2.0=加速下落。</summary>
    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    /// <summary>
    /// 直接覆盖重力通道的垂直速度（一次性注入）。
    /// 注入后由重力系统自然累积衰减，形成物理正确的抛物线。
    /// 用于击飞/浮空等需要垂直初速度的场景。
    /// </summary>
    public void SetVerticalVelocity(float upSpeed)
    {
        _gravityVelocity = new Vector3(0f, upSpeed, 0f);
    }

    #endregion

    #region === 朝向管线 ===

    // 覆盖层（Clip 显式设置/清除，不自动清零）
    private Vector3 _overrideFacingDirection;
    private bool _hasFacingOverride;
    private float _overrideAngularSpeed = -1f; // -1 表示使用默认 rotateSpeed

    // 当前帧的目标旋转
    private Quaternion _targetRotation = Quaternion.identity;

    /// <summary>Clip 写入覆盖朝向（高优先级）。不会自动清零，需调用 ClearFacingOverride。</summary>
    /// <param name="angularSpeed">自定义旋转速度（度/秒），-1 表示使用默认 rotateSpeed。</param>
    public void SetFacingOverride(Vector3 worldDirection, float angularSpeed = -1f)
    {
        if (worldDirection.sqrMagnitude < 0.001f) return;
        _overrideFacingDirection = worldDirection;
        _hasFacingOverride = true;
        _overrideAngularSpeed = angularSpeed;
    }

    /// <summary>Clip 结束时释放覆盖层，回退到默认层。</summary>
    public void ClearFacingOverride()
    {
        _hasFacingOverride = false;
        _overrideAngularSpeed = -1f;
    }

    /// <summary>瞬间转向指定方向，无平滑过渡。适用于受击瞬间、攻击起手。</summary>
    public void SnapFacing(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.001f) return;
        _targetRotation = Quaternion.LookRotation(worldDirection, Vector3.up);
        transform.rotation = _targetRotation;
    }

    #endregion

    #region === 内部状态 ===

    // Y 轴死区逻辑参数
    private float _yPositionDeadZone = 0.5f;
    private float _accumulatedYDelta;

    #endregion

    #region === 生命周期 ===

    private void Start()
    {
        _targetRotation = transform.rotation;
    }

    /// <summary>抓取动画位移和旋转，缓存起来供 Update 使用。</summary>
    private void OnAnimatorMove()
    {
        if (actor.characterController == null) return;

        _cachedRootMotionDelta = ProcessRootMotionDeadZone(animator.deltaPosition);
        _cachedRootMotionRotationDelta = animator.deltaRotation;
    }

    /// <summary>统一计算与最终执行：朝向 → 位移合成 → CC.Move → 帧末清零。</summary>
    private void Update()
    {
        // ── 1. 朝向执行 ──
        PerformRotation();

        // ── 2. 从 Intent 换算 Locomotion 速度（内部计算，不再由外部写入）──
        Vector3 locomotionVelocity = Vector3.zero;
        if (!_locomotionSuppressed && _hasLocomotionIntent)
        {
            Vector3 dir = _locomotionIntent.WorldMoveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                locomotionVelocity = dir * (_locomotionIntent.MoveStrength * _locomotionBaseSpeed);
            }
        }

        // ── 3. 位移合成 ──
        Vector3 finalMovement = Vector3.zero;

        // RootMotion 托管层
        if (_rootMotionApplyMode == RootMotionApplyMode.Managed)
        {
            finalMovement += _cachedRootMotionDelta;
        }

        // 速度通道（叠加）
        finalMovement += locomotionVelocity * Time.deltaTime;   // 受压制控制
        finalMovement += _impulseVelocity * Time.deltaTime;     // 不受压制

        // 重力
        PerformGravity();
        finalMovement += _gravityVelocity * Time.deltaTime;

        // ── 4. 执行移动 ──
        if (actor.characterController != null)
        {
            actor.characterController.Move(finalMovement);
        }

        // ── 5. 记录实际速度（供动画系统读取）──
        _currentVelocity = Time.deltaTime > 0f ? finalMovement / Time.deltaTime : Vector3.zero;

        // ── 6. 帧末清零 ──
        _cachedRootMotionDelta = Vector3.zero;
        _cachedRootMotionRotationDelta = Quaternion.identity;
        _impulseVelocity = Vector3.zero;
        _hasLocomotionIntent = false; // 意图层每帧重置，要求外部每帧写入
        // 注意：_hasFacingOverride 和 _locomotionSuppressed 不清零，由外部显式控制
    }

    #endregion

    #region === 内部计算 ===

    /// <summary>朝向管线：覆盖层 > Intent 默认层。被压制时不从 Intent 更新朝向。</summary>
    private void PerformRotation()
    {
        if (_hasFacingOverride)
        {
            _targetRotation = Quaternion.LookRotation(_overrideFacingDirection, Vector3.up);
        }
        else if (!_locomotionSuppressed && _hasLocomotionIntent)
        {
            // 从 Intent 计算朝向
            Vector3 face = _locomotionIntent.FacingDirection;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f)
            {
                // 没有显式朝向 → 朝移动方向
                face = _locomotionIntent.WorldMoveDirection;
                face.y = 0f;
            }
            if (face.sqrMagnitude > 0.0001f)
                _targetRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }
        // 都没有 → 保持上一帧

        // 确定旋转速度：覆盖层有自定义速度用覆盖的，否则用默认
        float angularSpeed = (_hasFacingOverride && _overrideAngularSpeed >= 0f)
            ? _overrideAngularSpeed
            : rotateSpeed;

        // 平滑旋转
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            _targetRotation,
            angularSpeed * Time.deltaTime
        );
    }

    /// <summary>Y 轴死区处理，过滤 RootMotion 中微小的垂直抖动。</summary>
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

    /// <summary>重力计算，受 gravityScale 影响。</summary>
    private void PerformGravity()
    {
        if (actor.characterController.isGrounded)
        {
            _gravityVelocity = Vector3.down * 2f;
        }
        else
        {
            _gravityVelocity += Physics.gravity * (_gravityScale * Time.deltaTime);
        }
    }

    #endregion
}