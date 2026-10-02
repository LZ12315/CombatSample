using UnityEngine;

/// <summary>
/// ActorLocomotion-owned motion producer. Intent lifetime is managed by ActorLocomotion.
/// </summary>
public sealed class LocomotionRunner
{
    private LocomotionIntent _effectiveIntent = LocomotionIntent.Idle;
    private bool _hasEffectiveIntent;
    private Vector3 _cachedVelocity;

    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _pendingRotation = Quaternion.identity;

    public LocomotionIntent EffectiveIntent => _effectiveIntent;
    public Vector3 CachedVelocity => _cachedVelocity;
    public Quaternion PendingRotation => _pendingRotation;
    public float TargetRotationYaw => _targetRotation.eulerAngles.y;

    public void Initialize(Quaternion initialRotation)
    {
        _targetRotation = initialRotation;
        _pendingRotation = initialRotation;
    }

    public void SyncRotation(Quaternion rotation)
    {
        _targetRotation = rotation;
        _pendingRotation = rotation;
    }

    public void ClearIntent()
    {
        _effectiveIntent = LocomotionIntent.Idle;
        _hasEffectiveIntent = false;
        _cachedVelocity = Vector3.zero;
    }

    public void Prepare(
        in LocomotionIntent intent,
        bool hasIntent,
        float deltaTime,
        in LocomotionMovementConfig movementConfig)
    {
        if (deltaTime <= 0f)
            return;

        _effectiveIntent = hasIntent ? intent : LocomotionIntent.Idle;
        _hasEffectiveIntent = hasIntent;

        LocomotionMovementConfig config = movementConfig.Sanitize();
        UpdateRotation(intent, hasIntent, deltaTime, config);
        Vector3 targetVelocity = ComputeTargetVelocity(config);
        _cachedVelocity = IntegrateVelocity(
            _cachedVelocity, targetVelocity, config.Acceleration, config.Deceleration, deltaTime);
    }

    private void UpdateRotation(in LocomotionIntent intent, bool hasIntent, float deltaTime,
        in LocomotionMovementConfig config)
    {
        Vector3 face = hasIntent ? intent.FacingDirection : Vector3.zero;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            _targetRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }
        else
        {
            _targetRotation = _pendingRotation;
            return;
        }

        float angle = Quaternion.Angle(_pendingRotation, _targetRotation);
        float responseStep = config.TurnResponseTime > 0f
            ? angle * (1f - Mathf.Exp(-deltaTime / config.TurnResponseTime))
            : angle;
        float cappedStep = Mathf.Min(responseStep, config.RotateSpeed * deltaTime);
        _pendingRotation = Quaternion.RotateTowards(_pendingRotation, _targetRotation, cappedStep);
    }

    private Vector3 ComputeTargetVelocity(in LocomotionMovementConfig config)
    {
        if (!_hasEffectiveIntent)
            return Vector3.zero;

        Vector3 dir = _effectiveIntent.WorldMoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        dir.Normalize();
        return dir * (_effectiveIntent.MoveStrength * config.MaxSpeed);
    }

    private static Vector3 IntegrateVelocity(Vector3 current, Vector3 target,
        float acceleration, float deceleration, float deltaTime)
    {
        if (Vector3.Dot(current, target) < 0f)
        {
            float currentSpeed = current.magnitude;
            float brakingTime = currentSpeed / deceleration;
            if (deltaTime <= brakingTime)
                return Vector3.MoveTowards(current, Vector3.zero, deceleration * deltaTime);
            return Vector3.MoveTowards(Vector3.zero, target, acceleration * (deltaTime - brakingTime));
        }

        float rate = target.sqrMagnitude < current.sqrMagnitude ? deceleration : acceleration;
        return Vector3.MoveTowards(current, target, rate * deltaTime);
    }
}
