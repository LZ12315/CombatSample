using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// KCC bridge — implements ICharacterController, owns ActorMotionRuntime,
/// and drives it during KCC callbacks.
///
/// Velocity publishing: AfterCharacterUpdate computes solved velocity from
/// KCC displacement and hands it to runtime for readout.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor { get; private set; }
    public ActorMotionRuntime MotionRuntime { get; private set; }
    private ActorMovement _movement;

    [SerializeField, Tooltip("Collision filter layer mask. Only colliders on these layers cause collisions. ~0 = all.")]
    private LayerMask _collisionMask = ~0;

    // Per-tick bridge state (stays on ActorMotor — see plan §7)
    private Vector3 _motorFrameStartWorldPosition;
    private Vector3 _requestedVelocity;
    private bool _hasRequestedVelocity;
    private bool _pausedThisTick;

    private void Awake()
    {
        Motor = GetComponent<KinematicCharacterMotor>();
        MotionRuntime = new ActorMotionRuntime();

        var actor = GetComponent<Actor>();
        _movement = actor != null && actor.movement != null
            ? actor.movement
            : GetComponent<ActorMovement>();

        _movement?.BindMotor(this);
        Motor.CharacterController = this;
    }

    // ─────────────────── ICharacterController ───────────────────

    public void BeforeCharacterUpdate(float deltaTime)
    {
        _motorFrameStartWorldPosition = transform.position;
        _requestedVelocity = Vector3.zero;
        _hasRequestedVelocity = false;
        _pausedThisTick = false;

        MotionRuntime.BeginMotorTick();
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (_movement == null) return;
        MotionRuntime.ApplyKccGrounding(
            Motor.GroundingStatus.IsStableOnGround,
            Motor.LastGroundingStatus.IsStableOnGround);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_movement == null) return;
        currentRotation = _movement.GetPendingRotation();
        var rmRot = MotionRuntime.RootMotionPendingRotation;
        if (rmRot != Quaternion.identity)
            currentRotation = rmRot * currentRotation;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_movement == null || deltaTime <= 0f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        float effDt = deltaTime * MotionRuntime.MovementTimeScale;
        if (effDt <= 0.0001f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        // 1. Force unground: consume pending request, apply this tick
        if (MotionRuntime.ConsumeForceUngroundRequest())
        {
            Motor.ForceUnground(0.1f);
            MotionRuntime.MarkForcedUngroundedThisTick();
        }

        // 2. Determine groundedness for this tick (KCC status + local override)
        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        // 3. Step channels with config from ActorMovement
        ActorMotionRuntimeConfig config = _movement.GetRuntimeConfig();
        MotionRuntime.StepChannels(deltaTime, grounded, config);

        // 4. Compose KCC velocity from runtime
        Vector3 locomotion = _movement.GetCachedLocomotionVelocity();
        currentVelocity = MotionRuntime.ComposeKccVelocity(
            Motor,
            locomotion,
            grounded,
            deltaTime);

        _requestedVelocity = currentVelocity;
        _hasRequestedVelocity = true;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (_movement == null) return;

        Vector3 solvedVelocity = ComputeSolvedVelocity(deltaTime);
        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        ActorMotionRuntimeConfig config = _movement.GetRuntimeConfig();
        MotionRuntime.PublishSolvedVelocity(
            solvedVelocity,
            grounded,
            Time.fixedDeltaTime,
            config.VerticalSmoothTime);

        MotionRuntime.EndMotorTick();
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return (_collisionMask & (1 << coll.gameObject.layer)) != 0;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            MotionRuntime.SignalCeilingHit();
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    // ─────────────────── internal helpers ───────────────────

    private Vector3 ComputeSolvedVelocity(float deltaTime)
    {
        if (_pausedThisTick || deltaTime <= 0f)
            return Vector3.zero;

        Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
        Vector3 finalVelocity = solvedDelta / deltaTime;

        if (finalVelocity.sqrMagnitude < 0.000001f && _hasRequestedVelocity)
            finalVelocity = Motor.BaseVelocity;

        return finalVelocity;
    }

    private void SetPausedVelocity(ref Vector3 currentVelocity)
    {
        currentVelocity = Vector3.zero;
        _requestedVelocity = Vector3.zero;
        _hasRequestedVelocity = true;
        _pausedThisTick = true;
    }
}
