using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lightweight ownership token for ActorMotor motion domains.
/// Clips may only release the owner they acquired.
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
/// Translation authority used by ActorMotor.
/// Horizontal values are world-planar velocities except trajectory root motion,
/// which enters this domain as actor-local interval displacement before compose.
/// Vertical free movement is represented by a single ballistic velocity state.
/// </summary>
public sealed class TranslationDomain
{
    private const float VelocityEpsilon = 0.001f;

    private int _nextOwnerId = 1;

    private Vector3 _horizontalImpulseVelocity = Vector3.zero;
    private float _ballisticVerticalVelocity;

    private readonly List<HorizontalVelocityOwnerState> _horizontalVelocityOwners = new();
    private readonly List<VerticalVelocityOwnerState> _verticalVelocityOwners = new();
    private readonly List<TrajectoryRootMotionOwnerState> _trajectoryRootMotionOwners = new();

    private MotionOwner _tickTrajectoryRootMotionOwner;
    private Vector3 _tickTrajectoryRootMotionLocalPosition;

    public bool HasHorizontalVelocityOwner => _horizontalVelocityOwners.Count > 0;
    public bool HasVerticalVelocityOwner => _verticalVelocityOwners.Count > 0;

    public Vector3 DebugHorizontalImpulse => _horizontalImpulseVelocity;
    public float BallisticVerticalVelocity => _ballisticVerticalVelocity;
    public Vector3 DebugOwnerHorizontalVelocity => TryGetTopHorizontal(out HorizontalVelocityOwnerState horizontal) ? horizontal.Velocity : Vector3.zero;
    public float DebugOwnerVerticalVelocity => TryGetTopVertical(out VerticalVelocityOwnerState vertical) ? vertical.Velocity : 0f;
    public int DebugHorizontalVelocityOwnerCount => _horizontalVelocityOwners.Count;
    public int DebugVerticalVelocityOwnerCount => _verticalVelocityOwners.Count;
    public Vector3 HorizontalImpulseVelocity => _horizontalImpulseVelocity;
    public bool HasTrajectoryRootMotionTick => _tickTrajectoryRootMotionOwner.IsValid;
    public Vector3 TrajectoryRootMotionLocalPosition => _tickTrajectoryRootMotionLocalPosition;

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

    public MotionOwner BeginTrajectoryRootMotion()
    {
        MotionOwner owner = NewOwner();
        _trajectoryRootMotionOwners.Add(new TrajectoryRootMotionOwnerState(owner, Vector3.zero));
        return owner;
    }

    public bool SubmitTrajectoryRootMotion(MotionOwner owner, Vector3 localPositionDelta)
    {
        int index = FindTrajectoryRootMotionOwnerIndex(owner);
        if (index < 0)
            return false;

        TrajectoryRootMotionOwnerState state = _trajectoryRootMotionOwners[index];
        _trajectoryRootMotionOwners[index] = new TrajectoryRootMotionOwnerState(
            owner,
            state.PendingLocalPosition + localPositionDelta);
        return true;
    }

    public bool EndTrajectoryRootMotion(MotionOwner owner)
    {
        int index = FindTrajectoryRootMotionOwnerIndex(owner);
        if (index < 0)
            return false;

        _trajectoryRootMotionOwners.RemoveAt(index);
        if (IsCurrent(owner, _tickTrajectoryRootMotionOwner))
        {
            _tickTrajectoryRootMotionOwner = default;
            _tickTrajectoryRootMotionLocalPosition = Vector3.zero;
        }

        return true;
    }

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

    public void BeginMotionTick()
    {
        if (_trajectoryRootMotionOwners.Count > 0)
        {
            TrajectoryRootMotionOwnerState top = _trajectoryRootMotionOwners[_trajectoryRootMotionOwners.Count - 1];
            _tickTrajectoryRootMotionOwner = top.Owner;
            _tickTrajectoryRootMotionLocalPosition = top.PendingLocalPosition;
        }
        else
        {
            _tickTrajectoryRootMotionOwner = default;
            _tickTrajectoryRootMotionLocalPosition = Vector3.zero;
        }

        for (int i = 0; i < _trajectoryRootMotionOwners.Count; i++)
        {
            _trajectoryRootMotionOwners[i] = new TrajectoryRootMotionOwnerState(
                _trajectoryRootMotionOwners[i].Owner,
                Vector3.zero);
        }
    }

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

    public float ComposeVertical(float timeScale)
    {
        if (TryGetTopVertical(out VerticalVelocityOwnerState owner))
            return owner.Velocity * timeScale;

        return _ballisticVerticalVelocity * timeScale;
    }

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

    private int FindTrajectoryRootMotionOwnerIndex(MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = _trajectoryRootMotionOwners.Count - 1; i >= 0; i--)
        {
            if (_trajectoryRootMotionOwners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && current.IsValid && owner.Id == current.Id;
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

    private readonly struct TrajectoryRootMotionOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly Vector3 PendingLocalPosition;

        public TrajectoryRootMotionOwnerState(MotionOwner owner, Vector3 pendingLocalPosition)
        {
            Owner = owner;
            PendingLocalPosition = pendingLocalPosition;
        }
    }
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
