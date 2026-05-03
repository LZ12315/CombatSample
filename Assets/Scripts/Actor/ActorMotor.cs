using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// KCC 桥接层 — 实现 ICharacterController，在 KCC 回调中将 ActorMovement 的业务意图
/// 翻译为 KCC 所需的 BaseVelocity 和 Rotation。
///
/// 只负责翻译，不持有业务逻辑。业务逻辑全部在 ActorMovement 中。
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor { get; private set; }
    private Actor _actor;
    private ActorMovement _movement;

    private ActorMovement Movement
    {
        get
        {
            if (_movement == null)
            {
                if (_actor == null)
                    _actor = GetComponent<Actor>();
                if (_actor != null)
                    _movement = _actor.movement;
                if (_movement == null)
                    _movement = GetComponent<ActorMovement>();
            }
            return _movement;
        }
    }

    private void Awake()
    {
        Motor = GetComponent<KinematicCharacterMotor>();
        _actor = GetComponent<Actor>();
        if (_actor != null)
            _movement = _actor.movement;
        if (_movement == null)
            _movement = GetComponent<ActorMovement>();
        Motor.CharacterController = this;
    }

    // ─────────────────── ICharacterController ───────────────────

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (Movement == null) return;
        bool isStableNow = Motor.GroundingStatus.IsStableOnGround;
        bool wasStable = Motor.LastGroundingStatus.IsStableOnGround;
        Movement.ApplyGroundingUpdate(isStableNow, wasStable);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (Movement == null) return;
        currentRotation = Movement.ConsumePendingRotation();
        var rmRot = Movement.ConsumePendingRootRotation();
        if (rmRot != Quaternion.identity)
            currentRotation = rmRot * currentRotation;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (Movement == null || deltaTime <= 0f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        float effDt = deltaTime * Movement.MovementTimeScale;
        var state = Movement.GetMovementState();

        if (effDt <= 0.0001f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        if (state.ShouldForceUnground)
            Motor.ForceUnground(0.1f);

        if (state.IsRootMotionManaged && state.RootMotionDelta.sqrMagnitude > 0.0001f)
        {
            currentVelocity = state.RootMotionDelta / deltaTime;

            if (Motor.GroundingStatus.IsStableOnGround)
            {
                currentVelocity = Motor.GetDirectionTangentToSurface(
                    currentVelocity,
                    Motor.GroundingStatus.GroundNormal
                ) * currentVelocity.magnitude;
            }
            return;
        }

        bool isGrounded = Motor.GroundingStatus.IsStableOnGround;
        bool isAirborne = !isGrounded;

        Movement.StepChannels(deltaTime, isGrounded, isAirborne);

        Vector3 horizontal = state.HorizontalVelocity;
        float vertical = state.VerticalVelocity;

        if (isGrounded)
        {
            horizontal = Motor.GetDirectionTangentToSurface(
                horizontal,
                Motor.GroundingStatus.GroundNormal
            ) * horizontal.magnitude;
            vertical = 0f;
        }

        currentVelocity = horizontal + Motor.CharacterUp * vertical;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (Movement == null) return;
        Movement.SignalMotorFrameEnd();
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (Movement != null && Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            Movement.SignalCeilingHit();
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
