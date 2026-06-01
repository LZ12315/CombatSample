using UnityEngine;

/// <summary>
/// 朝向意图运行时。
/// 负责维护外部覆盖朝向、瞬时 Snap，以及由 locomotion 意图驱动的待应用旋转。
/// </summary>
public sealed class FacingRuntime
{
    private Vector3 _overrideFacingDirection;
    private bool _hasFacingOverride;
    private float _overrideAngularSpeed = -1f;

    private Quaternion _targetRotation = Quaternion.identity;
    private Quaternion _pendingRotation = Quaternion.identity;

    public Quaternion PendingRotation => _pendingRotation;

    public bool HasFacingOverride => _hasFacingOverride;
    public float TargetRotationYaw => _targetRotation.eulerAngles.y;
    public Vector3 OverrideDirection => _overrideFacingDirection;

    public void Initialize(Quaternion initialRotation)
    {
        _targetRotation = initialRotation;
        _pendingRotation = initialRotation;
    }

    public void SetOverride(Vector3 worldDirection, float angularSpeed = -1f)
    {
        if (worldDirection.sqrMagnitude < 0.001f)
            return;

        _overrideFacingDirection = worldDirection;
        _hasFacingOverride = true;
        _overrideAngularSpeed = angularSpeed;
    }

    public void ClearOverride()
    {
        _hasFacingOverride = false;
        _overrideAngularSpeed = -1f;
    }

    public void Snap(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.001f)
            return;

        _targetRotation = Quaternion.LookRotation(worldDirection, Vector3.up);
        _pendingRotation = _targetRotation;
    }

    public void Tick(
        float deltaTime,
        float defaultAngularSpeed,
        in LocomotionIntent intent,
        bool hasLocomotionIntent,
        bool locomotionSuppressed)
    {
        if (_hasFacingOverride)
        {
            _targetRotation = Quaternion.LookRotation(_overrideFacingDirection, Vector3.up);
        }
        else if (!locomotionSuppressed && hasLocomotionIntent)
        {
            Vector3 face = intent.FacingDirection;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f)
            {
                face = intent.WorldMoveDirection;
                face.y = 0f;
            }

            if (face.sqrMagnitude > 0.0001f)
                _targetRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }

        float angularSpeed = (_hasFacingOverride && _overrideAngularSpeed >= 0f)
            ? _overrideAngularSpeed
            : defaultAngularSpeed;

        _pendingRotation = Quaternion.RotateTowards(
            _pendingRotation,
            _targetRotation,
            angularSpeed * deltaTime);
    }
}
