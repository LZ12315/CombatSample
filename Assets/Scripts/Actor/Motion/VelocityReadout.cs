using UnityEngine;

/// <summary>
/// 对外速度读数。
/// 输入是 KCC 解算后的真实位移速度，输出给 gameplay 使用的 CurrentVelocity、
/// 水平速度和经过落地平滑的垂直速度。
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
        Vector3 characterUp,
        bool isStableGrounded,
        float deltaTime,
        float verticalSmoothTime)
    {
        if (characterUp.sqrMagnitude < 0.0001f)
            characterUp = Vector3.up;
        else
            characterUp.Normalize();

        Vector3 planarVelocity = Vector3.ProjectOnPlane(solvedVelocity, characterUp);
        float targetVertical = isStableGrounded ? 0f : Vector3.Dot(solvedVelocity, characterUp);
        _smoothedVelocityY = Mathf.SmoothDamp(
            _smoothedVelocityY,
            targetVertical,
            ref _smoothedVelocityYRef,
            verticalSmoothTime,
            float.MaxValue,
            deltaTime);

        _currentVelocity = planarVelocity + characterUp * _smoothedVelocityY;
        _currentHorizontalSpeed = planarVelocity.magnitude;
        _currentVerticalSpeed = _smoothedVelocityY;
    }
}
