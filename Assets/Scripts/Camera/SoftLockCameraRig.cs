using Cinemachine;
using UnityEngine;

/// <summary>
/// Soft-lock composition and its private world-space targets. The controller decides
/// when to refresh; leaving these targets untouched isolates a blending-out camera
/// from subsequent actor movement. Cinemachine still owns the camera's damping.
/// </summary>
internal sealed class SoftLockCameraRig
{
    internal struct Settings
    {
        public float PlayerWeight;
        public float EnemyWeight;
        public float PlayerRadius;
        public float EnemyRadius;
    }

    private Transform _followTarget;
    private Transform _playerFramingTarget;
    private Transform _enemyFramingTarget;
    private Transform _trackedEnemy;

    public CinemachineVirtualCamera Camera { get; }
    public CinemachineTargetGroup TargetGroup { get; private set; }

    public SoftLockCameraRig(CinemachineVirtualCamera camera)
    {
        Camera = camera;
        TargetGroup = LockCameraRuntimeObjects.CreateTargetGroup(
            "Runtime_LockTargetGroup", CinemachineTargetGroup.PositionMode.GroupCenter);
    }

    public void Refresh(Actor actor, Transform enemyTarget, Settings settings)
    {
        if (actor == null || enemyTarget == null || TargetGroup == null)
            return;

        // Allocate lazily as before, but do not create an unused soft-lock anchor.
        EnsureTargets(actor.transform.position);
        Transform playerRawTarget = actor.CameraTarget;
        Actor enemyActor = enemyTarget.GetComponentInParent<Actor>();
        Transform enemyRawTarget = enemyActor != null ? enemyActor.CameraTarget : enemyTarget;

        Vector3 playerPos = playerRawTarget.position;
        Vector3 enemyPos = enemyRawTarget.position;
        _playerFramingTarget.SetPositionAndRotation(playerPos, Quaternion.identity);
        _enemyFramingTarget.SetPositionAndRotation(enemyPos, Quaternion.identity);

        float groupY = ResolveGroupY(playerPos.y, enemyPos.y, settings);
        Vector3 followPos = new Vector3(playerPos.x, Mathf.Max(playerPos.y, groupY), playerPos.z);
        _followTarget.SetPositionAndRotation(followPos, Quaternion.identity);

        RefreshTargetGroup(enemyTarget, settings);
        if (Camera != null)
        {
            Camera.Follow = _followTarget;
            Camera.LookAt = TargetGroup.transform;
        }
        TargetGroup.DoUpdate();
    }

    private void EnsureTargets(Vector3 initialPosition)
    {
        if (_followTarget == null)
            _followTarget = LockCameraRuntimeObjects.CreateTransform("Runtime_SoftLockFollowTarget", initialPosition);
        if (_playerFramingTarget == null)
            _playerFramingTarget = LockCameraRuntimeObjects.CreateTransform("Runtime_SoftLockPlayerFramingTarget", initialPosition);
        if (_enemyFramingTarget == null)
            _enemyFramingTarget = LockCameraRuntimeObjects.CreateTransform("Runtime_SoftLockEnemyFramingTarget", initialPosition);
    }

    private static float ResolveGroupY(float playerY, float enemyY, Settings settings)
    {
        float playerWeight = Mathf.Max(0f, settings.PlayerWeight);
        float enemyWeight = Mathf.Max(0f, settings.EnemyWeight);
        float totalWeight = playerWeight + enemyWeight;
        if (totalWeight <= 0.0001f)
            return (playerY + enemyY) * 0.5f;
        return (playerY * playerWeight + enemyY * enemyWeight) / totalWeight;
    }

    private void RefreshTargetGroup(Transform enemyTarget, Settings settings)
    {
        var targets = TargetGroup.m_Targets;
        if (_trackedEnemy != enemyTarget
            || targets == null || targets.Length != 2
            || targets[0].target != _playerFramingTarget
            || targets[1].target != _enemyFramingTarget)
        {
            targets = new[]
            {
                new CinemachineTargetGroup.Target { target = _playerFramingTarget },
                new CinemachineTargetGroup.Target { target = _enemyFramingTarget }
            };
            _trackedEnemy = enemyTarget;
        }

        TargetGroup.gameObject.name = "Runtime_SoftLockTargetGroup";
        TargetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupAverage;
        TargetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
        TargetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
        targets[0].weight = Mathf.Max(0f, settings.PlayerWeight);
        targets[0].radius = Mathf.Max(0f, settings.PlayerRadius);
        targets[1].weight = Mathf.Max(0f, settings.EnemyWeight);
        targets[1].radius = Mathf.Max(0f, settings.EnemyRadius);
        TargetGroup.m_Targets = targets;
    }

    public void DestroyRuntime()
    {
        if (TargetGroup != null)
            LockCameraRuntimeObjects.Destroy(TargetGroup.gameObject);
        TargetGroup = null;
        LockCameraRuntimeObjects.DestroyTransform(ref _followTarget);
        LockCameraRuntimeObjects.DestroyTransform(ref _playerFramingTarget);
        LockCameraRuntimeObjects.DestroyTransform(ref _enemyFramingTarget);
        _trackedEnemy = null;
    }
}
