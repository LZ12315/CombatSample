using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Cinemachine Proxy")]
    [Tooltip("FollowProxy 偏向敌人的比例。值越低越跟玩家；值越高越像双目标镜头。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockFollowEnemyBias = 0.18f;
    [Tooltip("AimProxy 偏向敌人的比例。用于保证敌人可见，但不应过高，否则会变硬锁。")]
    [Range(0f, 0.65f)]
    [SerializeField] private float softLockAimEnemyBias = 0.34f;
    [Tooltip("FollowProxy 水平平滑时间。值越大，镜头站位越稳、越不着急追。")]
    [SerializeField] private float softLockFollowSmoothTime = 0.45f;
    [Tooltip("AimProxy 平滑时间。值越大，镜头朝向越稳。")]
    [SerializeField] private float softLockAimSmoothTime = 0.22f;
    [Tooltip("AimProxy 额外高度偏移，通常让镜头看向玩家胸口附近。")]
    [SerializeField] private float softLockAimHeightOffset = 0.95f;

    [Header("Soft Lock - Lens")]
    [Tooltip("软锁定固定跟随距离。动态 FOV/距离会在后续阶段处理。")]
    [SerializeField] private float softLockFollowDistance = 6f;
    [Tooltip("软锁定固定 FOV。动态 FOV 会在后续阶段处理。")]
    [SerializeField] private float softLockFov = 45f;
    [Tooltip("软锁定目标组构图大小。")]
    [Range(0.45f, 0.9f)]
    [SerializeField] private float softLockFramingSize = 0.72f;
    [Tooltip("软锁定目标组基础边距。")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float softLockTargetPadding = 1.0f;

    [Header("Soft Lock - Composer Profile")]
    [Tooltip("Composer 屏幕水平死区。目标在死区内时，Cinemachine 不主动修正旋转。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockComposerDeadZoneWidth = 0.16f;
    [Tooltip("Composer 屏幕垂直死区。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockComposerDeadZoneHeight = 0.12f;
    [Tooltip("Composer 屏幕水平软区。目标进入软区后，Cinemachine 使用 damping 缓慢修正。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockComposerSoftZoneWidth = 0.82f;
    [Tooltip("Composer 屏幕垂直软区。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockComposerSoftZoneHeight = 0.76f;
    [Tooltip("Composer 水平 damping。值越大，横向修正越不着急。")]
    [SerializeField] private float softLockComposerHorizontalDamping = 1.15f;
    [Tooltip("Composer 垂直 damping。值越大，上下修正越平稳。")]
    [SerializeField] private float softLockComposerVerticalDamping = 1.3f;
    [Tooltip("AimProxy 在屏幕中的目标 X。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockComposerScreenX = 0.5f;
    [Tooltip("AimProxy 在屏幕中的目标 Y。略高于中心可看到更多地面和角色。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockComposerScreenY = 0.55f;

    private SoftLockComposer _softLockComposer;
    private SoftLockComposer SoftComposer
    {
        get
        {
            if (_softLockComposer == null)
                _softLockComposer = new SoftLockComposer(this);
            return _softLockComposer;
        }
    }

    private sealed class SoftLockComposer
    {
        private readonly ActorCameraControl _o;

        public SoftLockComposer(ActorCameraControl owner)
        {
            _o = owner;
        }

        public void OnEnter(LockCameraRigRuntime rt, Transform enemyTarget)
        {
            Refresh(rt, enemyTarget, instant: true, updateStickySide: true);
        }

        public void Refresh(
            LockCameraRigRuntime rt,
            Transform enemyTarget,
            bool instant,
            bool updateStickySide)
        {
            if (rt == null || rt.anchor == null || enemyTarget == null || _o.actor == null)
                return;

            rt.CreateAimProxy(_o.transform);
            if (rt.aimProxy == null) return;

            SoftLockFrame frame = BuildFrame(enemyTarget);
            Vector3 followTarget = ResolveFollowProxyPosition(frame);
            Vector3 aimTarget = ResolveAimProxyPosition(frame);

            if (instant)
            {
                rt.anchor.position = followTarget;
                rt.aimProxy.position = aimTarget;
                rt.anchorPositionVelocity = Vector3.zero;
                rt.aimProxyVelocity = Vector3.zero;
            }
            else
            {
                rt.anchor.position = Vector3.SmoothDamp(
                    rt.anchor.position,
                    followTarget,
                    ref rt.anchorPositionVelocity,
                    Mathf.Max(0.001f, _o.softLockFollowSmoothTime));

                rt.aimProxy.position = Vector3.SmoothDamp(
                    rt.aimProxy.position,
                    aimTarget,
                    ref rt.aimProxyVelocity,
                    Mathf.Max(0.001f, _o.softLockAimSmoothTime));
            }

            rt.currentFollowDistance = Mathf.Max(0.01f, _o.softLockFollowDistance);

            CaptureDiagnostics(rt, frame, followTarget, aimTarget);
            RefreshTargetGroup(rt, enemyTarget);
            ApplyCinemachineSettings(rt);
        }

        private struct SoftLockFrame
        {
            public Vector3 PlayerPos;
            public Vector3 EnemyPos;
            public Vector3 PlayerXZ;
            public Vector3 EnemyXZ;
            public Vector3 CombatDir;
            public Vector3 CombatRight;
            public float Distance;
        }

        private SoftLockFrame BuildFrame(Transform enemyTarget)
        {
            Vector3 playerPos = _o.transform.position;
            Vector3 enemyPos = enemyTarget.position;
            Vector3 playerXZ = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 enemyXZ = new Vector3(enemyPos.x, 0f, enemyPos.z);

            Vector3 toEnemy = enemyXZ - playerXZ;
            float distance = toEnemy.magnitude;
            Vector3 combatDir;
            if (distance > 0.01f)
            {
                combatDir = toEnemy / distance;
            }
            else
            {
                Camera cam = Camera.main;
                combatDir = cam != null ? cam.transform.forward : _o.transform.forward;
                combatDir.y = 0f;
                combatDir = combatDir.sqrMagnitude > 0.0001f ? combatDir.normalized : Vector3.forward;
            }

            Vector3 combatRight = Vector3.Cross(Vector3.up, combatDir).normalized;

            return new SoftLockFrame
            {
                PlayerPos = playerPos,
                EnemyPos = enemyPos,
                PlayerXZ = playerXZ,
                EnemyXZ = enemyXZ,
                CombatDir = combatDir,
                CombatRight = combatRight,
                Distance = distance
            };
        }

        private Vector3 ResolveFollowProxyPosition(SoftLockFrame frame)
        {
            Vector3 followXZ = Vector3.Lerp(
                frame.PlayerXZ,
                frame.EnemyXZ,
                Mathf.Clamp01(_o.softLockFollowEnemyBias));

            Vector3 follow = followXZ;
            follow.y = frame.PlayerPos.y + _o.heightOffset;
            return follow;
        }

        private Vector3 ResolveAimProxyPosition(SoftLockFrame frame)
        {
            Vector3 aimXZ = Vector3.Lerp(
                frame.PlayerXZ,
                frame.EnemyXZ,
                Mathf.Clamp01(_o.softLockAimEnemyBias));

            Vector3 aim = aimXZ;
            aim.y = frame.PlayerPos.y + _o.softLockAimHeightOffset;
            return aim;
        }

        private void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget)
        {
            if (rt.targetGroup == null) return;

            Transform lockTarget = enemyTarget;
            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != lockTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != _o.transform
                || targets[1].target != lockTarget;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target
                    {
                        target = _o.transform,
                        weight = _o.lockPlayerWeight,
                        radius = _o.softLockTargetPadding
                    },
                    new CinemachineTargetGroup.Target
                    {
                        target = lockTarget,
                        weight = _o.lockEnemyWeight,
                        radius = _o.softLockTargetPadding
                    }
                };

                rt.trackedState = Enums.PlayerCameraState.SoftLock;
                rt.trackedLockTarget = lockTarget;
                rt.targetGroupDirty = false;
            }

            targets = rt.targetGroup.m_Targets;
            if (targets != null && targets.Length == 2)
            {
                targets[0].weight = _o.lockPlayerWeight;
                targets[0].radius = _o.softLockTargetPadding;
                targets[1].weight = _o.lockEnemyWeight;
                targets[1].radius = _o.softLockTargetPadding;
            }
        }

        private void ApplyCinemachineSettings(LockCameraRigRuntime rt)
        {
            CinemachineVirtualCamera vcam = _o.softLockCamera;
            if (vcam == null) return;

            CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
                transposer.m_FollowOffset = new Vector3(0f, 0f, -rt.currentFollowDistance);
                transposer.m_XDamping = 1.2f;
                transposer.m_YDamping = 1.4f;
                transposer.m_ZDamping = 1.0f;
            }

            var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
                composer.m_ScreenX = _o.softLockComposerScreenX;
                composer.m_ScreenY = _o.softLockComposerScreenY;
                composer.m_DeadZoneWidth = _o.softLockComposerDeadZoneWidth;
                composer.m_DeadZoneHeight = _o.softLockComposerDeadZoneHeight;
                composer.m_SoftZoneWidth = _o.softLockComposerSoftZoneWidth;
                composer.m_SoftZoneHeight = _o.softLockComposerSoftZoneHeight;
                composer.m_HorizontalDamping = _o.softLockComposerHorizontalDamping;
                composer.m_VerticalDamping = _o.softLockComposerVerticalDamping;
                composer.m_CenterOnActivate = false;
            }

            var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
            if (groupComposer != null)
            {
                groupComposer.m_GroupFramingSize = _o.softLockFramingSize;
                groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyOnly;
                groupComposer.m_MaximumFOV = Mathf.Max(_o.softLockFov, 60f);
                groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
                groupComposer.m_ScreenX = _o.softLockComposerScreenX;
                groupComposer.m_ScreenY = _o.softLockComposerScreenY;
                groupComposer.m_DeadZoneWidth = _o.softLockComposerDeadZoneWidth;
                groupComposer.m_DeadZoneHeight = _o.softLockComposerDeadZoneHeight;
                groupComposer.m_SoftZoneWidth = _o.softLockComposerSoftZoneWidth;
                groupComposer.m_SoftZoneHeight = _o.softLockComposerSoftZoneHeight;
                groupComposer.m_HorizontalDamping = _o.softLockComposerHorizontalDamping;
                groupComposer.m_VerticalDamping = _o.softLockComposerVerticalDamping;
                groupComposer.m_CenterOnActivate = false;
            }

            var framingTransposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framingTransposer != null)
            {
                framingTransposer.m_GroupFramingSize = _o.softLockFramingSize;
                framingTransposer.m_AdjustmentMode = CinemachineFramingTransposer.AdjustmentMode.DollyOnly;
                framingTransposer.m_MaximumFOV = Mathf.Max(_o.softLockFov, 60f);
            }

            var lens = vcam.m_Lens;
            lens.FieldOfView = _o.softLockFov;
            vcam.m_Lens = lens;
        }

        private static void CaptureDiagnostics(
            LockCameraRigRuntime rt,
            SoftLockFrame frame,
            Vector3 followTarget,
            Vector3 aimTarget)
        {
            rt.dbgLabel = "SoftLockProxy";
            rt.dbgCombatCenter = aimTarget;
            rt.dbgCombatDir = frame.CombatDir;
            rt.dbgCombatDist = frame.Distance;
            rt.dbgRawSide = 0f;
            rt.dbgSideAmount = 0f;
            rt.dbgDesiredAnchorPos = followTarget;
            rt.dbgYawSource = "CinemachineComposer";
            rt.dbgSectorZone = "Proxy";
            rt.dbgTrend = "screen-space";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}
