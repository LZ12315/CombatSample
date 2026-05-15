using UnityEngine;

/// <summary>
/// 轻量级运动控制所有权标识。
/// Clip 只能释放自己用同一个 owner 获取到的控制权。
/// </summary>
public readonly struct MotionOwner
{
    public readonly int Id;

    public bool IsValid => Id != 0;

    public MotionOwner(int id)
    {
        Id = id;
    }
}

/// <summary>
/// 由 ActorMotionRuntime 持有的运行时运动通道。
/// 这是纯 C# 状态，不是 Unity 组件。
///
/// 水平和垂直 API 有意保持不对称：
/// 水平运动建模的是平面动量，垂直运动同时混合了起跳/击飞、下砸、
/// 重力和接地贴附等语义。
///
/// 通道分类：
/// - Locomotion：调用方提供的基础水平速度。
/// - Impulse：可叠加的水平动量，以及 launch/slam 语义的垂直意图。
/// - Velocity owner：Action/Timeline 对单轴速度的单槽强覆盖。
/// - Gravity accumulator：没有垂直 owner 时的内部垂直演化状态。
///
/// Velocity owner 每个轴只有一个槽位，没有栈，也不会恢复旧 owner。
/// </summary>
public sealed class MotionChannels
{
    #region === 常量与状态 ===

    private const float GroundStickVelocity = -2f;
    private const float VelocityEpsilon = 0.001f;

    private int _nextOwnerId = 1;

    private Vector3 _horizontalImpulseVelocity = Vector3.zero;
    private float _gravityAccumulator;
    private float _verticalImpulseVelocity;

    private MotionOwner _horizontalVelocityOwner;
    private Vector3 _horizontalVelocity = Vector3.zero;

    private MotionOwner _verticalVelocityOwner;
    private float _verticalVelocity;

    public bool HasHorizontalVelocityOwner => _horizontalVelocityOwner.IsValid;
    public bool HasVerticalVelocityOwner => _verticalVelocityOwner.IsValid;

    #endregion

    #region === Velocity Owner 控制 ===

    /// <summary>
    /// 清空两个轴的 velocity owner 及其缓存速度。
    /// Action 入场时用它做硬重置，避免旧 Action 的 owner 泄漏到新 Action。
    /// </summary>
    public void ClearVelocityOwners()
    {
        _horizontalVelocityOwner = default;
        _horizontalVelocity = Vector3.zero;
        _verticalVelocityOwner = default;
        _verticalVelocity = 0f;
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        if (_horizontalVelocityOwner.IsValid)
        {
            Debug.LogWarning($"[MotionChannels] Replacing active horizontal owner id={_horizontalVelocityOwner.Id}. " +
                             "Velocity owner is single-slot; old owner will not regain control automatically.");
        }

        _horizontalVelocityOwner = NewOwner();
        _horizontalVelocity = Vector3.zero;
        return _horizontalVelocityOwner;
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        if (!IsCurrent(owner, _horizontalVelocityOwner))
            return;

        velocity.y = 0f;
        _horizontalVelocity = velocity;
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        if (!IsCurrent(owner, _horizontalVelocityOwner))
            return;

        _horizontalVelocityOwner = default;
        _horizontalVelocity = Vector3.zero;
    }

    public MotionOwner BeginVerticalVelocity()
    {
        if (_verticalVelocityOwner.IsValid)
        {
            Debug.LogWarning($"[MotionChannels] Replacing active vertical owner id={_verticalVelocityOwner.Id}. " +
                             "Velocity owner is single-slot; old owner will not regain control automatically.");
        }

        _verticalVelocityOwner = NewOwner();
        _verticalVelocity = 0f;
        return _verticalVelocityOwner;
    }

    public void SetVerticalVelocity(MotionOwner owner, float velocity)
    {
        if (!IsCurrent(owner, _verticalVelocityOwner))
            return;

        _verticalVelocity = velocity;
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        if (!IsCurrent(owner, _verticalVelocityOwner))
            return;

        _verticalVelocityOwner = default;
        _verticalVelocity = 0f;
    }

    #endregion

    #region === Impulse 与 Handoff ===

    public void AddHorizontalImpulse(Vector3 velocity)
    {
        velocity.y = 0f;
        _horizontalImpulseVelocity += velocity;
    }

    public void ClearHorizontalImpulse()
    {
        _horizontalImpulseVelocity = Vector3.zero;
    }

    /// <summary>
    /// 写入垂直 launch/slam 意图。
    /// 向上冲量保留当前最强的 launch；向下冲量直接覆盖当前垂直冲量，
    /// 让下砸能立刻生效。
    /// </summary>
    public void ApplyVerticalImpulse(float verticalSpeed)
    {
        if (verticalSpeed >= 0f)
            _verticalImpulseVelocity = Mathf.Max(_verticalImpulseVelocity, verticalSpeed);
        else
            _verticalImpulseVelocity = verticalSpeed;

        if (verticalSpeed > 0f)
            _gravityAccumulator = 0f;
    }

    /// <summary>
    /// Action 入场时按配置继承已有动量的一部分。
    /// Velocity owner 不参与继承；ActionInstance 会显式清空它们。
    /// </summary>
    public void ApplyHandoff(float horizontalInheritance, float verticalInheritance)
    {
        horizontalInheritance = Mathf.Clamp01(horizontalInheritance);
        verticalInheritance = Mathf.Clamp01(verticalInheritance);

        _horizontalImpulseVelocity *= horizontalInheritance;
        _verticalImpulseVelocity *= verticalInheritance;
        _gravityAccumulator *= verticalInheritance;
    }

    #endregion

    #region === Tick 演化 ===

    /// <summary>
    /// 接地时调和内部垂直状态。
    /// 清掉垂直冲量，并把重力钳到一个较小的贴地速度。
    /// 这只影响下一次离地手感；接地时对外发布的 CurrentVelocity.y
    /// 仍由 VelocityReadout 负责归零。
    /// 水平冲量有意保留，不在这里清除。
    /// </summary>
    internal void ResetVerticalStateForGround()
    {
        _verticalImpulseVelocity = 0f;
        _gravityAccumulator = GroundStickVelocity;
    }

    /// <summary>
    /// 演化重力。dt 是 Actor 本地 motion delta（已乘 MovementTimeScale）。
    /// 若垂直 velocity owner 正在接管该轴，则暂停内部重力演化。
    /// 稳定接地时会钳制垂直内部状态，而不是继续累积重力。
    /// </summary>
    public void StepGravity(float dt, bool isGrounded, float gravityScale)
    {
        if (_verticalVelocityOwner.IsValid)
            return;

        if (isGrounded)
            ResetVerticalStateForGround();
        else
            _gravityAccumulator += Physics.gravity.y * gravityScale * dt;
    }

    /// <summary>
    /// 衰减可叠加的水平冲量。dt 是 Actor 本地 motion delta。
    /// Locomotion 和 velocity owner 不在这里衰减。
    /// </summary>
    public void StepHorizontalDrag(float dt, float drag)
    {
        if (_horizontalImpulseVelocity.sqrMagnitude <= VelocityEpsilon * VelocityEpsilon)
        {
            _horizontalImpulseVelocity = Vector3.zero;
            return;
        }

        float factor = Mathf.Exp(-drag * dt);
        _horizontalImpulseVelocity *= factor;
    }

    /// <summary>
    /// 衰减垂直冲量，并处理一次性的垂直碰撞反馈。dt 是 Actor 本地 motion delta。
    /// 撞天花板会截断向上速度，但不会把已累积的重力瞬间暴露成过快下落。
    /// </summary>
    public void StepVerticalImpulse(
        float dt,
        bool isAirborne,
        bool isGrounded,
        bool hitCeiling,
        float airDrag)
    {
        if (isAirborne && airDrag > 0f && Mathf.Abs(_verticalImpulseVelocity) > 0.01f)
        {
            float factor = Mathf.Exp(-airDrag * dt);
            _verticalImpulseVelocity *= factor;
        }

        if (hitCeiling && _verticalImpulseVelocity > 0f)
        {
            float currentVerticalSpeed = _gravityAccumulator + _verticalImpulseVelocity;
            _verticalImpulseVelocity = 0f;
            _gravityAccumulator = Mathf.Min(0f, currentVerticalSpeed);
        }

        if (isGrounded)
            _verticalImpulseVelocity = 0f;

        if (Mathf.Abs(_verticalImpulseVelocity) < 0.01f)
            _verticalImpulseVelocity = 0f;
    }

    #endregion

    #region === 速度合成 ===

    /// <summary>
    /// 合成送给 ActorMotor 做 KCC 地面投影前的水平请求速度。
    /// 水平 owner 会完全覆盖 locomotion 和水平冲量。
    /// timeScale 是 MovementTimeScale，作为统一出口倍率在此应用。
    /// </summary>
    public Vector3 ComposeHorizontal(Vector3 locomotionVelocity, float timeScale)
    {
        if (_horizontalVelocityOwner.IsValid)
            return _horizontalVelocity * timeScale;

        Vector3 horizontal = locomotionVelocity + _horizontalImpulseVelocity;
        horizontal.y = 0f;
        return horizontal * timeScale;
    }

    /// <summary>
    /// 合成 ActorMotor 执行接地钳制前的垂直请求速度。
    /// 垂直 owner 会完全覆盖重力和垂直冲量。
    /// timeScale 是 MovementTimeScale，作为统一出口倍率在此应用。
    /// </summary>
    public float ComposeVertical(float timeScale)
    {
        if (_verticalVelocityOwner.IsValid)
            return _verticalVelocity * timeScale;

        return (_gravityAccumulator + _verticalImpulseVelocity) * timeScale;
    }

    #endregion

    #region === 内部工具 ===

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && owner.Id == current.Id;
    }

    #endregion
}
