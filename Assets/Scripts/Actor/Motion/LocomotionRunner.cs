using UnityEngine;

/// <summary>
/// ActorMotor-owned locomotion producer. It consumes the pending intent exactly once per
/// motion tick and exposes the effective contribution for Translation and Rotation.
/// </summary>
public sealed class LocomotionRunner
{
    private LocomotionIntent _pendingIntent = LocomotionIntent.Idle;
    private LocomotionIntent _effectiveIntent = LocomotionIntent.Idle;
    private bool _hasPendingIntent;
    private bool _hasEffectiveIntent;
    private Vector3 _cachedVelocity;

    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _pendingRotation = Quaternion.identity;

    public LocomotionIntent Intent => _effectiveIntent;
    public LocomotionIntent EffectiveIntent => _effectiveIntent;
    public LocomotionIntent PendingIntent => _pendingIntent;
    public bool HasIntent => _hasEffectiveIntent;
    public bool HasPendingIntent => _hasPendingIntent;
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

    public void SetIntent(in LocomotionIntent intent)
    {
        _pendingIntent = intent;
        _hasPendingIntent = true;
    }

    public void ClearPendingIntent()
    {
        _pendingIntent = LocomotionIntent.Idle;
        _hasPendingIntent = false;
    }

    public void Prepare(
        float deltaTime,
        float baseSpeed,
        float airControlFactor,
        float rotateSpeed,
        bool isAirborne,
        float locomotionScale,
        float airLocomotionScale)
    {
        if (_hasPendingIntent)
        {
            _effectiveIntent = _pendingIntent;
            _hasEffectiveIntent = true;
        }
        else if (deltaTime > 0f)
        {
            _effectiveIntent = LocomotionIntent.Idle;
            _hasEffectiveIntent = false;
        }

        UpdateRotation(deltaTime, rotateSpeed);
        _cachedVelocity = ComputeVelocity(baseSpeed, airControlFactor, isAirborne, locomotionScale, airLocomotionScale);

        if (deltaTime > 0f)
            ClearPendingIntent();
    }

    private void UpdateRotation(float deltaTime, float rotateSpeed)
    {
        if (_hasPendingIntent)
        {
            Vector3 face = _pendingIntent.FacingDirection;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f)
            {
                face = _pendingIntent.WorldMoveDirection;
                face.y = 0f;
            }

            if (face.sqrMagnitude > 0.0001f)
                _targetRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }

        _pendingRotation = Quaternion.RotateTowards(
            _pendingRotation,
            _targetRotation,
            Mathf.Max(0f, rotateSpeed) * Mathf.Max(0f, deltaTime));
    }

    private Vector3 ComputeVelocity(
        float baseSpeed,
        float airControlFactor,
        bool isAirborne,
        float locomotionScale,
        float airLocomotionScale)
    {
        if (!_hasEffectiveIntent)
            return Vector3.zero;

        Vector3 dir = _effectiveIntent.WorldMoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        dir.Normalize();
        float speed = _effectiveIntent.MoveStrength * Mathf.Max(0f, baseSpeed) * Mathf.Clamp01(locomotionScale);
        if (isAirborne)
            speed *= Mathf.Clamp01(airControlFactor) * Mathf.Clamp01(airLocomotionScale);

        return dir * speed;
    }
}
