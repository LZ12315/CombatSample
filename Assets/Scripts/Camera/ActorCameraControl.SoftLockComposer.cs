using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Basic")]
    [Tooltip("玩家在 SoftLock TargetGroup 中的权重。玩家应保持主体。")]
    [SerializeField] private float softLockPlayerWeight = 1.2f;
    [Tooltip("敌人在 SoftLock TargetGroup 中的权重。Phase 0 固定，不做动态权重。")]
    [SerializeField] private float softLockEnemyWeight = 0.85f;
    [Tooltip("玩家目标半径。影响 GroupComposer 对玩家占屏空间的估计。")]
    [SerializeField] private float softLockPlayerRadius = 0.9f;
    [Tooltip("敌人目标半径。")]
    [SerializeField] private float softLockEnemyRadius = 0.9f;

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
            if (rt == null || enemyTarget == null || _o.actor == null)
                return;

            rt.CreateSoftLockBasicRuntime(_o.transform);
            if (rt.targetGroup == null)
                return;

            Transform playerCameraTarget = ResolveCameraTarget(_o.actor, _o.transform);
            Transform enemyCameraTarget = ResolveCameraTarget(enemyTarget);

            RefreshTargetGroup(rt, enemyTarget, playerCameraTarget, enemyCameraTarget);
            CaptureDiagnostics(rt, enemyTarget, playerCameraTarget, enemyCameraTarget);
        }

        private static Transform ResolveCameraTarget(Actor actor, Transform fallback)
        {
            if (actor != null && actor.CameraTarget != null)
                return actor.CameraTarget;
            return fallback;
        }

        private static Transform ResolveCameraTarget(Transform target)
        {
            if (target == null) return null;

            Actor actor = target.GetComponentInParent<Actor>();
            if (actor != null && actor.CameraTarget != null)
                return actor.CameraTarget;

            return target;
        }

        private void RefreshTargetGroup(
            LockCameraRigRuntime rt,
            Transform enemyTarget,
            Transform playerCameraTarget,
            Transform enemyCameraTarget)
        {
            if (rt.targetGroup == null || playerCameraTarget == null || enemyCameraTarget == null)
                return;

            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != enemyTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != playerCameraTarget
                || targets[1].target != enemyCameraTarget;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = playerCameraTarget },
                    new CinemachineTargetGroup.Target { target = enemyCameraTarget }
                };

                rt.trackedState = Enums.PlayerCameraState.SoftLock;
                rt.trackedLockTarget = enemyTarget;
                rt.targetGroupDirty = false;
            }

            targets = rt.targetGroup.m_Targets;
            if (targets == null || targets.Length != 2) return;

            targets[0].weight = Mathf.Max(0f, _o.softLockPlayerWeight);
            targets[0].radius = Mathf.Max(0f, _o.softLockPlayerRadius);
            targets[1].weight = Mathf.Max(0f, _o.softLockEnemyWeight);
            targets[1].radius = Mathf.Max(0f, _o.softLockEnemyRadius);
            rt.targetGroup.m_Targets = targets;
        }

        private static void CaptureDiagnostics(
            LockCameraRigRuntime rt,
            Transform enemyTarget,
            Transform playerCameraTarget,
            Transform enemyCameraTarget)
        {
            Vector3 playerPos = playerCameraTarget != null ? playerCameraTarget.position : Vector3.zero;
            Vector3 enemyPos = enemyCameraTarget != null ? enemyCameraTarget.position : Vector3.zero;
            Vector3 center = (playerPos + enemyPos) * 0.5f;
            Vector3 dir = enemyTarget != null ? enemyPos - playerPos : Vector3.forward;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.forward;

            rt.dbgLabel = "SoftLockPhase0";
            rt.dbgCombatCenter = center;
            rt.dbgCombatDir = dir;
            rt.dbgCombatDist = dist;
            rt.dbgRawSide = 0f;
            rt.dbgSideAmount = 0f;
            rt.dbgDesiredAnchorPos = playerPos;
            rt.dbgYawSource = "ActorCameraTarget+GroupComposer";
            rt.dbgSectorZone = "Phase0";
            rt.dbgTrend = "basic";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}