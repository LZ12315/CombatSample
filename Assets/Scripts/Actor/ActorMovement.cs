using System;
using UnityEngine;

/// <summary>
/// 角色移动执行层 — 负责组装运动学目标并交给底层 Motor。
/// 
/// 意图层：接收 LocomotionIntent（由 ActorLogicInput / AI 每帧写入），内部换算速度和朝向。
/// 位移管线：
///   - 无 Velocity owner 时：水平 = Locomotion + HorizontalImpulse；垂直 = Gravity + VerticalImpulse
///   - 有 Velocity owner 时：对应轴由 VelocityClip 覆盖，未声明的轴不受影响
///   - Action 入场会先清理 Velocity owner，owner 不跨 Action 继承
/// 朝向管线：优先级覆盖模型（Override 层 > Intent 默认层 + Snap 机制）。
/// 压制机制：ActionInstance 可压制 Locomotion 通道（移动 + 朝向），Impulse/重力不受影响。
/// RootMotion：始终缓存并公开（位移+旋转），Managed 时自动应用，External 时仅供读取。
/// 地面状态：维护 GroundState 状态机 + OnLanded/OnLeftGround 事件，供跳跃等能力订阅。
///
/// ──────────────────────────────────────────────────────────────────────────
/// [过渡期实现 — 未来将迁移到 KCC（Kinematic Character Controller）]
/// 当前实现基于 Unity 内建 CharacterController，作为阶段性方案。架构整顿完成后，
/// 底层会整体切换到 KinematicCharacterMotor（KCC，已在 Assets/Plugins/KCC 就位）。
///
/// 迁移后的职责划分：
///   - ActorMotor : ICharacterController —— 回调层，在 UpdateVelocity / UpdateRotation
///     里把本文件的意图/通道翻译成 KCC 所需的 currentVelocity / currentRotation
///   - ActorMovement —— 纯业务层，保留 LocomotionIntent、朝向管线、ActionMotionConfig
///     压制机制、Clip 的 Impulse/Velocity 通道等高层抽象
///
/// 届时将废弃的实现（已被 KCC 原生能力替代，不要在当前文件过度优化）：
///   - _airborneFrameThreshold / _airborneFrameCounter 离地帧数滤波
///     → 被 KCC 的 GroundingStatus.IsStableOnGround 替代
///   - UpdateGroundState 状态机
///     → 被 PostGroundingUpdate + 对比 LastGroundingStatus 替代
///   - _gravityAccumulator = -2f 贴地力 hack
///     → 被 KCC 的 ground snapping + ForceUnground() 替代
///   - PerformGravity 里"有向上冲量时不贴地"的守卫
///     → 被 ForceUnground() 一步替代
///   - _verticalImpulseVelocity 独立通道（第一刀的产物）
///     → KCC 风格下不再需要"通道拆分"，直接改 currentVelocity + ForceUnground
///   - 斜坡 / 台阶 / 悬崖 / 可动平台 / 低帧插值
///     → 全由 KCC 原生支持，请不要自行重造
///
/// 会原样保留 / 搬运的（业务层职责）：
///   - LocomotionIntent / SetLocomotionIntent / SetLocomotionSuppressed
///   - 朝向三层管线（Override / Intent / Snap）
///   - RootMotion 的 Managed / External 模式
///   - AddHorizontalImpulse / AddVerticalImpulse + drag 衰减
///   - ActionMotionConfig 压制机制、gravityScale 覆盖
///   - MovementTimeScale（HitStop 等与 Timeline 同步，演化用 scaled dt）
/// ──────────────────────────────────────────────────────────────────────────
/// </summary>
public class ActorMovement : MonoBehaviour
{
    private readonly MotionChannels _channels = new MotionChannels();
    private readonly GroundStateTracker _groundTracker = new GroundStateTracker();

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

    [SerializeField, Tooltip("离地帧数阈值，用于滤除斜坡/台阶抖动。超过此值才认为真正离地。")]
    private int _airborneFrameThreshold = 2;

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

    #region === 水平位移通道 ===

    [Header("Debug (ReadOnly)")]
    [SerializeField, Tooltip("当前帧实际移动速度（米/秒，世界空间）。运行时只读，仅调试观察用。")]
    private Vector3 _currentVelocity = Vector3.zero;

    [SerializeField, Tooltip("水平速度大小（米/秒）= √(vx²+vz²)。运行时只读，仅调试观察用。")]
    private float _currentHorizontalSpeed;

    [SerializeField, Tooltip("垂直速度（米/秒，正上负下）= vy。运行时只读，仅调试观察用。")]
    private float _currentVerticalSpeed;

    /// <summary>当前帧的实际移动速度（米/秒）。供 AnimancerBehaviour 读取以驱动 Mixed2D 参数。</summary>
    public Vector3 CurrentVelocity => _currentVelocity;

    /// <summary>当前帧水平速度大小（米/秒）= √(vx²+vz²)。</summary>
    public float CurrentHorizontalSpeed => _currentHorizontalSpeed;

    /// <summary>当前帧垂直速度（米/秒，正上负下）= vy。</summary>
    public float CurrentVerticalSpeed => _currentVerticalSpeed;

    /// <summary>
    /// 注入水平冲量（世界空间，米/秒）。Y 分量会被忽略。
    /// 冲量由 MotionChannels 持有，之后每帧按 drag 指数衰减。
    /// 用于前刺、冲刺、击退等带惯性的水平位移。
    /// </summary>
    public void AddHorizontalImpulse(Vector3 velocity)
    {
        _channels.AddHorizontalImpulse(velocity);
    }

    /// <summary>强制立即清零水平冲量（极少用，一般由 drag 自然衰减即可）。</summary>
    public void ClearHorizontalImpulse()
    {
        _channels.ClearHorizontalImpulse();
    }

    /// <summary>开始持续控制水平速度。返回的 owner 必须用于后续写入和释放。</summary>
    public MotionOwner BeginHorizontalVelocity()
    {
        return _channels.BeginHorizontalVelocity();
    }

    /// <summary>写入持续水平速度。只有当前 owner 能生效。</summary>
    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        _channels.SetHorizontalVelocity(owner, velocity);
    }

    /// <summary>释放持续水平速度。旧 owner 不能清掉新 owner 的状态。</summary>
    public void EndHorizontalVelocity(MotionOwner owner)
    {
        _channels.EndHorizontalVelocity(owner);
    }

    #endregion

    #region === 垂直位移通道 ===

    private float _gravityScale = 1f;

    /// <summary>HitStop 等与 Timeline 同步：演化位移/重力/冲量衰减用 Time.deltaTime * 此值。默认 1。</summary>
    private float _movementTimeScale = 1f;

    /// <summary>设置重力缩放。1.0=正常, 0=无重力(浮空), 2.0=加速下落。</summary>
    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    /// <summary>与 <see cref="ActionPlayer"/> 的 HitStop 速度对齐；0=本帧不演化运动学。</summary>
    public float MovementTimeScale => _movementTimeScale;

    public void SetMovementTimeScale(float scale)
    {
        _movementTimeScale = Mathf.Max(0f, scale);
    }

    /// <summary>
    /// 注入垂直冲量（米/秒，正值向上，负值向下）。
    /// 规则：
    ///   - 正值（跳跃/起飞类）：与当前冲量取最大值，避免二段跳被削弱
    ///   - 负值（下砸/击落类）：直接覆盖，后一次决定速度
    ///   - 注入向上冲量时顺手清空重力累积，让抛物线从干净状态开始
    /// 用于起跳、二段跳、击飞 Up 阶段等需要垂直初速度的场景。
    /// </summary>
    public void AddVerticalImpulse(float upwardSpeed)
    {
        _channels.AddVerticalImpulse(upwardSpeed);

        if (upwardSpeed > 0f)
            ForceUngroundForVerticalLaunch();
    }

    private void ForceUngroundForVerticalLaunch()
    {
        actor?.actorMotor?.ForceUnground(0.1f);

        bool leftGround = _groundTracker.ForceUnground(_airborneFrameThreshold + 1);
        _groundState = _groundTracker.State;

        if (leftGround)
            OnLeftGround?.Invoke();
    }

    /// <summary>开始持续控制垂直速度。返回的 owner 必须用于后续写入和释放。</summary>
    public MotionOwner BeginVerticalVelocity()
    {
        return _channels.BeginVerticalVelocity();
    }

    /// <summary>写入持续垂直速度。只有当前 owner 能生效。</summary>
    public void SetVerticalVelocity(MotionOwner owner, float verticalSpeed)
    {
        _channels.SetVerticalVelocity(owner, verticalSpeed);
    }

    /// <summary>释放持续垂直速度。旧 owner 不能清掉新 owner 的状态。</summary>
    public void EndVerticalVelocity(MotionOwner owner)
    {
        _channels.EndVerticalVelocity(owner);
    }

    /// <summary>
    /// 清理水平/垂直 Velocity owner 与对应缓存速度。
    /// 由 Action 入场调用，用于阻断旧 Action 的控制权残留。
    /// </summary>
    public void ClearVelocityOwners()
    {
        _channels.ClearVelocityOwners();
    }

    /// <summary>按比例继承旧动作遗留的水平/垂直动量。</summary>
    public void ApplyMotionHandoff(float horizontalInheritance, float verticalInheritance)
    {
        _channels.ApplyHandoff(horizontalInheritance, verticalInheritance);
    }

    #endregion

    #region === 地面状态 ===

    /// <summary>角色与地面的关系状态。</summary>
    public enum GroundState
    {
        /// <summary>稳定在地面上。</summary>
        Grounded,
        /// <summary>这一帧刚离开地面（下一帧会切到 Airborne）。</summary>
        JustLeftGround,
        /// <summary>在空中。</summary>
        Airborne,
        /// <summary>这一帧刚落地（下一帧会切到 Grounded）。</summary>
        JustLanded
    }

    [SerializeField, Tooltip("当前地面状态（运行时只读，调试观察用）")]
    private GroundState _groundState = GroundState.Grounded;

    /// <summary>当前地面状态。</summary>
    public GroundState groundState => _groundTracker.State;

    /// <summary>是否在地面（Grounded 或 JustLanded）。供条件系统判断。</summary>
    public bool IsGrounded => _groundTracker.IsGrounded;

    /// <summary>是否在空中（Airborne 或 JustLeftGround）。</summary>
    public bool IsAirborne => _groundTracker.IsAirborne;

    /// <summary>落地事件：状态由 Airborne → JustLanded 时触发（每次落地触发一次）。</summary>
    public event Action OnLanded;

    /// <summary>离地事件：状态由 Grounded → JustLeftGround 时触发（每次离地触发一次）。</summary>
    public event Action OnLeftGround;

    // TODO：未来做 Coyote Time 时，在这里加 timeSinceGrounded / timeSinceAirborne 计时。
    //       Jump Buffer 已由输入层（InputSequenceCondition + ActorLogicInput）实现，Movement 不参与。

    #endregion

    #region === 运动能力状态 ===
    // 与角色运动能力相关的运行时计数/标记。
    // 原则：数据跟随信息专家（Movement 拥有 GroundState / 落地事件等完整运动上下文），
    //       行为在同一类内闭环（落地重置 jumpCount 无需跨组件事件订阅）。
    // 条件系统通过 actor.movement.CanJump() 访问，与 actor.movement.groundState 路径一致。

    [Header("Jump Ability")]
    [SerializeField, Tooltip("最大跳跃次数。2 = 支持二段跳。")]
    private int _maxJumpCount = 2;

    /// <summary>已消耗的跳跃次数。落地时自动重置为 0。</summary>
    public int jumpCount { get; private set; }

    /// <summary>最大跳跃次数（面板配置）。</summary>
    public int maxJumpCount => _maxJumpCount;

    /// <summary>是否还能跳（供条件系统查询）。</summary>
    public bool CanJump() => jumpCount < _maxJumpCount;

    /// <summary>消耗一次跳跃（由跳跃 Action 的 Claim 或 Timeline Clip 触发）。</summary>
    public void ConsumeJump() => jumpCount++;

    // 未来加 dash / airCombo 等同类运动能力状态时，也放在这个 region。

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
        if (animator == null) return;

        _cachedRootMotionDelta = ProcessRootMotionDeadZone(animator.deltaPosition);
        _cachedRootMotionRotationDelta = animator.deltaRotation;
    }

    /// <summary>
    /// Legacy Update entry kept intentionally light.
    /// KCC-timed motion is evaluated in BuildMotorVelocity/BuildMotorRotation and consumed by ActorMotor callbacks.
    /// </summary>
    private void Update() { }

    /// <summary>
    /// Builds desired motor velocity for this simulation step.
    /// Called from ActorMotor.UpdateVelocity in KCC update order.
    /// </summary>
    public Vector3 BuildMotorVelocity(float dt, bool isStableGrounded, bool hitCeiling)
    {
        float scaledDt = dt * _movementTimeScale;
        if (scaledDt <= 1e-8f)
            return Vector3.zero;

        UpdateGroundState(scaledDt, isStableGrounded);

        Vector3 locomotionVelocity = Vector3.zero;
        if (!_locomotionSuppressed && _hasLocomotionIntent)
        {
            Vector3 dir = _locomotionIntent.WorldMoveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                float speed = _locomotionIntent.MoveStrength * _locomotionBaseSpeed;
                if (IsAirborne) speed *= _airControlFactor;
                locomotionVelocity = dir * speed;
            }
        }

        _channels.StepGravity(scaledDt, IsGrounded, _gravityScale);
        _channels.StepHorizontalDrag(scaledDt, _horizontalDrag);
        _channels.StepVerticalImpulseDecay(scaledDt, IsAirborne, IsGrounded, hitCeiling, _verticalImpulseAirDrag);

        Vector3 desiredVelocity = Vector3.zero;
        if (_rootMotionApplyMode == RootMotionApplyMode.Managed)
            desiredVelocity += _cachedRootMotionDelta / scaledDt;

        Vector3 horizontal = _channels.ComposeHorizontal(locomotionVelocity);
        float vertical = _channels.ComposeVertical();
        desiredVelocity += horizontal + Vector3.up * vertical;

        return desiredVelocity;
    }

    /// <summary>
    /// Builds desired motor rotation for this simulation step.
    /// Called from ActorMotor.UpdateRotation in KCC update order.
    /// </summary>
    public Quaternion BuildMotorRotation(float dt, Quaternion currentRotation)
    {
        float scaledDt = dt * _movementTimeScale;
        if (scaledDt <= 1e-8f)
            return currentRotation;

        return PerformRotation(scaledDt, currentRotation);
    }

    /// <summary>
    /// Called after KCC simulation finishes this step.
    /// Records current velocity and clears per-frame caches consumed by motor callbacks.
    /// </summary>
    public void AfterMotorUpdate(Vector3 finalVelocity)
    {
        _currentVelocity = finalVelocity;
        _currentHorizontalSpeed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude;
        _currentVerticalSpeed = _currentVelocity.y;

        _cachedRootMotionDelta = Vector3.zero;
        _cachedRootMotionRotationDelta = Quaternion.identity;
        _hasLocomotionIntent = false;
    }

    #endregion

    #region === 内部计算 ===

    /// <summary>
    /// 更新地面状态机。每帧读取 KCC 的稳定 grounded 回传，经过轻量滤波，产生 4 种状态 + 落地/离地事件。
    /// </summary>
    private void UpdateGroundState(float dt, bool rawGrounded)
    {
        _groundTracker.Update(
            rawGrounded,
            dt,
            _airborneFrameThreshold,
            out bool landed,
            out bool leftGround);

        _groundState = _groundTracker.State;

        if (landed)
        {
            jumpCount = 0; // 落地重置跳跃计数（内部闭环，无需跨组件事件）
            OnLanded?.Invoke();
        }

        if (leftGround)
        {
            OnLeftGround?.Invoke();
        }
    }

    /// <summary>朝向管线：覆盖层 > Intent 默认层。被压制时不从 Intent 更新朝向。</summary>
    private Quaternion PerformRotation(float dt, Quaternion currentRotation)
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

        // 平滑旋转（最终旋转由 ActorMotor 在 KCC 回调中应用）
        _targetRotation = Quaternion.RotateTowards(
            currentRotation,
            _targetRotation,
            angularSpeed * dt
        );

        return _targetRotation;
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

    #endregion
}