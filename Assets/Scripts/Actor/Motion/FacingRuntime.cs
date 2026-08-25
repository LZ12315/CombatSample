using UnityEngine;

/// <summary>
/// E3-B compatibility facing adapter。
/// 负责维护现有外部覆盖朝向、瞬时 Snap 与 locomotion rotation contribution；
/// 最终 requested rotation 由 RotationDomain 产出。
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

    public void SyncTo(Quaternion rotation)
    {
        _targetRotation = rotation;
        _pendingRotation = rotation;
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

/// <summary>
/// ActorMotor 的 Rotation Domain。
/// 固定仲裁：Scripted Rotation > Root Rotation > Locomotion Rotation。
/// 所有 temporary rotation owner 都提交本 Tick 的 local yaw delta。
/// </summary>
public sealed class RotationDomain
{
    public Quaternion RequestedRotation { get; private set; } = Quaternion.identity;

    public void Prepare(
        Quaternion tickStartRotation,
        Quaternion locomotionRotation,
        bool hasScriptedRotation,
        Quaternion scriptedLocalYawDelta,
        bool hasRootRotation,
        Quaternion rootLocalYawDelta)
    {
        if (hasScriptedRotation)
        {
            RequestedRotation = tickStartRotation * scriptedLocalYawDelta;
            return;
        }

        if (hasRootRotation)
        {
            RequestedRotation = tickStartRotation * rootLocalYawDelta;
            return;
        }

        RequestedRotation = locomotionRotation;
    }
}
