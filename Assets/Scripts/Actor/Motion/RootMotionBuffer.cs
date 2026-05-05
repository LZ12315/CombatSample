using UnityEngine;

/// <summary>
/// Owns pending root position/rotation and Y dead-zone accumulation,
/// migrated from ActorMovement.
/// </summary>
public sealed class RootMotionBuffer
{
    private Vector3 _pendingPosition;
    private Quaternion _pendingRotation = Quaternion.identity;
    private float _accumulatedYDelta;

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation,
        float yDeadZone)
    {
        _pendingPosition += ApplyYDeadZone(deltaPosition, yDeadZone);
        _pendingRotation = deltaRotation * _pendingRotation;
    }

    public Vector3 PendingPosition => _pendingPosition;
    public Quaternion PendingRotation => _pendingRotation;

    public void ClearAfterMotorTick()
    {
        _pendingPosition = Vector3.zero;
        _pendingRotation = Quaternion.identity;
    }

    private Vector3 ApplyYDeadZone(Vector3 rawDelta, float yDeadZone)
    {
        float currentYDelta = rawDelta.y;
        if (Mathf.Abs(currentYDelta) < yDeadZone)
        {
            _accumulatedYDelta += currentYDelta;
            if (Mathf.Abs(_accumulatedYDelta) >= yDeadZone)
            {
                rawDelta.y = _accumulatedYDelta;
                _accumulatedYDelta = 0f;
            }
            else
            {
                rawDelta.y = 0f;
            }
        }
        else
        {
            _accumulatedYDelta = 0f;
        }

        return rawDelta;
    }
}
