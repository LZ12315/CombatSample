using Cinemachine;
using UnityEngine;

/// <summary>
/// Hard-lock anchor, composition and smoothing state. Intentionally keeps the
/// existing controller-transform / enemy-root targeting, distinct from SoftLock.
/// </summary>
internal sealed class HardLockCameraRig
{
    internal struct Settings
    {
        public float HeightOffset;
        public float PositionSmoothTime;
        public float RotationSmoothTime;
        public float FramingSize;
        public float TargetPadding;
        public float CenterBias;
        public float Fov;
        public float BaseFollowDistance;
        public float DistancePerCombatMeter;
        public float MaxFollowDistance;
        public float PlayerWeight;
        public float EnemyWeight;
    }

    private Transform _anchor;
    private Vector3 _positionVelocity;
    private float _anchorYaw;
    private float _yawVelocity;

    public CinemachineVirtualCamera Camera { get; }
    public CinemachineTargetGroup TargetGroup { get; private set; }
    public float FollowDistance { get; private set; } = 8f;

    public HardLockCameraRig(CinemachineVirtualCamera camera, Vector3 initialPosition)
    {
        Camera = camera;
        _anchor = LockCameraRuntimeObjects.CreateTransform("Runtime_LockAnchor", initialPosition);
        TargetGroup = LockCameraRuntimeObjects.CreateTargetGroup(
            "Runtime_LockTargetGroup", CinemachineTargetGroup.PositionMode.GroupCenter);
    }

    public void InitializeAnchor(Vector3 position)
    {
        if (_anchor != null)
            _anchor.SetPositionAndRotation(position, Quaternion.identity);
    }

    public void Refresh(Transform playerTarget, Transform enemyTarget, Settings settings, bool instant = false)
    {
        if (playerTarget == null || enemyTarget == null || _anchor == null || TargetGroup == null)
            return;

        Vector3 playerPos = playerTarget.position;
        Vector3 enemyPos = enemyTarget.position;
        Vector3 toEnemy = new Vector3(enemyPos.x - playerPos.x, 0f, enemyPos.z - playerPos.z);
        float distance = toEnemy.magnitude;
        Vector3 combatDir = distance > 0.01f ? toEnemy / distance : Vector3.forward;
        Vector3 center = Vector3.Lerp(playerPos, enemyPos, settings.CenterBias);
        center.y = (playerPos.y + enemyPos.y) * 0.5f + settings.HeightOffset;

        if (instant)
        {
            _anchor.position = center;
            _positionVelocity = Vector3.zero;
        }
        else
        {
            _anchor.position = Vector3.SmoothDamp(
                _anchor.position, center, ref _positionVelocity,
                Mathf.Max(0.001f, settings.PositionSmoothTime));
        }

        FollowDistance = Mathf.Min(
            settings.BaseFollowDistance + distance * settings.DistancePerCombatMeter,
            settings.MaxFollowDistance);
        float desiredYaw = Mathf.Atan2(combatDir.x, combatDir.z) * Mathf.Rad2Deg;
        if (instant)
        {
            _anchorYaw = desiredYaw;
            _yawVelocity = 0f;
        }
        else
        {
            _anchorYaw = Mathf.SmoothDampAngle(
                _anchorYaw, desiredYaw, ref _yawVelocity,
                Mathf.Max(0.001f, settings.RotationSmoothTime));
        }
        _anchor.rotation = Quaternion.Euler(0f, _anchorYaw, 0f);

        ConfigureCamera(settings);
        RefreshTargetGroup(playerTarget, enemyTarget, settings);
        if (Camera != null)
        {
            Camera.LookAt = TargetGroup.transform;
            Camera.Follow = _anchor;
        }
        TargetGroup.DoUpdate();
    }

    private void ConfigureCamera(Settings settings)
    {
        if (Camera == null) return;

        var transposer = Camera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = new Vector3(0f, 0f, -FollowDistance);
        }

        var composer = Camera.GetCinemachineComponent<CinemachineGroupComposer>();
        if (composer != null)
        {
            composer.m_GroupFramingSize = settings.FramingSize;
            composer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyThenZoom;
            composer.m_MaximumFOV = Mathf.Max(settings.Fov, 68f);
            composer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
        }

        var lens = Camera.m_Lens;
        lens.FieldOfView = settings.Fov;
        Camera.m_Lens = lens;
    }

    private void RefreshTargetGroup(Transform playerTarget, Transform enemyTarget, Settings settings)
    {
        var targets = TargetGroup.m_Targets;
        if (targets == null || targets.Length != 2
            || targets[0].target != playerTarget || targets[1].target != enemyTarget)
        {
            targets = new[]
            {
                new CinemachineTargetGroup.Target { target = playerTarget },
                new CinemachineTargetGroup.Target { target = enemyTarget }
            };
        }
        targets[0].weight = settings.PlayerWeight;
        targets[0].radius = settings.TargetPadding;
        targets[1].weight = settings.EnemyWeight;
        targets[1].radius = settings.TargetPadding;
        TargetGroup.m_Targets = targets;
    }

    public void DestroyRuntime()
    {
        if (TargetGroup != null)
            LockCameraRuntimeObjects.Destroy(TargetGroup.gameObject);
        TargetGroup = null;
        LockCameraRuntimeObjects.DestroyTransform(ref _anchor);
    }
}
