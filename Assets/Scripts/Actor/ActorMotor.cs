using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// KCC 桥接层 — 实现 ICharacterController，在 KCC 回调中将 ActorMovement 的业务意图
/// 翻译为 KCC 所需的 BaseVelocity 和 Rotation。
///
/// 速度发布：AfterCharacterUpdate 根据 KCC 最终位移发布 solved velocity，
/// 确保 CurrentVelocity 反映 KCC 后处理（碰撞、斜坡投影、刚体交互）后的实际速度。
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor { get; private set; }
    private ActorMovement _movement;

    [SerializeField, Tooltip("碰撞过滤层掩码。仅允许与这些层上的碰撞体发生碰撞。~0 = 全部。")]
    private LayerMask _collisionMask = ~0;

    // 运行时缓存
    private Vector3 _motorFrameStartWorldPosition;
    private Vector3 _requestedVelocity;
    private bool _hasRequestedVelocity;
    private bool _forceUngroundedThisTick;
    private bool _pausedThisTick;

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
        // KCC resets transient position from transform after this callback.
        // Capture the world-frame start from transform to avoid stale transient caches.
        _motorFrameStartWorldPosition = transform.position;
        _requestedVelocity = Vector3.zero;
        _hasRequestedVelocity = false;
        _forceUngroundedThisTick = false;
        _pausedThisTick = false;
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
            _requestedVelocity = Vector3.zero;
            _hasRequestedVelocity = true;
            _pausedThisTick = true;
            return;
        }

        float effDt = deltaTime * _movement.MovementTimeScale;
        if (effDt <= 0.0001f)
        {
            currentVelocity = Vector3.zero;
            _requestedVelocity = Vector3.zero;
            _hasRequestedVelocity = true;
            _pausedThisTick = true;
            return;
        }

        // 1. 主动离地：KCC 探地已结束，本地标记当帧生效
        var preState = _movement.GetMovementState();
        if (preState.ShouldForceUnground)
        {
            Motor.ForceUnground(0.1f);
            _forceUngroundedThisTick = true;
            _movement.ApplyForcedUnground();
        }

        // 2. 演化通道（以本 tick KCC 地面状态 + 本地标记为准）
        bool isGrounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;
        _movement.StepChannels(deltaTime, isGrounded, !isGrounded);

        // 3. 只计算送给 KCC 的请求速度，不在此处发布 CurrentVelocity
        var state = _movement.GetMovementState();
        currentVelocity = ComposeKccVelocity(state, isGrounded, deltaTime);
        _requestedVelocity = currentVelocity;
        _hasRequestedVelocity = true;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (_movement == null)
            return;

        Vector3 finalVelocity = Vector3.zero;

        if (!_pausedThisTick && deltaTime > 0f)
        {
            Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
            finalVelocity = solvedDelta / deltaTime;

            if (finalVelocity.sqrMagnitude < 0.000001f && _hasRequestedVelocity)
                finalVelocity = Motor.BaseVelocity;
        }

        bool isStableGrounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;

        _movement.PublishMotorVelocity(finalVelocity, isStableGrounded);
        _movement.SignalMotorFrameEnd();
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
        if (_movement != null && Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            _movement.SignalCeilingHit();
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport) { }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    // ─────────────────── 内部计算 ───────────────────

    private Vector3 ComposeKccVelocity(ActorMovement.MovementState state, bool isGrounded, float deltaTime)
    {
        if (state.IsRootMotionManaged && state.RootMotionDelta.sqrMagnitude > 0.0001f)
        {
            Vector3 velocity = state.RootMotionDelta / deltaTime;
            if (isGrounded)
                velocity = Motor.GetDirectionTangentToSurface(velocity, Motor.GroundingStatus.GroundNormal) * velocity.magnitude;
            return velocity;
        }

        Vector3 horizontal = state.HorizontalVelocity;
        float vertical = state.VerticalVelocity;

        if (isGrounded)
        {
            horizontal = Motor.GetDirectionTangentToSurface(horizontal, Motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
            vertical = 0f;
        }

        return horizontal + Motor.CharacterUp * vertical;
    }
}
