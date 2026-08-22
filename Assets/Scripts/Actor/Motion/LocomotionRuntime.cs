using UnityEngine;

public sealed class LocomotionRuntime
{
    private LocomotionIntent _pendingIntent = LocomotionIntent.Idle;
    private LocomotionIntent _effectiveIntent = LocomotionIntent.Idle;
    private bool _hasPendingIntent;
    private bool _hasEffectiveIntent;
    private bool _suppressed;
    private Vector3 _cachedVelocity;

    public LocomotionIntent Intent => _effectiveIntent;
    public LocomotionIntent EffectiveIntent => _effectiveIntent;
    public LocomotionIntent PendingIntent => _pendingIntent;
    public bool HasIntent => _hasEffectiveIntent;
    public bool HasPendingIntent => _hasPendingIntent;
    public bool IsSuppressed => _suppressed;
    public Vector3 CachedVelocity => _cachedVelocity;

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

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }

    public void Tick(float baseSpeed, float airControlFactor, bool isAirborne)
    {
        if (_hasPendingIntent)
        {
            _effectiveIntent = _pendingIntent;
            _hasEffectiveIntent = true;
        }
        else
        {
            _effectiveIntent = LocomotionIntent.Idle;
            _hasEffectiveIntent = false;
        }

        _cachedVelocity = ComputeVelocity(baseSpeed, airControlFactor, isAirborne);
        ClearPendingIntent();
    }

    private Vector3 ComputeVelocity(float baseSpeed, float airControlFactor, bool isAirborne)
    {
        if (_suppressed || !_hasEffectiveIntent)
            return Vector3.zero;

        Vector3 dir = _effectiveIntent.WorldMoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        dir.Normalize();
        float speed = _effectiveIntent.MoveStrength * baseSpeed;
        if (isAirborne)
            speed *= airControlFactor;

        return dir * speed;
    }
}
