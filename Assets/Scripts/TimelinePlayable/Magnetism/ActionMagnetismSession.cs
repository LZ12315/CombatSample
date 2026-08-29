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
    private MotionOwner _rotationOwner;

    public ActionMagnetismSession(Actor actor, Transform combatTarget, MagnetismConfig config)
    {
        _actor = actor;
        _targetTransform = combatTarget;
        _config = config;
    }

    public void Begin()
    {
        _hasCachedHorizontalDir = false;
        TryAcquireRotationOwner();
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
        if (!TryAcquireRotationOwner()) return;

        Vector3 faceDir = dir;
        if (_config.rotationAxis == MagnetismRotationAxis.YawOnly)
            faceDir.y = 0f;

        Quaternion currentRotation = _actor.actorMotor.Motor != null && Application.isPlaying
            ? _actor.actorMotor.Motor.TransientRotation
            : _actor.transform.rotation;
        Vector3 currentForward = currentRotation * Vector3.forward;
        currentForward.y = 0f;
        if (currentForward.sqrMagnitude <= 0.000001f || faceDir.sqrMagnitude <= 0.000001f)
            return;

        currentForward.Normalize();
        faceDir.Normalize();

        float signedYaw = Vector3.SignedAngle(currentForward, faceDir, Vector3.up);
        if (_config.rotationMode != MagnetismRotationMode.InstantSnap && _config.rotationAngularSpeed > 0f)
        {
            float maxDelta = _config.rotationAngularSpeed * Mathf.Max(Time.deltaTime, 0f);
            signedYaw = Mathf.Clamp(signedYaw, -maxDelta, maxDelta);
        }

        _actor.actorMotor.SubmitScriptedRotation(
            _rotationOwner,
            Quaternion.AngleAxis(signedYaw, Vector3.up));
    }

    public void End()
    {
        _hasCachedHorizontalDir = false;
        if (_actor != null && _actor.actorMotor != null && _rotationOwner.IsValid)
            _actor.actorMotor.EndScriptedRotation(_rotationOwner);

        _rotationOwner = default;
    }

    private bool TryAcquireRotationOwner()
    {
        if (_rotationOwner.IsValid)
            return true;

        if (_actor == null || _actor.actorMotor == null)
            return false;

        return _actor.actorMotor.BeginScriptedRotation(out _rotationOwner);
    }
}
