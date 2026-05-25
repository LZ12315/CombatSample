using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Pose")]
    [Tooltip("软锁定战斗中心偏向敌人的比例。值越低越贴近玩家；值越高越像双目标构图。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockCenterBias = 0.3f;
    [Tooltip("软锁定的额外斜视偏移。0 表示不主动偏左/右，只通过横向滞后自然形成斜视。")]
    [SerializeField] private float softLockSideOffset = 0f;
    [Tooltip("启用额外斜视偏移时，玩家越过该侧向阈值后才切换偏移侧。")]
    [SerializeField] private float softLockSideSwitchThreshold = 0.6f;
    [Tooltip("软锁定横向跟随死区。玩家在该范围内横移时，anchor 不主动追；超过后只追到死区边缘。")]
    [SerializeField] private float softLockLateralDeadZone = 0.9f;

    [Header("Soft Lock - Damping")]
    [Tooltip("软锁定 anchor 位置阻尼。值越大，相机跟随越慢、画面越稳。")]
    [SerializeField] private float softLockAnchorSmoothTime = 0.35f;
    [Tooltip("软锁定 yaw 阻尼。值越大，相机转向越慢、越稳定。")]
    [SerializeField] private float softLockYawSmoothTime = 0.35f;
    [Tooltip("软锁定额外斜视侧切换的平滑时间。")]
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

            rt.currentFollowDistance = Mathf.Max(0.01f, _o.softLockFollowDistance);

            Vector3 desiredAnchorPos = ResolveAnchorPosition(rt, frame, sideValue, instant);
            float desiredYaw = ResolveAnchorYaw(frame, desiredAnchorPos, rt.currentFollowDistance);

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

        private Vector3 ResolveAnchorPosition(
            LockCameraRigRuntime rt,
            SoftLockFrame frame,
            float sideValue,
            bool instant)
        {
            float explicitSideOffset = Mathf.Max(0f, _o.softLockSideOffset) * sideValue;
            Vector3 baseTarget = frame.Center + frame.CombatRight * explicitSideOffset;

            if (instant)
                return baseTarget;

            float deadZone = Mathf.Max(0f, _o.softLockLateralDeadZone);
            if (deadZone <= 0.001f)
                return baseTarget;

            Vector3 anchorToTarget = rt.anchor.position - baseTarget;
            float lateralOffset = Vector3.Dot(anchorToTarget, frame.CombatRight);
            Vector3 lateralTargetCorrection = Vector3.zero;

            if (Mathf.Abs(lateralOffset) > deadZone)
            {
                float targetLateralOffset = Mathf.Sign(lateralOffset) * deadZone;
                lateralTargetCorrection = frame.CombatRight * targetLateralOffset;
            }
            else
            {
                lateralTargetCorrection = frame.CombatRight * lateralOffset;
            }

            // Forward/depth and height continue to follow the combat frame, while lateral
            // motion is dead-zoned. This creates the delayed side transition seen in
            // character-action soft lock cameras.
            return baseTarget + lateralTargetCorrection;
        }

        private float ResolveAnchorYaw(
            SoftLockFrame frame,
            Vector3 desiredAnchorPos,
            float followDistance)
        {
            // Default to a rear camera: behind the player on the player->enemy axis.
            // Any oblique angle now comes from the anchor's actual lateral lag, not from
            // a hard-coded yaw side bias. Therefore softLockSideOffset=0 really means
            // no forced斜视角.
            Vector3 estimatedCameraPos = desiredAnchorPos - frame.CombatDir * Mathf.Max(0.01f, followDistance);
            Vector3 anchorForward = frame.Center - estimatedCameraPos;
            anchorForward.y = 0f;

            if (anchorForward.sqrMagnitude <= 0.0001f)
                anchorForward = frame.CombatDir;

            anchorForward.Normalize();
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
            rt.dbgYawSource = "SoftLockRearLag";
            rt.dbgSectorZone = "SoftLock";
            rt.dbgTrend = "lateral-deadzone";
            rt.dbgCorrectionWeight = Mathf.Abs(sideValue);
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}
