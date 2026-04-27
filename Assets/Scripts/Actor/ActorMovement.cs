using System;
using UnityEngine;

/// <summary>
/// 角色移动执行层 — 唯一调用 CC.Move 的地方。
/// 
/// 意图层：接收 LocomotionIntent（由 ActorLogicInput / AI 每帧写入），内部换算速度和朝向。
/// 位移管线（水平/垂直对称的"环境力 + 冲量 + 外部持续速度"三层结构）：
///   水平 = Locomotion（空中 × airControlFactor）+ HorizontalImpulse（drag 衰减）+ ExternalHorizontal（VelocityClip 每帧写入）
///   垂直 = GravityAccumulator（重力累积，≤0）+ VerticalImpulse（AddVerticalImpulse 注入）+ ExternalVertical（VelocityClip 每帧写入）
///       -> PostProcess（Override / Clamp）得到最终垂直速度
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
/// ──────────────────────────────────────────────────────────────────────────
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

    [SerializeField, Range(0f, 1f), Tooltip("空中 Locomotion 控制系数（0=完全失控，1=与地面一致）。只影响 Locomotion 通道，不影响 External / Impulse。")]
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

    /// <summary>
    /// 水平冲量速度（世界空间，米/秒）。
    /// 由 ImpulseClip 通过 AddHorizontalImpulse 一次性注入，每帧按 _horizontalDrag 指数衰减，不会帧末清零。
    /// TODO：未来手感调优时可考虑落地时临时提高衰减系数，让落地后的残余速度更快停下。
    /// </summary>
    private Vector3 _horizontalImpulseVelocity = Vector3.zero;

    /// <summary>
    /// 外部持续水平速度（世界空间，米/秒）。
    /// 由 VelocityClip 在 OnClipUpdate 每帧通过 SetExternalHorizontalVelocity 写入，
    /// OnClipStop 调用 ClearExternalVelocity 清零。与 Impulse 通道独立：Impulse 是一次性冲量+衰减，
    /// External 是段性持续覆盖。未写入时保持 0，不做任何自然衰减。
    /// </summary>
    private Vector3 _externalHorizontalVelocity = Vector3.zero;

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
    /// 冲量会累加到 _horizontalImpulseVelocity，之后每帧按 drag 指数衰减。
    /// 用于前刺、冲刺、击退等带惯性的水平位移。
    /// </summary>
    public void AddHorizontalImpulse(Vector3 velocity)
    {
        velocity.y = 0f;
        _horizontalImpulseVelocity += velocity;
    }

    /// <summary>强制立即清零水平冲量（极少用，一般由 drag 自然衰减即可）。</summary>
    public void ClearHorizontalImpulse()
    {
        _horizontalImpulseVelocity = Vector3.zero;
    }

    /// <summary>
    /// 写入外部持续水平速度（世界空间，米/秒）。Y 分量会被忽略。
    /// 约定由 VelocityClip 每帧调用；Clip 结束时务必调用 ClearExternalVelocity 归零。
    /// 与 Impulse 通道正交：Impulse 是带 drag 衰减的一次性冲量，External 是段性持续覆盖。
    /// </summary>
    public void SetExternalHorizontalVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        _externalHorizontalVelocity = velocity;
    }

    #endregion

    #region === 垂直位移通道 ===

    /// <summary>重力累积速度（只由 PerformGravity 演化，不承担冲量）。在地面=贴地力 -2，在空中从 0 按重力累积。值恒 ≤ 0。</summary>
    private float _gravityAccumulator = 0f;

    /// <summary>
    /// 垂直冲量速度（由 AddVerticalImpulse 注入，正值向上，负值向下）。
    /// 与 _gravityAccumulator 独立存在，合成时两者相加，形成抛物线。
    /// 由 PerformVerticalImpulseDecay 在着地 / 撞顶时清零，平时保留。
    /// </summary>
    private float _verticalImpulseVelocity = 0f;

    /// <summary>
    /// 外部持续垂直速度（米/秒，正值向上）。
    /// 由 VelocityClip 在 OnClipUpdate 每帧通过 SetExternalVerticalVelocity 写入，
    /// OnClipStop 调用 ClearExternalVelocity 清零。与 _gravityAccumulator / _verticalImpulseVelocity 独立。
    /// 
    /// 重要：VelocityClip 期间按约定会把 gravityScale 设为 0（浮空），由 Clip 完全接管垂直；
    ///       如果 Config 里把 gravityScale 设回 1，就是"持续速度叠加在重力之上"的语义。
    /// </summary>
    private float _externalVerticalVelocity = 0f;

    private float _gravityScale = 1f;
    private bool _hasVerticalVelocityOverride;
    private float _verticalVelocityOverride;
    private bool _hasVerticalClamp;
    private float _verticalClampMin;
    private float _verticalClampMax;

    /// <summary>设置重力缩放。1.0=正常, 0=无重力(浮空), 2.0=加速下落。</summary>
    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    /// <summary>当前重力缩放（只读）。Timeline Clip 等如需嵌套保存/恢复可读取。</summary>
    public float GravityScale => _gravityScale;

    /// <summary>
    /// 直接覆盖最终垂直速度（米/秒，正上负下）。
    /// </summary>
    public void SetVerticalVelocityOverride(float verticalSpeed)
    {
        _hasVerticalVelocityOverride = true;
        _verticalVelocityOverride = verticalSpeed;
    }

    /// <summary>
    /// 取消垂直速度覆盖，回到通道叠加（重力+冲量+外部速度）。
    /// </summary>
    public void ClearVerticalVelocityOverride()
    {
        _hasVerticalVelocityOverride = false;
        _verticalVelocityOverride = 0f;
    }

    /// <summary>
    /// 设置垂直速度钳制范围（作用于通道求和之后）。
    /// 例：(-0.5f, 0f) = 最多缓降 0.5m/s，且不允许上升。
    /// </summary>
    public void SetVerticalClamp(float min, float max)
    {
        _hasVerticalClamp = true;
        _verticalClampMin = min;
        _verticalClampMax = max;
    }

    /// <summary>
    /// 取消垂直速度钳制，回到通道叠加结果。
    /// </summary>
    public void ClearVerticalClamp()
    {
        _hasVerticalClamp = false;
        _verticalClampMin = 0f;
        _verticalClampMax = 0f;
    }

    /// <summary>
    /// 重置垂直通道到干净状态：冲量归零、重力累积归零。
    /// 语义：垂直方向的“权威期”结束，从零速开始由重力自然接管。
    /// 由 VelocityClip（releaseMode = ResetVertical）在 OnClipStop 时调用。
    /// </summary>
    public void ResetVerticalState()
    {
        _gravityAccumulator = 0f;
        _verticalImpulseVelocity = 0f;
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
        if (upwardSpeed >= 0f)
            _verticalImpulseVelocity = Mathf.Max(_verticalImpulseVelocity, upwardSpeed);
        else
            _verticalImpulseVelocity = upwardSpeed;

        if (upwardSpeed > 0f)
            _gravityAccumulator = 0f;
    }

    /// <summary>
    /// 写入外部持续垂直速度（米/秒，正值向上）。
    /// 约定由 VelocityClip 每帧调用；Clip 结束时务必调用 ClearExternalVelocity 归零。
    /// 与 Impulse / 重力通道独立叠加，不会互相覆盖。
    /// </summary>
    public void SetExternalVerticalVelocity(float verticalSpeed)
    {
        _externalVerticalVelocity = verticalSpeed;
    }

    /// <summary>
    /// 清空外部持续速度通道（水平 + 垂直）。
    /// 约定由 VelocityClip.OnClipStop 调用，确保 Clip 结束后不会有速度残留。
    /// </summary>
    public void ClearExternalVelocity()
    {
        _externalHorizontalVelocity = Vector3.zero;
        _externalVerticalVelocity = 0f;
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
    private int _airborneFrameCounter;

    /// <summary>当前地面状态。</summary>
    public GroundState groundState => _groundState;

    /// <summary>是否在地面（Grounded 或 JustLanded）。供条件系统判断。</summary>
    public bool IsGrounded => _groundState == GroundState.Grounded || _groundState == GroundState.JustLanded;

    /// <summary>是否在空中（Airborne 或 JustLeftGround）。</summary>
    public bool IsAirborne => _groundState == GroundState.Airborne || _groundState == GroundState.JustLeftGround;

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
        if (actor.characterController == null) return;

        _cachedRootMotionDelta = ProcessRootMotionDeadZone(animator.deltaPosition);
        _cachedRootMotionRotationDelta = animator.deltaRotation;
    }

    /// <summary>统一计算与最终执行：地面状态 → 朝向 → 位移合成 → CC.Move → 帧末清零。</summary>
    private void Update()
    {
        // ── 1. 更新地面状态（在位移之前，供 PerformGravity 和其它系统读取）──
        UpdateGroundState();

        // ── 2. 朝向执行 ──
        PerformRotation();

        // ── 3. 从 Intent 换算 Locomotion 速度（内部计算，不再由外部写入）──
        Vector3 locomotionVelocity = Vector3.zero;
        if (!_locomotionSuppressed && _hasLocomotionIntent)
        {
            Vector3 dir = _locomotionIntent.WorldMoveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                float speed = _locomotionIntent.MoveStrength * _locomotionBaseSpeed;
                // 只削弱 Locomotion，External 和 Impulse 保持设计值
                if (IsAirborne) speed *= _airControlFactor;
                locomotionVelocity = dir * speed;
            }
        }

        // ── 4. 重力累积（纯粹：只演化 _gravityAccumulator，不碰冲量通道）──
        PerformGravity();

        // ── 5. 冲量演化：水平 drag 衰减，垂直撞顶/着地清零 ──
        PerformHorizontalDrag();
        PerformVerticalImpulseDecay();

        // ── 6. 位移合成 ──
        Vector3 finalMovement = Vector3.zero;

        // RootMotion 托管层
        if (_rootMotionApplyMode == RootMotionApplyMode.Managed)
        {
            finalMovement += _cachedRootMotionDelta;
        }

        // 水平通道：Locomotion + HorizontalImpulse + ExternalHorizontal（VelocityClip 持续速度）
        Vector3 horizontal = locomotionVelocity + _horizontalImpulseVelocity + _externalHorizontalVelocity;
        horizontal.y = 0f;

        // 垂直通道：
        // - rawVertical = GravityAccumulator + VerticalImpulse + ExternalVertical
        // - 后处理优先级：VerticalOverride > VerticalClamp > Raw
        float rawVertical = _gravityAccumulator + _verticalImpulseVelocity + _externalVerticalVelocity;
        float vertical;
        if (_hasVerticalVelocityOverride)
            vertical = _verticalVelocityOverride;
        else if (_hasVerticalClamp)
            vertical = Mathf.Clamp(rawVertical, _verticalClampMin, _verticalClampMax);
        else
            vertical = rawVertical;

        finalMovement += (horizontal + Vector3.up * vertical) * Time.deltaTime;

        // ── 7. 执行移动 ──
        if (actor.characterController != null)
        {
            actor.characterController.Move(finalMovement);
        }

        // ── 8. 记录实际速度（供动画系统读取）──
        _currentVelocity = Time.deltaTime > 0f ? finalMovement / Time.deltaTime : Vector3.zero;
        _currentHorizontalSpeed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude;
        _currentVerticalSpeed = _currentVelocity.y;

        // ── 9. 帧末清零（只清零每帧必须重置的量）──
        _cachedRootMotionDelta = Vector3.zero;
        _cachedRootMotionRotationDelta = Quaternion.identity;
        _hasLocomotionIntent = false; // 意图层每帧重置，要求外部每帧写入
        // 不清零：_horizontalImpulseVelocity（由 drag 自然衰减）
        //         _verticalImpulseVelocity（由 PerformVerticalImpulseDecay 在着地/撞顶时清零）
        //         _gravityAccumulator（由 PerformGravity 演化）
        //         _externalHorizontalVelocity / _externalVerticalVelocity（由 VelocityClip 生命周期管理：
        //             OnClipUpdate 每帧写入，OnClipStop 调 ClearExternalVelocity 归零）
        //         _hasFacingOverride / _locomotionSuppressed（由外部显式控制）
    }

    #endregion

    #region === 内部计算 ===

    /// <summary>
    /// 更新地面状态机。每帧读取 CC.isGrounded，经过离地帧数滤波，产生 4 种状态 + 落地/离地事件。
    /// 滤波原因：CharacterController 在斜坡、台阶上会偶发性地 isGrounded=false 1-2 帧，
    ///           直接用会导致事件频繁误触发。
    /// </summary>
    private void UpdateGroundState()
    {
        if (actor == null || actor.characterController == null) return;

        bool rawGrounded = actor.characterController.isGrounded;

        // 先把上一帧的瞬时状态收敛到稳定状态
        if (_groundState == GroundState.JustLanded) _groundState = GroundState.Grounded;
        else if (_groundState == GroundState.JustLeftGround) _groundState = GroundState.Airborne;

        if (rawGrounded)
        {
            _airborneFrameCounter = 0;
            if (_groundState == GroundState.Airborne)
            {
                _groundState = GroundState.JustLanded;
                jumpCount = 0; // 落地重置跳跃计数（内部闭环，无需跨组件事件）
                OnLanded?.Invoke();
            }
        }
        else
        {
            // 离地滤波：连续 N 帧离地才真正切换
            _airborneFrameCounter++;
            if (_groundState == GroundState.Grounded && _airborneFrameCounter > _airborneFrameThreshold)
            {
                _groundState = GroundState.JustLeftGround;
                OnLeftGround?.Invoke();
            }
        }
    }

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

    /// <summary>
    /// 重力计算，受 gravityScale 影响。
    /// 规则：在地面且没有向上冲量时，维持贴地力 -2 避免斜坡/台阶抖动弹起；
    ///       否则按重力累积（向下为负）。_gravityAccumulator 的值恒 ≤ 0。
    /// 不再碰 _verticalImpulseVelocity，冲量通道由 PerformVerticalImpulseDecay 独立管理。
    /// </summary>
    private void PerformGravity()
    {
        if (IsGrounded && _verticalImpulseVelocity <= 0f)
        {
            _gravityAccumulator = -2f;
        }
        else
        {
            _gravityAccumulator += Physics.gravity.y * _gravityScale * Time.deltaTime;
        }
    }

    /// <summary>水平冲量指数衰减：v *= exp(-drag * dt)。低于阈值时归零，避免浮点垃圾。</summary>
    private void PerformHorizontalDrag()
    {
        if (_horizontalImpulseVelocity.sqrMagnitude <= 0.0001f)
        {
            _horizontalImpulseVelocity = Vector3.zero;
            return;
        }
        float factor = Mathf.Exp(-_horizontalDrag * Time.deltaTime);
        _horizontalImpulseVelocity *= factor;
    }

    /// <summary>
    /// 垂直冲量衰减/清理：
    ///   - 空中：可选指数衰减（_verticalImpulseAirDrag > 0 时生效）
    ///   - 撞顶（CC.collisionFlags.Above）+ 向上冲量：立即截断，避免粘在天花板
    ///   - 着地 + 向下冲量：清零，让重力通道接管
    ///   - 低于阈值：归零，避免浮点垃圾
    /// </summary>
    private void PerformVerticalImpulseDecay()
    {
        if (IsAirborne && _verticalImpulseAirDrag > 0f && Mathf.Abs(_verticalImpulseVelocity) > 0.01f)
        {
            float factor = Mathf.Exp(-_verticalImpulseAirDrag * Time.deltaTime);
            _verticalImpulseVelocity *= factor;
        }

        if (actor != null && actor.characterController != null)
        {
            bool hitCeiling = (actor.characterController.collisionFlags & CollisionFlags.Above) != 0;
            if (hitCeiling && _verticalImpulseVelocity > 0f)
                _verticalImpulseVelocity = 0f;
        }

        if (IsGrounded && _verticalImpulseVelocity < 0f)
            _verticalImpulseVelocity = 0f;

        if (Mathf.Abs(_verticalImpulseVelocity) < 0.01f)
            _verticalImpulseVelocity = 0f;
    }

    #endregion
}