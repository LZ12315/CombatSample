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
            _cachedVelocity, targetVelocity, config.Acceleration, config.Deceleration,
            config.DirectionResponse, deltaTime);
    }

    /// <summary>Continuous linear braking prediction, in world metres under the current policy.
    /// Movement time scale changes the duration, not the integrated stopping distance.</summary>
    public static bool TryPredictStoppingDistance(float modelSpeed, float deceleration, float locomotionScale,
        out float distance)
    {
        distance = 0f;
        if (!LocomotionDataValidation.IsFinite(modelSpeed) || modelSpeed < 0f
            || !LocomotionDataValidation.IsFinite(deceleration) || deceleration <= 0f
            || !LocomotionDataValidation.IsFinite(locomotionScale) || locomotionScale < 0f) return false;
        distance = modelSpeed * modelSpeed / (2f * deceleration) * locomotionScale;
        return LocomotionDataValidation.IsFinite(distance);
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
        float acceleration, float deceleration, float directionResponse, float deltaTime)
    {
        // No-input braking keeps the constant deceleration used by stop-distance prediction.
        if (target.sqrMagnitude == 0f)
            return Vector3.MoveTowards(current, Vector3.zero, deceleration * deltaTime);

        // The same steering rule applies at every angle. Opposing velocities can
        // cancel naturally, so reversals never need an arbitrary rotation axis.
        float steering = 1f - Mathf.Exp(-directionResponse * deltaTime);
        current = Vector3.Lerp(current, target.normalized * current.magnitude, steering);

        float rate = target.sqrMagnitude < current.sqrMagnitude ? deceleration : acceleration;
        return Vector3.MoveTowards(current, target, rate * deltaTime);
    }
}
