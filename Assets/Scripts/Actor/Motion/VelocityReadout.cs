using UnityEngine;

/// <summary>
/// Owns CurrentVelocity / horizontal / vertical speed and vertical smoothing,
/// migrated from ActorMovement.PublishMotorVelocity.
/// </summary>
public sealed class VelocityReadout
{
    private Vector3 _currentVelocity;
    private float _currentHorizontalSpeed;
    private float _currentVerticalSpeed;
    private float _smoothedVelocityY;
    private float _smoothedVelocityYRef;

    public Vector3 CurrentVelocity => _currentVelocity;
    public float CurrentHorizontalSpeed => _currentHorizontalSpeed;
    public float CurrentVerticalSpeed => _currentVerticalSpeed;

    public void Publish(
        Vector3 solvedVelocity,
        bool isStableGrounded,
        float deltaTime,
        float verticalSmoothTime)
    {
        float targetY = isStableGrounded ? 0f : solvedVelocity.y;
        _smoothedVelocityY = Mathf.SmoothDamp(
            _smoothedVelocityY,
            targetY,
            ref _smoothedVelocityYRef,
            verticalSmoothTime,
            float.MaxValue,
            deltaTime);

        _currentVelocity = new Vector3(
            solvedVelocity.x,
            _smoothedVelocityY,
            solvedVelocity.z);
        _currentHorizontalSpeed = new Vector2(
            solvedVelocity.x,
            solvedVelocity.z).magnitude;
        _currentVerticalSpeed = _smoothedVelocityY;
    }
}
