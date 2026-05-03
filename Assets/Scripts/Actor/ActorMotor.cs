using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// KCC 桥接层 — 实现 ICharacterController，在 KCC 回调中将 ActorMovement 的业务意图
/// 翻译为 KCC 所需的 BaseVelocity 和 Rotation。
///
/// 速度发布：UpdateVelocity 结尾调用 ActorMovement.PublishMotorVelocity()，
/// 确保 CurrentVelocity 反映 KCC 后处理（接地裁剪、切向投影）后的实际速度。
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
        if (effDt <= 0.0001f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        // 1. 读取 pre-state（通道演化前的快照，用于 ShouldForceUnground / RootMotion 判定）
        var preState = _movement.GetMovementState();

        if (preState.ShouldForceUnground)
            Motor.ForceUnground(0.1f);

        // 2. 演化通道（以当前 tick 的 KCC 地面状态为准）
        bool isGrounded = Motor.GroundingStatus.IsStableOnGround;
        _movement.StepChannels(deltaTime, isGrounded, !isGrounded);

        // 3. 读取 post-state（通道演化后的值，用于速度组合）
        var state = _movement.GetMovementState();

        // 4. Managed RootMotion：动画位移转速度，覆盖程序化速度
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

            _movement.PublishMotorVelocity(currentVelocity, isGrounded);
            return;
        }

        // 5. External 模式：程序化速度 + 地面处理
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
        _movement.PublishMotorVelocity(currentVelocity, isGrounded);
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
