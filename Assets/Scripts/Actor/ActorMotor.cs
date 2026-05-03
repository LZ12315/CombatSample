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
    private ActorMovement _movement;

    private void Awake()
    {
        Motor = GetComponent<KinematicCharacterMotor>();
        var actor = GetComponent<Actor>();
        _movement = actor != null ? actor.movement : GetComponent<ActorMovement>();
        Motor.CharacterController = this;
    }

    // ─────────────────── ICharacterController ───────────────────

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (_movement == null) return;
        bool isStableNow = Motor.GroundingStatus.IsStableOnGround;
        bool wasStable = Motor.LastGroundingStatus.IsStableOnGround;
        _movement.ApplyGroundingUpdate(isStableNow, wasStable);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_movement == null) return;
        currentRotation = _movement.GetPendingRotation();
        var rmRot = _movement.GetPendingRootRotation();
        if (rmRot != Quaternion.identity)
            currentRotation = rmRot * currentRotation;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_movement == null || deltaTime <= 0f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        float effDt = deltaTime * _movement.MovementTimeScale;
        var state = _movement.GetMovementState();

        if (effDt <= 0.0001f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        if (state.ShouldForceUnground)
            Motor.ForceUnground(0.1f);

        bool isGrounded = Motor.GroundingStatus.IsStableOnGround;

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

        bool isAirborne = !isGrounded;

        _movement.StepChannels(deltaTime, isGrounded, isAirborne);

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
        if (_movement == null) return;
        _movement.SignalMotorFrameEnd();
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (_movement != null && Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            _movement.SignalCeilingHit();
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
