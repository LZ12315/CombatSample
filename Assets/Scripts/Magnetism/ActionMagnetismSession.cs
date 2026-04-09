using UnityEngine;

/// <summary>
/// 单段 Magnetism Clip 运行时：<strong>仅旋转</strong>（+ maxDistance 门控）。
/// 由 <see cref="ActionMagnetismBehavior"/> 驱动。
/// </summary>
public sealed class ActionMagnetismSession
{
    private readonly Actor _actor;
    private readonly Transform _targetTransform;
    private readonly MagnetismConfig _config;

    private Vector3 _cachedHorizontalDir = Vector3.forward;
    private bool _hasCachedHorizontalDir;

    public ActionMagnetismSession(Actor actor, Transform combatTarget, MagnetismConfig config)
    {
        _actor = actor;
        _targetTransform = combatTarget;
        _config = config;
    }

    public void Begin()
    {
        _hasCachedHorizontalDir = false;
    }

    public void Tick()
    {
        if (_targetTransform == null || _actor == null || _config == null) return;

        Vector3 toTarget = _targetTransform.position - _actor.transform.position;
        toTarget.y = 0f;
        float horizontalDistance = toTarget.magnitude;

        if (_config.maxDistance > 0f && horizontalDistance > _config.maxDistance)
        {
            if (_config.debugLog)
                Debug.Log(
                    $"[Magnetism] Skip rotate: horizontalDistance={horizontalDistance:F3} > maxDistance={_config.maxDistance:F3}");
            return;
        }

        Vector3 dir;
        const float eps = 0.000001f;
        if (horizontalDistance > eps)
        {
            dir = toTarget / horizontalDistance;
            _cachedHorizontalDir = dir;
            _hasCachedHorizontalDir = true;
        }
        else
        {
            if (!_hasCachedHorizontalDir) return;
            dir = _cachedHorizontalDir;
        }

        if (!_config.rotateToTarget || _config.rotationMode == MagnetismRotationMode.None) return;
        if (_actor.movement == null) return;

        Vector3 faceDir = dir;
        if (_config.rotationAxis == MagnetismRotationAxis.YawOnly)
            faceDir.y = 0f;

        if (_config.rotationMode == MagnetismRotationMode.InstantSnap || _config.rotationAngularSpeed <= 0f)
            _actor.movement.SnapFacing(faceDir);
        else
        {
            _actor.movement.SetFacingOverride(faceDir, _config.rotationAngularSpeed);
        }
    }

    public void End()
    {
        _hasCachedHorizontalDir = false;
        _actor?.movement?.ClearFacingOverride();
    }
}
