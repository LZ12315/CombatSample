using UnityEngine;

/// <summary>
/// Locomotion 意图运行时。
/// 负责接收外部输入/AI 写入的移动意图，并在 ActorMotor.Update 中换算成 KCC 使用的水平速度。
/// </summary>
public sealed class LocomotionRuntime
{
    private LocomotionIntent _intent = LocomotionIntent.Idle;
    private bool _hasIntent;
    private bool _suppressed;
    private Vector3 _cachedVelocity;

    public LocomotionIntent Intent => _intent;
    public bool HasIntent => _hasIntent;
    public bool IsSuppressed => _suppressed;
    public Vector3 CachedVelocity => _cachedVelocity;

    public void SetIntent(in LocomotionIntent intent)
    {
        _intent = intent;
        _hasIntent = true;
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }

    public void Tick(float baseSpeed, float airControlFactor, bool isAirborne)
    {
        _cachedVelocity = ComputeVelocity(baseSpeed, airControlFactor, isAirborne);
        _hasIntent = false;
    }

    private Vector3 ComputeVelocity(float baseSpeed, float airControlFactor, bool isAirborne)
    {
        if (_suppressed || !_hasIntent)
            return Vector3.zero;

        Vector3 dir = _intent.WorldMoveDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        dir.Normalize();
        float speed = _intent.MoveStrength * baseSpeed;
        if (isAirborne)
            speed *= airControlFactor;

        return dir * speed;
    }
}
