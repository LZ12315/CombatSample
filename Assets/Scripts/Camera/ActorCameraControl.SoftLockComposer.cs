using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Pose")]
    [Tooltip("软锁定战斗中心偏向敌人的比例。值越低越贴近玩家；值越高越像双目标构图。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockCenterBias = 0.3f;
    [Tooltip("软锁定斜视侧向偏移。值越大，玩家与敌人越不容易在画面中重叠。")]
    [SerializeField] private float softLockSideOffset = 1.5f;
    [Tooltip("玩家越过该侧向阈值后，软锁定相机才切换斜视侧。")]
    [SerializeField] private float softLockSideSwitchThreshold = 0.6f;

    [Header("Soft Lock - Damping")]
    [Tooltip("软锁定 anchor 位置阻尼。值越大，相机跟随越慢、画面越稳。")]
    [SerializeField] private float softLockAnchorSmoothTime = 0.35f;
    [Tooltip("软锁定 yaw 阻尼。值越大，相机转向越慢、越稳定。")]
    [SerializeField] private float softLockYawSmoothTime = 0.35f;
    [Tooltip("软锁定左右斜视侧切换的平滑时间。")]
    [SerializeField] private float softLockSideSmoothTime = 0.45f;

    [Header("Soft Lock - Lens")]
    [Tooltip("软锁定固定跟随距离。动态 FOV/距离会在后续阶段处理。")]
    [SerializeField] private float softLockFollowDistance = 6f;
    [Tooltip("软锁定固定 FOV。动态 FOV 会在后续阶段处理。")]
    [SerializeField] private float softLockFov = 45f;
    [Tooltip("软锁定目标组构图大小。")]
    [Range(0.45f, 0.9f)]
    [SerializeField] private float softLockFramingSize = 0.68f;
    [Tooltip("软锁定目标组基础边距。")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float softLockTargetPadding = 1.2f;

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

            SoftLockFrame frame = BuildFrame(enemyTarget);
            float rawSide = ReadCameraSide(frame);
            float sideValue = ResolveStickySide(rt, rawSide, instant, updateStickySide);

            Vector3 desiredAnchorPos = ResolveAnchorPosition(frame, sideValue);
            float desiredYaw = ResolveAnchorYaw(frame, sideValue);

            if (instant)
            {
                rt.anchor.position = desiredAnchorPos;
                rt.currentAnchorYaw = desiredYaw;
                rt.anchorPositionVelocity = Vector3.zero;
                rt.anchorYawVelocity = 0f;
            }
            else
            {
                rt.anchor.position = Vector3.SmoothDamp(
                    rt.anchor.position,
                    desiredAnchorPos,
                    ref rt.anchorPositionVelocity,
                    Mathf.Max(0.001f, _o.softLockAnchorSmoothTime));

                rt.currentAnchorYaw = Mathf.SmoothDampAngle(
                    rt.currentAnchorYaw,
                    desiredYaw,
                    ref rt.anchorYawVelocity,
                    Mathf.Max(0.001f, _o.softLockYawSmoothTime));
            }

            rt.currentFollowDistance = Mathf.Max(0.01f, _o.softLockFollowDistance);
            rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);

            CaptureDiagnostics(rt, frame, rawSide, sideValue, desiredAnchorPos, desiredYaw);
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
            public Vector3 Center;
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

            Vector3 centerXZ = Vector3.Lerp(
                playerXZ,
                enemyXZ,
                Mathf.Clamp01(_o.softLockCenterBias));
            Vector3 center = centerXZ;
            center.y = playerPos.y + _o.heightOffset;

            return new SoftLockFrame
            {
                PlayerPos = playerPos,
                EnemyPos = enemyPos,
                PlayerXZ = playerXZ,
                EnemyXZ = enemyXZ,
                CombatDir = combatDir,
                CombatRight = combatRight,
                Center = center,
                Distance = distance
            };
        }

        private static float ReadCameraSide(SoftLockFrame frame)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return 0f;

            Vector3 playerToCamera = mainCam.transform.position - frame.PlayerPos;
            playerToCamera.y = 0f;
            if (playerToCamera.sqrMagnitude <= 0.001f) return 0f;

            return Vector3.Dot(frame.CombatRight, playerToCamera.normalized);
        }

        private float ResolveStickySide(
            LockCameraRigRuntime rt,
            float rawSide,
            bool instant,
            bool updateStickySide)
        {
            float current = Mathf.Abs(rt.smoothedSide) > 0.001f
                ? Mathf.Clamp(rt.smoothedSide, -1f, 1f)
                : ResolveInitialSide(rawSide);

            if (!updateStickySide)
                return current;

            float target = current >= 0f ? 1f : -1f;
            float threshold = Mathf.Max(0.001f, _o.softLockSideSwitchThreshold);
            if (rawSide > threshold)
                target = 1f;
            else if (rawSide < -threshold)
                target = -1f;

            if (instant)
            {
                rt.smoothedSide = target;
                rt.sideSmoothVelocity = 0f;
                return target;
            }

            rt.smoothedSide = Mathf.SmoothDamp(
                current,
                target,
                ref rt.sideSmoothVelocity,
                Mathf.Max(0.001f, _o.softLockSideSmoothTime));

            return Mathf.Clamp(rt.smoothedSide, -1f, 1f);
        }

        private static float ResolveInitialSide(float rawSide)
        {
            if (Mathf.Abs(rawSide) > 0.001f)
                return rawSide >= 0f ? 1f : -1f;
            return 1f;
        }

        private Vector3 ResolveAnchorPosition(SoftLockFrame frame, float sideValue)
        {
            float sideOffset = Mathf.Max(0f, _o.softLockSideOffset) * sideValue;
            return frame.Center + frame.CombatRight * sideOffset;
        }

        private float ResolveAnchorYaw(SoftLockFrame frame, float sideValue)
        {
            Vector3 cameraOffsetDir = -frame.CombatDir + frame.CombatRight * sideValue;
            cameraOffsetDir.y = 0f;
            if (cameraOffsetDir.sqrMagnitude <= 0.0001f)
                cameraOffsetDir = -frame.CombatDir;
            cameraOffsetDir.Normalize();

            // The virtual camera sits at FollowOffset (0, 0, -distance),
            // so anchor forward points from camera toward the combat frame.
            Vector3 anchorForward = -cameraOffsetDir;
            return Mathf.Atan2(anchorForward.x, anchorForward.z) * Mathf.Rad2Deg;
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
            }

            float framingSize = _o.softLockFramingSize;
            float maxFov = Mathf.Max(_o.softLockFov, 68f);

            var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
            if (groupComposer != null)
            {
                groupComposer.m_GroupFramingSize = framingSize;
                groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyThenZoom;
                groupComposer.m_MaximumFOV = maxFov;
                groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
            }
            else
            {
                var framingTransposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (framingTransposer != null)
                {
                    framingTransposer.m_GroupFramingSize = framingSize;
                    framingTransposer.m_AdjustmentMode = CinemachineFramingTransposer.AdjustmentMode.DollyThenZoom;
                    framingTransposer.m_MaximumFOV = maxFov;
                }
            }

            var lens = vcam.m_Lens;
            lens.FieldOfView = _o.softLockFov;
            vcam.m_Lens = lens;
        }

        private static void CaptureDiagnostics(
            LockCameraRigRuntime rt,
            SoftLockFrame frame,
            float rawSide,
            float sideValue,
            Vector3 desiredAnchorPos,
            float desiredYaw)
        {
            rt.dbgLabel = "SoftLock";
            rt.dbgCombatCenter = frame.Center;
            rt.dbgCombatDir = frame.CombatDir;
            rt.dbgCombatDist = frame.Distance;
            rt.dbgRawSide = rawSide;
            rt.dbgSideAmount = sideValue;
            rt.dbgDesiredAnchorPos = desiredAnchorPos;

            rt.dbgYawBefore = rt.currentAnchorYaw;
            rt.dbgYawAfter = rt.currentAnchorYaw;
            rt.dbgFormulaYaw = desiredYaw;
            rt.dbgYawSource = "SoftLockPose";
            rt.dbgSectorZone = "SoftLock";
            rt.dbgTrend = "sticky-side";
            rt.dbgCorrectionWeight = Mathf.Abs(sideValue);
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}
