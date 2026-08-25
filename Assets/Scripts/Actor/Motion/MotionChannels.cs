using UnityEngine;
using System.Collections.Generic;

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
/// ActorMotor 的 Translation Domain。
/// 所有水平数据均为 world planar velocity（Root Motion 的 interval displacement
/// 在 compose boundary 进入这里之前完成换算）；垂直自由运动只有一个
/// BallisticVerticalVelocity authoritative state。
/// </summary>
public sealed class TranslationDomain
{
    #region === 常量与状态 ===

    private const float VelocityEpsilon = 0.001f;

    private int _nextOwnerId = 1;

    private Vector3 _horizontalImpulseVelocity = Vector3.zero;
    private float _ballisticVerticalVelocity;

    private readonly List<HorizontalVelocityOwnerState> _horizontalVelocityOwners = new();

    private readonly List<VerticalVelocityOwnerState> _verticalVelocityOwners = new();

    public bool HasHorizontalVelocityOwner => _horizontalVelocityOwners.Count > 0;
    public bool HasVerticalVelocityOwner => _verticalVelocityOwners.Count > 0;

    public Vector3 DebugHorizontalImpulse => _horizontalImpulseVelocity;
    public float BallisticVerticalVelocity => _ballisticVerticalVelocity;
    public Vector3 DebugOwnerHorizontalVelocity => TryGetTopHorizontal(out HorizontalVelocityOwnerState horizontal) ? horizontal.Velocity : Vector3.zero;
    public float DebugOwnerVerticalVelocity => TryGetTopVertical(out VerticalVelocityOwnerState vertical) ? vertical.Velocity : 0f;
    public int DebugHorizontalVelocityOwnerCount => _horizontalVelocityOwners.Count;
    public int DebugVerticalVelocityOwnerCount => _verticalVelocityOwners.Count;
    public Vector3 HorizontalImpulseVelocity => _horizontalImpulseVelocity;

    #endregion

    #region === Velocity Owner 控制 ===

    /// <summary>
    /// 清空两个轴的 velocity owner 及其缓存速度。
    /// Action 入场时用它做硬重置，避免旧 Action 的 owner 泄漏到新 Action。
    /// </summary>
    public void ClearVelocityOwners()
    {
        bool hadVerticalOwners = _verticalVelocityOwners.Count > 0;
        _horizontalVelocityOwners.Clear();
        _verticalVelocityOwners.Clear();
        if (hadVerticalOwners)
            _ballisticVerticalVelocity = 0f;
    }

    public MotionOwner BeginHorizontalVelocity()
    {
        MotionOwner owner = NewOwner();
        _horizontalVelocityOwners.Add(new HorizontalVelocityOwnerState(owner, Vector3.zero));
        return owner;
    }

    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity)
    {
        int index = FindHorizontalOwnerIndex(owner);
        if (index < 0)
            return;

        velocity.y = 0f;
        _horizontalVelocityOwners[index] = new HorizontalVelocityOwnerState(owner, velocity);
    }

    public void EndHorizontalVelocity(MotionOwner owner)
    {
        int index = FindHorizontalOwnerIndex(owner);
        if (index >= 0)
            _horizontalVelocityOwners.RemoveAt(index);
    }

    public MotionOwner BeginVerticalVelocity()
    {
        MotionOwner owner = NewOwner();
        _verticalVelocityOwners.Add(new VerticalVelocityOwnerState(owner, 0f));
        return owner;
    }

    public bool SetVerticalVelocity(MotionOwner owner, float velocity)
    {
        int index = FindVerticalOwnerIndex(owner);
        if (index < 0)
            return false;

        _verticalVelocityOwners[index] = new VerticalVelocityOwnerState(owner, velocity);
        return true;
    }

    public bool IsTopVerticalVelocityOwner(MotionOwner owner)
    {
        return owner.IsValid
            && TryGetTopVertical(out VerticalVelocityOwnerState current)
            && current.Owner.Id == owner.Id;
    }

    public void EndVerticalVelocity(MotionOwner owner)
    {
        int index = FindVerticalOwnerIndex(owner);
        if (index < 0)
            return;

        _verticalVelocityOwners.RemoveAt(index);
        if (_verticalVelocityOwners.Count == 0)
            _ballisticVerticalVelocity = 0f;
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

    public void AddBallisticVerticalVelocity(float velocity)
    {
        _ballisticVerticalVelocity += velocity;
    }

    public void SetBallisticVerticalVelocity(float velocity)
    {
        _ballisticVerticalVelocity = velocity;
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
        _ballisticVerticalVelocity *= verticalInheritance;
    }

    #endregion

    #region === Tick 演化 ===

    /// <summary>
    /// 演化唯一 Ballistic state。Vertical owner 活跃时冻结；稳定接地时归零。
    /// </summary>
    public void StepBallistic(
        float dt,
        bool isGrounded,
        bool hitCeiling,
        float gravityScale,
        float legacyAirDrag)
    {
        if (HasVerticalVelocityOwner)
            return;

        if (isGrounded)
        {
            _ballisticVerticalVelocity = 0f;
            return;
        }

        if (hitCeiling && _ballisticVerticalVelocity > 0f)
            _ballisticVerticalVelocity = 0f;

        if (legacyAirDrag > 0f && Mathf.Abs(_ballisticVerticalVelocity) > 0.01f)
            _ballisticVerticalVelocity *= Mathf.Exp(-legacyAirDrag * dt);

        _ballisticVerticalVelocity += Physics.gravity.y * gravityScale * dt;

        if (Mathf.Abs(_ballisticVerticalVelocity) < 0.01f)
            _ballisticVerticalVelocity = 0f;
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

    #endregion

    #region === 速度合成 ===

    /// <summary>
    /// 合成送给 ActorMotor 做 KCC 地面投影前的水平请求速度。
    /// 水平 owner 会完全覆盖 locomotion 和水平冲量。
    /// timeScale 是 MovementTimeScale，作为统一出口倍率在此应用。
    /// </summary>
    public Vector3 ComposeHorizontal(Vector3 locomotionVelocity, float timeScale)
    {
        if (TryGetTopHorizontal(out HorizontalVelocityOwnerState owner))
            return owner.Velocity * timeScale;

        Vector3 horizontal = locomotionVelocity + _horizontalImpulseVelocity;
        horizontal.y = 0f;
        return horizontal * timeScale;
    }

    public bool TryComposeHorizontalVelocityOwner(float timeScale, out Vector3 velocity)
    {
        if (!TryGetTopHorizontal(out HorizontalVelocityOwnerState owner))
        {
            velocity = Vector3.zero;
            return false;
        }

        velocity = owner.Velocity * timeScale;
        return true;
    }

    /// <summary>
    /// 合成 ActorMotor 执行接地钳制前的垂直请求速度。
    /// 垂直 owner 会完全覆盖 BallisticVerticalVelocity。
    /// timeScale 是 MovementTimeScale，作为统一出口倍率在此应用。
    /// </summary>
    public float ComposeVertical(float timeScale)
    {
        if (TryGetTopVertical(out VerticalVelocityOwnerState owner))
            return owner.Velocity * timeScale;

        return _ballisticVerticalVelocity * timeScale;
    }

    #endregion

    #region === 内部工具 ===

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private int FindHorizontalOwnerIndex(MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = _horizontalVelocityOwners.Count - 1; i >= 0; i--)
        {
            if (_horizontalVelocityOwners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private int FindVerticalOwnerIndex(MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = _verticalVelocityOwners.Count - 1; i >= 0; i--)
        {
            if (_verticalVelocityOwners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private bool TryGetTopHorizontal(out HorizontalVelocityOwnerState owner)
    {
        if (_horizontalVelocityOwners.Count == 0)
        {
            owner = default;
            return false;
        }

        owner = _horizontalVelocityOwners[_horizontalVelocityOwners.Count - 1];
        return true;
    }

    private bool TryGetTopVertical(out VerticalVelocityOwnerState owner)
    {
        if (_verticalVelocityOwners.Count == 0)
        {
            owner = default;
            return false;
        }

        owner = _verticalVelocityOwners[_verticalVelocityOwners.Count - 1];
        return true;
    }

    private readonly struct HorizontalVelocityOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly Vector3 Velocity;

        public HorizontalVelocityOwnerState(MotionOwner owner, Vector3 velocity)
        {
            Owner = owner;
            Velocity = velocity;
        }
    }

    private readonly struct VerticalVelocityOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly float Velocity;

        public VerticalVelocityOwnerState(MotionOwner owner, float velocity)
        {
            Owner = owner;
            Velocity = velocity;
        }
    }

    #endregion
}

/// <summary>
/// ActorMotor supporting state for temporary motion constraints.
/// Each parameter owns its own token list and combine rule.
/// </summary>
public sealed class MotionPolicyState
{
    private int _nextOwnerId = 1;

    private float _baseLocomotionScale = 1f;
    private float _baseAirLocomotionScale = 1f;
    private float _baseGravityScale = 1f;

    private readonly List<PolicyOwnerState> _locomotionScaleOwners = new();
    private readonly List<PolicyOwnerState> _airLocomotionScaleOwners = new();
    private readonly List<PolicyOwnerState> _gravityScaleOwners = new();

    public float LocomotionScale => ComposeMin(_baseLocomotionScale, _locomotionScaleOwners);
    public float AirLocomotionScale => ComposeMin(_baseAirLocomotionScale, _airLocomotionScaleOwners);
    public float GravityScale => ComposeMultiply(_baseGravityScale, _gravityScaleOwners);

    public int DebugLocomotionScaleOwnerCount => _locomotionScaleOwners.Count;
    public int DebugAirLocomotionScaleOwnerCount => _airLocomotionScaleOwners.Count;
    public int DebugGravityScaleOwnerCount => _gravityScaleOwners.Count;

    public void SetBaseLocomotionScale(float scale)
    {
        _baseLocomotionScale = Sanitize01(scale);
    }

    public void SetBaseAirLocomotionScale(float scale)
    {
        _baseAirLocomotionScale = Sanitize01(scale);
    }

    public void SetBaseGravityScale(float scale)
    {
        _baseGravityScale = SanitizeNonNegative(scale);
    }

    public MotionOwner BeginLocomotionScale(float scale)
    {
        return Begin(_locomotionScaleOwners, Sanitize01(scale));
    }

    public bool UpdateLocomotionScale(MotionOwner owner, float scale)
    {
        return Update(_locomotionScaleOwners, owner, Sanitize01(scale));
    }

    public bool EndLocomotionScale(MotionOwner owner)
    {
        return End(_locomotionScaleOwners, owner);
    }

    public MotionOwner BeginAirLocomotionScale(float scale)
    {
        return Begin(_airLocomotionScaleOwners, Sanitize01(scale));
    }

    public bool UpdateAirLocomotionScale(MotionOwner owner, float scale)
    {
        return Update(_airLocomotionScaleOwners, owner, Sanitize01(scale));
    }

    public bool EndAirLocomotionScale(MotionOwner owner)
    {
        return End(_airLocomotionScaleOwners, owner);
    }

    public MotionOwner BeginGravityScale(float scale)
    {
        return Begin(_gravityScaleOwners, SanitizeNonNegative(scale));
    }

    public bool UpdateGravityScale(MotionOwner owner, float scale)
    {
        return Update(_gravityScaleOwners, owner, SanitizeNonNegative(scale));
    }

    public bool EndGravityScale(MotionOwner owner)
    {
        return End(_gravityScaleOwners, owner);
    }

    public void ClearPolicyOwners()
    {
        _locomotionScaleOwners.Clear();
        _airLocomotionScaleOwners.Clear();
        _gravityScaleOwners.Clear();
    }

    private MotionOwner Begin(List<PolicyOwnerState> owners, float value)
    {
        MotionOwner owner = NewOwner();
        owners.Add(new PolicyOwnerState(owner, value));
        return owner;
    }

    private static bool Update(List<PolicyOwnerState> owners, MotionOwner owner, float value)
    {
        int index = FindOwnerIndex(owners, owner);
        if (index < 0)
            return false;

        owners[index] = new PolicyOwnerState(owner, value);
        return true;
    }

    private static bool End(List<PolicyOwnerState> owners, MotionOwner owner)
    {
        int index = FindOwnerIndex(owners, owner);
        if (index < 0)
            return false;

        owners.RemoveAt(index);
        return true;
    }

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private static int FindOwnerIndex(List<PolicyOwnerState> owners, MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = owners.Count - 1; i >= 0; i--)
        {
            if (owners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private static float ComposeMin(float baseValue, List<PolicyOwnerState> owners)
    {
        float result = Sanitize01(baseValue);
        for (int i = 0; i < owners.Count; i++)
            result = Mathf.Min(result, owners[i].Value);

        return result;
    }

    private static float ComposeMultiply(float baseValue, List<PolicyOwnerState> owners)
    {
        float result = SanitizeNonNegative(baseValue);
        for (int i = 0; i < owners.Count; i++)
            result *= owners[i].Value;

        return result;
    }

    private static float Sanitize01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 1f;

        return Mathf.Clamp01(value);
    }

    private static float SanitizeNonNegative(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 1f;

        return Mathf.Max(0f, value);
    }

    private readonly struct PolicyOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly float Value;

        public PolicyOwnerState(MotionOwner owner, float value)
        {
            Owner = owner;
            Value = value;
        }
    }
}

/// <summary>
/// E3-B compatibility façade。旧调用方和 Editor 暂时仍可使用 MotionChannels，
/// authoritative state 与 compose 规则全部由 TranslationDomain 持有。
/// </summary>
public sealed class MotionChannels
{
    private readonly TranslationDomain _translation;

    public MotionChannels()
        : this(new TranslationDomain())
    {
    }

    internal MotionChannels(TranslationDomain translation)
    {
        _translation = translation;
    }

    public TranslationDomain Domain => _translation;
    public bool HasHorizontalVelocityOwner => _translation.HasHorizontalVelocityOwner;
    public bool HasVerticalVelocityOwner => _translation.HasVerticalVelocityOwner;
    public Vector3 DebugHorizontalImpulse => _translation.DebugHorizontalImpulse;
    public float DebugVerticalImpulse => _translation.BallisticVerticalVelocity;
    public float DebugGravityAccumulator => 0f;
    public float DebugBallisticVerticalVelocity => _translation.BallisticVerticalVelocity;
    public Vector3 DebugOwnerHorizontalVelocity => _translation.DebugOwnerHorizontalVelocity;
    public float DebugOwnerVerticalVelocity => _translation.DebugOwnerVerticalVelocity;
    public int DebugHorizontalVelocityOwnerCount => _translation.DebugHorizontalVelocityOwnerCount;
    public int DebugVerticalVelocityOwnerCount => _translation.DebugVerticalVelocityOwnerCount;
    public Vector3 HorizontalImpulseVelocity => _translation.HorizontalImpulseVelocity;

    public void ClearVelocityOwners() => _translation.ClearVelocityOwners();
    public MotionOwner BeginHorizontalVelocity() => _translation.BeginHorizontalVelocity();
    public void SetHorizontalVelocity(MotionOwner owner, Vector3 velocity) => _translation.SetHorizontalVelocity(owner, velocity);
    public void EndHorizontalVelocity(MotionOwner owner) => _translation.EndHorizontalVelocity(owner);
    public MotionOwner BeginVerticalVelocity() => _translation.BeginVerticalVelocity();
    public bool SetVerticalVelocity(MotionOwner owner, float velocity) => _translation.SetVerticalVelocity(owner, velocity);
    public bool IsTopVerticalVelocityOwner(MotionOwner owner) => _translation.IsTopVerticalVelocityOwner(owner);
    public void EndVerticalVelocity(MotionOwner owner) => _translation.EndVerticalVelocity(owner);
    public void AddHorizontalImpulse(Vector3 velocity) => _translation.AddHorizontalImpulse(velocity);
    public void ClearHorizontalImpulse() => _translation.ClearHorizontalImpulse();
    public void ApplyVerticalImpulse(float velocity) => _translation.AddBallisticVerticalVelocity(velocity);
    public void AddBallisticVerticalVelocity(float velocity) => _translation.AddBallisticVerticalVelocity(velocity);
    public void SetBallisticVerticalVelocity(float velocity) => _translation.SetBallisticVerticalVelocity(velocity);
    public void ApplyHandoff(float horizontalInheritance, float verticalInheritance) => _translation.ApplyHandoff(horizontalInheritance, verticalInheritance);
    public void StepGravity(float dt, bool isGrounded, float gravityScale) =>
        _translation.StepBallistic(dt, isGrounded, false, gravityScale, 0f);
    public void StepHorizontalDrag(float dt, float drag) => _translation.StepHorizontalDrag(dt, drag);
    public void StepVerticalImpulse(float dt, bool isAirborne, bool isGrounded, bool hitCeiling, float airDrag) =>
        _translation.StepBallistic(dt, isGrounded, hitCeiling, 0f, isAirborne ? airDrag : 0f);
    public Vector3 ComposeHorizontal(Vector3 locomotionVelocity, float timeScale) =>
        _translation.ComposeHorizontal(locomotionVelocity, timeScale);
    public bool TryComposeHorizontalVelocityOwner(float timeScale, out Vector3 velocity) =>
        _translation.TryComposeHorizontalVelocityOwner(timeScale, out velocity);
    public float ComposeVertical(float timeScale) => _translation.ComposeVertical(timeScale);
}
