using UnityEngine;

/// <summary>
/// Lightweight identity for clip-level motion ownership.
/// A clip can only release the state it acquired with the same owner.
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
/// Runtime motion channels owned by ActorMovement.
/// This is plain C# state, not a Unity component.
/// Velocity owner is single-slot per axis (no stack and no fallback).
/// </summary>
public sealed class MotionChannels
{
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

    /// <summary>
    /// Clears velocity owners for both axes and their cached velocity values.
    /// Use this as an Action-entry hard reset so owner control never leaks across Actions.
    /// </summary>
    public void ClearVelocityOwners()
    {
        _horizontalVelocityOwner = default;
        _horizontalVelocity = Vector3.zero;
        _verticalVelocityOwner = default;
        _verticalVelocity = 0f;
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

    public void AddVerticalImpulse(float upwardSpeed)
    {
        if (upwardSpeed >= 0f)
            _verticalImpulseVelocity = Mathf.Max(_verticalImpulseVelocity, upwardSpeed);
        else
            _verticalImpulseVelocity = upwardSpeed;

        if (upwardSpeed > 0f)
            _gravityAccumulator = 0f;
    }

    public void ApplyHandoff(float horizontalInheritance, float verticalInheritance)
    {
        horizontalInheritance = Mathf.Clamp01(horizontalInheritance);
        verticalInheritance = Mathf.Clamp01(verticalInheritance);

        _horizontalImpulseVelocity *= horizontalInheritance;
        _verticalImpulseVelocity *= verticalInheritance;
        _gravityAccumulator *= verticalInheritance;
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

    /// <summary>
    /// 着地时调和垂直内部状态：清掉向下冲量，重力钳到 GroundStickVelocity。
    /// 只影响下一次离地手感，不影响外部 CurrentVelocity（由 PublishMotorVelocity 归零 y）。
    /// 不清除水平冲量。
    /// </summary>
    internal void ResetVerticalForGround()
    {
        if (_verticalImpulseVelocity < 0f)
            _verticalImpulseVelocity = 0f;
        _gravityAccumulator = GroundStickVelocity;
    }

    public void StepGravity(float dt, bool isGrounded, float gravityScale)
    {
        if (_verticalVelocityOwner.IsValid)
            return;

        if (isGrounded)
            ResetVerticalForGround();
        else
            _gravityAccumulator += Physics.gravity.y * gravityScale * dt;
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

    public void StepVerticalImpulseDecay(
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
            _verticalImpulseVelocity = 0f;

        if (isGrounded && _verticalImpulseVelocity < 0f)
            _verticalImpulseVelocity = 0f;

        if (Mathf.Abs(_verticalImpulseVelocity) < 0.01f)
            _verticalImpulseVelocity = 0f;
    }

    public Vector3 ComposeHorizontal(Vector3 locomotionVelocity)
    {
        if (_horizontalVelocityOwner.IsValid)
            return _horizontalVelocity;

        Vector3 horizontal = locomotionVelocity + _horizontalImpulseVelocity;
        horizontal.y = 0f;
        return horizontal;
    }

    public float ComposeVertical()
    {
        if (_verticalVelocityOwner.IsValid)
            return _verticalVelocity;

        return _gravityAccumulator + _verticalImpulseVelocity;
    }

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
}
