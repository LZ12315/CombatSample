using System;
using KinematicCharacterController;
using UnityEngine;

public readonly struct ActorMotionRuntimeConfig
{
    public readonly float HorizontalDrag;
    public readonly float VerticalImpulseAirDrag;
    public readonly float VerticalSmoothTime;
    public readonly float RootMotionYDeadZone;

    public ActorMotionRuntimeConfig(
        float horizontalDrag,
        float verticalImpulseAirDrag,
        float verticalSmoothTime,
        float rootMotionYDeadZone)
    {
        HorizontalDrag = horizontalDrag;
        VerticalImpulseAirDrag = verticalImpulseAirDrag;
        VerticalSmoothTime = verticalSmoothTime;
        RootMotionYDeadZone = rootMotionYDeadZone;
    }
}

/// <summary>
/// Plain C# runtime root that aggregates KCC-tick-bound state.
/// Owns MotionChannels, GroundingRuntime, RootMotionBuffer, VelocityReadout
/// and per-tick flags (force unground, ceiling hit, movement time / gravity strategy).
/// </summary>
public sealed class ActorMotionRuntime
{
    private readonly MotionChannels _channels = new();
    private readonly GroundingRuntime _grounding = new();
    private readonly RootMotionBuffer _rootMotion = new();
    private readonly VelocityReadout _velocity = new();

    private bool _pendingForceUnground;
    private bool _forceUngroundedThisTick;
    private bool _pendingCeilingHit;

    private float _movementTimeScale = 1f;
    private float _gravityScale = 1f;
    private ActorMovement.RootMotionApplyMode _rootMotionApplyMode;

    // ─── public properties ───

    public ActorMovement.GroundState GroundState => _grounding.State;
    public Vector3 CurrentVelocity => _velocity.CurrentVelocity;
    public float CurrentHorizontalSpeed => _velocity.CurrentHorizontalSpeed;
    public float CurrentVerticalSpeed => _velocity.CurrentVerticalSpeed;
    public bool ForceUngroundedThisTick => _forceUngroundedThisTick;

    public float MovementTimeScale => _movementTimeScale;
    public float GravityScale => _gravityScale;

    public int JumpCount => _grounding.JumpCount;

    public Quaternion AppliedRootMotionRotation =>
        ShouldApplyRootMotion ? _rootMotion.PendingRotation : Quaternion.identity;

    // ─── events (forward to GroundingRuntime) ───

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

    // ─── strategy state setters ───

    public void SetMovementTimeScale(float scale)
    {
        _movementTimeScale = Mathf.Max(0f, scale);
    }

    public void SetGravityScale(float scale)
    {
        _gravityScale = scale;
    }

    public void SetRootMotionApplyMode(ActorMovement.RootMotionApplyMode mode)
    {
        _rootMotionApplyMode = mode;
    }

    // ─── per-tick lifecycle ───

    public void BeginMotorTick()
    {
        _forceUngroundedThisTick = false;
    }

    public void EndMotorTick()
    {
        _forceUngroundedThisTick = false;
        _rootMotion.ClearAfterMotorTick();
    }

    // ─── impulse / velocity owner public API ───

    public void AddVerticalImpulse(float speed)
    {
        _channels.ApplyVerticalImpulse(speed);
        if (speed > 0f)
            _pendingForceUnground = true;
    }

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

    // ─── force unground / ceiling hit ───

    public bool ConsumeForceUngroundRequest()
    {
        bool result = _pendingForceUnground;
        _pendingForceUnground = false;
        return result;
    }

    public void MarkForcedUngroundedThisTick()
    {
        _forceUngroundedThisTick = true;
        _grounding.ForceUngroundNow();
    }

    public void SignalCeilingHit()
    {
        _pendingCeilingHit = true;
    }

    // ─── KCC-tick driving ───

    public void StepChannels(
        float deltaTime,
        bool grounded,
        ActorMotionRuntimeConfig config)
    {
        float dt = deltaTime * _movementTimeScale;
        _channels.StepGravity(dt, grounded, _gravityScale);
        _channels.StepHorizontalDrag(dt, config.HorizontalDrag);
        _channels.StepVerticalImpulse(
            dt,
            !grounded,
            grounded,
            _pendingCeilingHit,
            config.VerticalImpulseAirDrag);

        _pendingCeilingHit = false;
    }

    public Vector3 ComposeKccVelocity(
        KinematicCharacterMotor motor,
        Vector3 locomotionVelocity,
        bool isGrounded,
        float deltaTime)
    {
        if (ShouldApplyRootMotion && _rootMotion.PendingPosition.sqrMagnitude > 0.0001f)
        {
            Vector3 velocity = _rootMotion.PendingPosition / deltaTime;
            if (isGrounded)
                velocity = motor.GetDirectionTangentToSurface(
                    velocity,
                    motor.GroundingStatus.GroundNormal) * velocity.magnitude;
            return velocity;
        }

        Vector3 horizontal = _channels.ComposeHorizontal(locomotionVelocity);
        float vertical = _channels.ComposeVertical();

        if (isGrounded)
        {
            horizontal = motor.GetDirectionTangentToSurface(
                horizontal,
                motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
            vertical = 0f;
        }

        return horizontal + motor.CharacterUp * vertical;
    }

    public void PublishSolvedVelocity(
        Vector3 solvedVelocity,
        bool isStableGrounded,
        float smoothingDeltaTime,
        float verticalSmoothTime)
    {
        _velocity.Publish(
            solvedVelocity,
            isStableGrounded,
            smoothingDeltaTime,
            verticalSmoothTime);
    }

    // ─── grounding delegation ───

    public void ApplyKccGrounding(bool isStableNow, bool wasStable)
    {
        _grounding.ApplyKccGrounding(isStableNow, wasStable);
    }

    // ─── jump delegation ───

    public bool CanJump(int maxJumpCount)
    {
        return _grounding.CanJump(maxJumpCount);
    }

    public void ConsumeJump()
    {
        _grounding.ConsumeJump();
    }

    // ─── root motion delegation ───

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation,
        float yDeadZone)
    {
        _rootMotion.AddAnimatorDelta(deltaPosition, deltaRotation, yDeadZone);
    }

    private bool ShouldApplyRootMotion =>
        _rootMotionApplyMode == ActorMovement.RootMotionApplyMode.Managed;
}
