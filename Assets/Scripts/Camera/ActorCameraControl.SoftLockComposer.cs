using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Observed Targets")]
    [Tooltip("玩家观察代理的高度。它代表玩家主体，而不是相机站位。")]
    [SerializeField] private float softLockPlayerViewHeightOffset = 1.05f;
    [Tooltip("敌人观察代理的高度。")]
    [SerializeField] private float softLockEnemyViewHeightOffset = 1.0f;
    [Tooltip("AimProxy 偏向敌人的比例。用于保证敌人可见，但不应过高，否则会变硬锁。")]
    [Range(0f, 0.65f)]
    [SerializeField] private float softLockAimEnemyBias = 0.24f;
    [Tooltip("AimProxy 额外高度偏移，通常让镜头看向玩家胸口附近。")]
    [SerializeField] private float softLockAimHeightOffset = 1.05f;
    [Tooltip("观察代理的平滑时间。代理只表达构图意图，真实相机移动交给 Cinemachine。")]
    [SerializeField] private float softLockProxySmoothTime = 0.18f;
    [Tooltip("敌人代理的平滑时间。略大可避免敌人位移把镜头拉得太急。")]
    [SerializeField] private float softLockEnemyProxySmoothTime = 0.24f;

    [Header("Soft Lock - Target Weights")]
    [Tooltip("玩家在软锁构图组中的基础权重。玩家应该始终是主体。")]
    [SerializeField] private float softLockPlayerViewWeight = 1.35f;
    [Tooltip("敌人在软锁构图组中的基础权重。低于玩家，避免变成硬锁。")]
    [SerializeField] private float softLockEnemyViewWeight = 0.48f;
    [Tooltip("敌人接近屏幕边缘或出画时提高到的最大权重。")]
    [SerializeField] private float softLockEnemyEdgeWeight = 0.82f;
    [Tooltip("AimProxy 的辅助权重，用于稳定玩家与敌人之间的关注区域。")]
    [SerializeField] private float softLockAimViewWeight = 0.32f;
    [Tooltip("侧向留白代理的最大权重。玩家与敌人屏幕重叠时启用，用来把敌人漏出来。")]
    [SerializeField] private float softLockRevealMaxWeight = 0.42f;
    [Tooltip("玩家代理半径。影响 FramingTransposer 认为玩家占据的屏幕空间。")]
    [SerializeField] private float softLockPlayerViewRadius = 0.75f;
    [Tooltip("敌人代理半径。")]
    [SerializeField] private float softLockEnemyViewRadius = 0.65f;
    [Tooltip("AimProxy 半径。")]
    [SerializeField] private float softLockAimViewRadius = 0.15f;
    [Tooltip("RevealProxy 半径。")]
    [SerializeField] private float softLockRevealRadius = 0.2f;

    [Header("Soft Lock - Reveal Hint")]
    [Tooltip("玩家和敌人在屏幕上接近到这个距离时，开始启用侧向留白。Viewport 单位。")]
    [SerializeField] private float softLockRevealStartScreenDistance = 0.22f;
    [Tooltip("玩家和敌人在屏幕上接近到这个距离时，侧向留白达到最大。Viewport 单位。")]
    [SerializeField] private float softLockRevealFullScreenDistance = 0.08f;
    [Tooltip("RevealProxy 相对玩家的侧向世界偏移。它不是相机偏移，只是构图提示点。")]
    [SerializeField] private float softLockRevealSideOffset = 1.15f;
    [Tooltip("RevealProxy 高度。")]
    [SerializeField] private float softLockRevealHeightOffset = 1.1f;

    [Header("Soft Lock - Lens")]
    [Tooltip("软锁定基础相机距离。由 FramingTransposer 执行，不再作为代码 anchor 距离。")]
    [SerializeField] private float softLockFollowDistance = 6f;
    [Tooltip("软锁定固定 FOV。动态 FOV 会在后续阶段处理。")]
    [SerializeField] private float softLockFov = 45f;
    [Tooltip("软锁定目标组构图大小。")]
    [Range(0.45f, 0.9f)]
    [SerializeField] private float softLockFramingSize = 0.7f;
    [Tooltip("软锁定目标组基础边距。")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float softLockTargetPadding = 1.0f;

    [Header("Soft Lock - Cinemachine Body Profile")]
    [Tooltip("FramingTransposer 的目标屏幕 X。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockBodyScreenX = 0.5f;
    [Tooltip("FramingTransposer 的目标屏幕 Y。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockBodyScreenY = 0.55f;
    [Tooltip("FramingTransposer 的水平死区。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockBodyDeadZoneWidth = 0.18f;
    [Tooltip("FramingTransposer 的垂直死区。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockBodyDeadZoneHeight = 0.14f;
    [Tooltip("FramingTransposer 的水平软区。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockBodySoftZoneWidth = 0.82f;
    [Tooltip("FramingTransposer 的垂直软区。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockBodySoftZoneHeight = 0.76f;
    [Tooltip("FramingTransposer 水平 damping。值越大，真实相机横向移动越不急。")]
    [SerializeField] private float softLockBodyXDamping = 1.25f;
    [Tooltip("FramingTransposer 垂直 damping。")]
    [SerializeField] private float softLockBodyYDamping = 1.35f;
    [Tooltip("FramingTransposer 距离 damping。")]
    [SerializeField] private float softLockBodyZDamping = 1.05f;

    [Header("Soft Lock - Aim Profile")]
    [Tooltip("TargetGroup 中心在屏幕中的目标 X。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockComposerScreenX = 0.5f;
    [Tooltip("TargetGroup 中心在屏幕中的目标 Y。")]
    [Range(0f, 1f)]
    [SerializeField] private float softLockComposerScreenY = 0.55f;
    [Tooltip("Composer 屏幕水平死区。目标在死区内时，Cinemachine 不主动修正旋转。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockComposerDeadZoneWidth = 0.16f;
    [Tooltip("Composer 屏幕垂直死区。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float softLockComposerDeadZoneHeight = 0.12f;
    [Tooltip("Composer 屏幕水平软区。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockComposerSoftZoneWidth = 0.82f;
    [Tooltip("Composer 屏幕垂直软区。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float softLockComposerSoftZoneHeight = 0.76f;
    [Tooltip("Composer 水平 damping。")]
    [SerializeField] private float softLockComposerHorizontalDamping = 1.15f;
    [Tooltip("Composer 垂直 damping。")]
    [SerializeField] private float softLockComposerVerticalDamping = 1.3f;

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
            if (rt == null || rt.targetGroup == null || enemyTarget == null || _o.actor == null)
                return;

            rt.CreateAimProxy(_o.transform);
            rt.CreateSoftLockViewProxies(_o.transform);
            if (rt.aimProxy == null || rt.playerViewProxy == null || rt.enemyViewProxy == null || rt.revealProxy == null)
                return;

            SoftLockFrame frame = BuildFrame(enemyTarget);
            Vector3 playerViewTarget = ResolvePlayerViewProxyPosition(frame);
            Vector3 enemyViewTarget = ResolveEnemyViewProxyPosition(frame);
            Vector3 aimTarget = ResolveAimProxyPosition(frame);
            Vector3 revealTarget = ResolveRevealProxyPosition(frame, out float revealWeight);
            float enemyWeight = ResolveEnemyWeight(frame);

            MoveProxy(rt.playerViewProxy, playerViewTarget, ref rt.playerViewProxyVelocity, _o.softLockProxySmoothTime, instant);
            MoveProxy(rt.enemyViewProxy, enemyViewTarget, ref rt.enemyViewProxyVelocity, _o.softLockEnemyProxySmoothTime, instant);
            MoveProxy(rt.aimProxy, aimTarget, ref rt.aimProxyVelocity, _o.softLockProxySmoothTime, instant);
            MoveProxy(rt.revealProxy, revealTarget, ref rt.revealProxyVelocity, _o.softLockProxySmoothTime, instant);

            rt.currentFollowDistance = Mathf.Max(0.01f, _o.softLockFollowDistance);

            RefreshTargetGroup(rt, enemyTarget, enemyWeight, revealWeight);
            ApplyCinemachineSettings(rt);
            CaptureDiagnostics(rt, frame, aimTarget, revealWeight);
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

        private Vector3 ResolvePlayerViewProxyPosition(SoftLockFrame frame)
        {
            Vector3 p = frame.PlayerPos;
            p.y = frame.PlayerPos.y + _o.softLockPlayerViewHeightOffset;
            return p;
        }

        private Vector3 ResolveEnemyViewProxyPosition(SoftLockFrame frame)
        {
            Vector3 p = frame.EnemyPos;
            p.y = frame.EnemyPos.y + _o.softLockEnemyViewHeightOffset;
            return p;
        }

        private Vector3 ResolveAimProxyPosition(SoftLockFrame frame)
        {
            float bias = Mathf.Clamp01(_o.softLockAimEnemyBias);
            Vector3 aimXZ = Vector3.Lerp(frame.PlayerXZ, frame.EnemyXZ, bias);
            Vector3 aim = aimXZ;
            float playerAimY = frame.PlayerPos.y + _o.softLockAimHeightOffset;
            float enemyAimY = frame.EnemyPos.y + _o.softLockEnemyViewHeightOffset;
            aim.y = Mathf.Lerp(playerAimY, enemyAimY, bias * 0.35f);
            return aim;
        }

        private Vector3 ResolveRevealProxyPosition(SoftLockFrame frame, out float revealWeight)
        {
            revealWeight = ResolveRevealWeight(frame, out float screenSideSign);

            Camera cam = Camera.main;
            Vector3 sideDir = Vector3.zero;
            if (cam != null)
            {
                sideDir = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
                if (sideDir.sqrMagnitude > 0.0001f)
                    sideDir.Normalize();
            }
            if (sideDir.sqrMagnitude <= 0.0001f)
                sideDir = frame.CombatRight;

            if (Mathf.Abs(screenSideSign) < 0.1f)
            {
                Vector3 camToEnemy = frame.EnemyXZ - new Vector3(cam != null ? cam.transform.position.x : frame.PlayerPos.x, 0f, cam != null ? cam.transform.position.z : frame.PlayerPos.z);
                screenSideSign = Mathf.Sign(Vector3.Dot(sideDir, camToEnemy));
                if (Mathf.Abs(screenSideSign) < 0.1f)
                    screenSideSign = 1f;
            }

            Vector3 reveal = frame.PlayerPos + sideDir * screenSideSign * _o.softLockRevealSideOffset;
            reveal.y = frame.PlayerPos.y + _o.softLockRevealHeightOffset;
            return reveal;
        }

        private float ResolveRevealWeight(SoftLockFrame frame, out float screenSideSign)
        {
            screenSideSign = 0f;
            Camera cam = Camera.main;
            if (cam == null) return 0f;

            Vector3 playerViewport = cam.WorldToViewportPoint(frame.PlayerPos + Vector3.up * _o.softLockPlayerViewHeightOffset);
            Vector3 enemyViewport = cam.WorldToViewportPoint(frame.EnemyPos + Vector3.up * _o.softLockEnemyViewHeightOffset);
            if (playerViewport.z <= 0f || enemyViewport.z <= 0f)
                return 0f;

            Vector2 p = new Vector2(playerViewport.x, playerViewport.y);
            Vector2 e = new Vector2(enemyViewport.x, enemyViewport.y);
            float separation = Vector2.Distance(p, e);
            screenSideSign = Mathf.Sign(enemyViewport.x - playerViewport.x);

            float start = Mathf.Max(_o.softLockRevealStartScreenDistance, _o.softLockRevealFullScreenDistance + 0.001f);
            float full = Mathf.Min(_o.softLockRevealFullScreenDistance, start - 0.001f);
            float t = 1f - Mathf.InverseLerp(full, start, separation);
            return Mathf.Clamp01(t) * Mathf.Max(0f, _o.softLockRevealMaxWeight);
        }

        private float ResolveEnemyWeight(SoftLockFrame frame)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return Mathf.Max(0f, _o.softLockEnemyViewWeight);

            Vector3 enemyViewport = cam.WorldToViewportPoint(frame.EnemyPos + Vector3.up * _o.softLockEnemyViewHeightOffset);
            if (enemyViewport.z <= 0f)
                return Mathf.Max(_o.softLockEnemyViewWeight, _o.softLockEnemyEdgeWeight);

            float edgeX = Mathf.Abs(enemyViewport.x - 0.5f) * 2f;
            float edgeY = Mathf.Abs(enemyViewport.y - 0.5f) * 2f;
            float edge = Mathf.Max(edgeX, edgeY);
            float edgeT = Mathf.InverseLerp(0.65f, 0.95f, edge);
            return Mathf.Lerp(
                Mathf.Max(0f, _o.softLockEnemyViewWeight),
                Mathf.Max(0f, _o.softLockEnemyEdgeWeight),
                Mathf.Clamp01(edgeT));
        }

        private static void MoveProxy(Transform proxy, Vector3 target, ref Vector3 velocity, float smoothTime, bool instant)
        {
            if (proxy == null) return;
            if (instant)
            {
                proxy.position = target;
                velocity = Vector3.zero;
                return;
            }

            proxy.position = Vector3.SmoothDamp(
                proxy.position,
                target,
                ref velocity,
                Mathf.Max(0.001f, smoothTime));
        }

        private void RefreshTargetGroup(
            LockCameraRigRuntime rt,
            Transform enemyTarget,
            float enemyWeight,
            float revealWeight)
        {
            if (rt.targetGroup == null) return;

            Transform lockTarget = enemyTarget;
            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != lockTarget
                || targets == null
                || targets.Length != 4
                || targets[0].target != rt.playerViewProxy
                || targets[1].target != rt.enemyViewProxy
                || targets[2].target != rt.aimProxy
                || targets[3].target != rt.revealProxy;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = rt.playerViewProxy },
                    new CinemachineTargetGroup.Target { target = rt.enemyViewProxy },
                    new CinemachineTargetGroup.Target { target = rt.aimProxy },
                    new CinemachineTargetGroup.Target { target = rt.revealProxy }
                };

                rt.trackedState = Enums.PlayerCameraState.SoftLock;
                rt.trackedLockTarget = lockTarget;
                rt.targetGroupDirty = false;
            }

            targets = rt.targetGroup.m_Targets;
            if (targets == null || targets.Length != 4) return;

            targets[0].weight = Mathf.Max(0f, _o.softLockPlayerViewWeight);
            targets[0].radius = Mathf.Max(0f, _o.softLockPlayerViewRadius);
            targets[1].weight = Mathf.Max(0f, enemyWeight);
            targets[1].radius = Mathf.Max(0f, _o.softLockEnemyViewRadius);
            targets[2].weight = Mathf.Max(0f, _o.softLockAimViewWeight);
            targets[2].radius = Mathf.Max(0f, _o.softLockAimViewRadius);
            targets[3].weight = Mathf.Max(0f, revealWeight);
            targets[3].radius = Mathf.Max(0f, _o.softLockRevealRadius);

            rt.targetGroup.m_Targets = targets;
        }

        private void ApplyCinemachineSettings(LockCameraRigRuntime rt)
        {
            CinemachineVirtualCamera vcam = _o.softLockCamera;
            if (vcam == null) return;

            EnsureFramingTransposer(vcam);
            EnsureComposer(vcam);

            var framingTransposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framingTransposer != null)
            {
                framingTransposer.m_XDamping = Mathf.Max(0f, _o.softLockBodyXDamping);
                framingTransposer.m_YDamping = Mathf.Max(0f, _o.softLockBodyYDamping);
                framingTransposer.m_ZDamping = Mathf.Max(0f, _o.softLockBodyZDamping);
                framingTransposer.m_ScreenX = _o.softLockBodyScreenX;
                framingTransposer.m_ScreenY = _o.softLockBodyScreenY;
                framingTransposer.m_DeadZoneWidth = _o.softLockBodyDeadZoneWidth;
                framingTransposer.m_DeadZoneHeight = _o.softLockBodyDeadZoneHeight;
                framingTransposer.m_SoftZoneWidth = _o.softLockBodySoftZoneWidth;
                framingTransposer.m_SoftZoneHeight = _o.softLockBodySoftZoneHeight;
                framingTransposer.m_BiasX = 0f;
                framingTransposer.m_BiasY = 0f;
                framingTransposer.m_CenterOnActivate = false;
                framingTransposer.m_CameraDistance = Mathf.Max(0.01f, rt.currentFollowDistance);
                framingTransposer.m_GroupFramingSize = _o.softLockFramingSize;
                framingTransposer.m_AdjustmentMode = CinemachineFramingTransposer.AdjustmentMode.DollyOnly;
                framingTransposer.m_MaximumFOV = Mathf.Max(_o.softLockFov, 60f);
                framingTransposer.m_MinimumDistance = 3f;
                framingTransposer.m_MaximumDistance = 9f;
                framingTransposer.m_MaxDollyIn = 2f;
                framingTransposer.m_MaxDollyOut = 4f;
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

            var lens = vcam.m_Lens;
            lens.FieldOfView = _o.softLockFov;
            vcam.m_Lens = lens;
        }

        private static void EnsureFramingTransposer(CinemachineVirtualCamera vcam)
        {
            if (vcam == null) return;
            if (vcam.GetCinemachineComponent<CinemachineFramingTransposer>() != null)
                return;

            if (vcam.GetCinemachineComponent<CinemachineTransposer>() != null)
                vcam.DestroyCinemachineComponent<CinemachineTransposer>();

            vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
        }

        private static void EnsureComposer(CinemachineVirtualCamera vcam)
        {
            if (vcam == null) return;

            if (vcam.GetCinemachineComponent<CinemachineGroupComposer>() != null)
                vcam.DestroyCinemachineComponent<CinemachineGroupComposer>();

            if (vcam.GetCinemachineComponent<CinemachineComposer>() == null)
                vcam.AddCinemachineComponent<CinemachineComposer>();
        }

        private static void CaptureDiagnostics(
            LockCameraRigRuntime rt,
            SoftLockFrame frame,
            Vector3 aimTarget,
            float revealWeight)
        {
            rt.dbgLabel = "SoftLockCinemachineTargetGroup";
            rt.dbgCombatCenter = aimTarget;
            rt.dbgCombatDir = frame.CombatDir;
            rt.dbgCombatDist = frame.Distance;
            rt.dbgRawSide = 0f;
            rt.dbgSideAmount = revealWeight;
            rt.dbgDesiredAnchorPos = rt.targetGroup != null ? rt.targetGroup.transform.position : aimTarget;
            rt.dbgYawSource = "CinemachineFramingTransposer";
            rt.dbgSectorZone = "ObservedTargets";
            rt.dbgTrend = "target-group";
            rt.dbgCorrectionWeight = revealWeight;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}
