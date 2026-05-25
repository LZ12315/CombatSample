using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Basic")]
    [SerializeField] private float softLockPlayerWeight = 1.15f;
    [SerializeField] private float softLockEnemyWeight = 1.05f;
    [SerializeField] private float softLockPlayerRadius = 1.1f;
    [SerializeField] private float softLockEnemyRadius = 1.0f;
    [SerializeField] private float softLockFollowFrameYawSmoothTime = 0.22f;

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

        public void Refresh(LockCameraRigRuntime rt, Transform enemyTarget, bool instant, bool updateStickySide)
        {
            if (rt == null || enemyTarget == null || _o.actor == null)
                return;

            rt.CreateSoftLockBasicRuntime(_o.transform);
            if (rt.targetGroup == null || rt.followFrame == null)
                return;

            Transform playerTarget = ResolveCameraTarget(_o.actor, _o.transform);
            Transform enemyTargetPoint = ResolveCameraTarget(enemyTarget);
            if (playerTarget == null || enemyTargetPoint == null)
                return;

            UpdateFollowFrame(rt, playerTarget.position, enemyTargetPoint.position, instant);
            RefreshTargetGroup(rt, enemyTarget, playerTarget, enemyTargetPoint);
            CaptureDiagnostics(rt, enemyTarget, playerTarget, enemyTargetPoint);
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

        private void UpdateFollowFrame(LockCameraRigRuntime rt, Vector3 playerPos, Vector3 enemyPos, bool instant)
        {
            Vector3 dir = enemyPos - playerPos;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                float desiredYaw = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
                if (instant)
                {
                    rt.followFrameYaw = desiredYaw;
                    rt.followFrameYawVelocity = 0f;
                }
                else
                {
                    rt.followFrameYaw = Mathf.SmoothDampAngle(
                        rt.followFrameYaw,
                        desiredYaw,
                        ref rt.followFrameYawVelocity,
                        Mathf.Max(0.001f, _o.softLockFollowFrameYawSmoothTime));
                }
            }

            rt.followFrame.position = playerPos;
            rt.followFrame.rotation = Quaternion.Euler(0f, rt.followFrameYaw, 0f);
        }

        private void RefreshTargetGroup(
            LockCameraRigRuntime rt,
            Transform enemyTarget,
            Transform playerTarget,
            Transform enemyTargetPoint)
        {
            if (rt.targetGroup == null || playerTarget == null || enemyTargetPoint == null)
                return;

            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != enemyTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != playerTarget
                || targets[1].target != enemyTargetPoint;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = playerTarget },
                    new CinemachineTargetGroup.Target { target = enemyTargetPoint }
                };

                rt.trackedState = Enums.PlayerCameraState.SoftLock;
                rt.trackedLockTarget = enemyTarget;
                rt.targetGroupDirty = false;
            }

            rt.targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupAverage;
            rt.targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
            rt.targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;

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
            Transform playerTarget,
            Transform enemyTargetPoint)
        {
            Vector3 playerPos = playerTarget != null ? playerTarget.position : Vector3.zero;
            Vector3 enemyPos = enemyTargetPoint != null ? enemyTargetPoint.position : Vector3.zero;
            Vector3 center = (playerPos + enemyPos) * 0.5f;
            Vector3 dir = enemyTarget != null ? enemyPos - playerPos : Vector3.forward;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.forward;

            rt.dbgLabel = "SoftLock3rdPersonFollow";
            rt.dbgCombatCenter = center;
            rt.dbgCombatDir = dir;
            rt.dbgCombatDist = dist;
            rt.dbgRawSide = 0f;
            rt.dbgSideAmount = 0f;
            rt.dbgDesiredAnchorPos = playerPos;
            rt.dbgTargetGroupPos = center;
            rt.dbgYawSource = "SoftLockFollowFrame";
            rt.dbgSectorZone = "3rdPersonFollow";
            rt.dbgTrend = "basic";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}