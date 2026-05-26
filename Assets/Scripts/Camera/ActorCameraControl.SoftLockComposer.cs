using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
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
            if (rt.targetGroup == null
                || rt.softLockFollowTarget == null
                || rt.softLockPlayerFramingTarget == null
                || rt.softLockEnemyFramingTarget == null)
                return;

            Transform playerRawTarget = ResolveCameraTarget(_o.actor, _o.transform);
            Transform enemyRawTarget = ResolveCameraTarget(enemyTarget);
            if (playerRawTarget == null || enemyRawTarget == null)
                return;

            Vector3 playerRawPos = playerRawTarget.position;
            Vector3 enemyRawPos = enemyRawTarget.position;

            rt.softLockPlayerFramingTarget.position = playerRawPos;
            rt.softLockPlayerFramingTarget.rotation = Quaternion.identity;
            rt.softLockEnemyFramingTarget.position = enemyRawPos;
            rt.softLockEnemyFramingTarget.rotation = Quaternion.identity;

            float groupY = ResolveGroupY(playerRawPos.y, enemyRawPos.y);
            Vector3 followPos = new Vector3(playerRawPos.x, groupY, playerRawPos.z);
            rt.softLockFollowTarget.position = followPos;
            rt.softLockFollowTarget.rotation = Quaternion.identity;

            RefreshTargetGroup(rt, enemyTarget);
            CaptureDiagnostics(rt, playerRawPos, enemyRawPos, followPos);
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

        private float ResolveGroupY(float playerY, float enemyY)
        {
            float playerWeight = Mathf.Max(0f, _o.softLockPlayerWeight);
            float enemyWeight = Mathf.Max(0f, _o.softLockEnemyWeight);
            float totalWeight = playerWeight + enemyWeight;
            if (totalWeight <= 0.0001f)
                return (playerY + enemyY) * 0.5f;

            return (playerY * playerWeight + enemyY * enemyWeight) / totalWeight;
        }

        private void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget)
        {
            if (rt.targetGroup == null
                || rt.softLockPlayerFramingTarget == null
                || rt.softLockEnemyFramingTarget == null)
                return;

            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != enemyTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != rt.softLockPlayerFramingTarget
                || targets[1].target != rt.softLockEnemyFramingTarget;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = rt.softLockPlayerFramingTarget },
                    new CinemachineTargetGroup.Target { target = rt.softLockEnemyFramingTarget }
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
            Vector3 playerRawPos,
            Vector3 enemyRawPos,
            Vector3 followPos)
        {
            Vector3 center = (playerRawPos + enemyRawPos) * 0.5f;
            Vector3 dir = enemyRawPos - playerRawPos;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.forward;

            rt.dbgLabel = "SoftLockGroupYFollow";
            rt.dbgCombatCenter = center;
            rt.dbgCombatDir = dir;
            rt.dbgCombatDist = dist;
            rt.dbgDesiredAnchorPos = followPos;
            rt.dbgTargetGroupPos = center;
            rt.dbgBodyTarget = "Runtime_SoftLockFollowTarget";
            rt.dbgTrend = "soft-lock-group-y";
        }
    }
}