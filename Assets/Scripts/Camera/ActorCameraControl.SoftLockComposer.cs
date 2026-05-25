using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    private const float SoftLockEnemyHighCompression = 0.15f;
    private const float SoftLockEnemyFramingSmoothTime = 0.08f;

    [Header("Soft Lock - Basic")]
    [SerializeField] private float softLockPlayerWeight = 1.15f;
    [SerializeField] private float softLockEnemyWeight = 1.05f;
    [SerializeField] private float softLockPlayerRadius = 1.1f;
    [SerializeField] private float softLockEnemyRadius = 1.0f;
    [SerializeField] private float softLockEnemyMaxVerticalInfluence = 1.8f;

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
            if (rt.targetGroup == null || rt.enemyFramingTarget == null)
                return;

            Transform playerTarget = ResolveCameraTarget(_o.actor, _o.transform);
            Transform enemyRawTarget = ResolveCameraTarget(enemyTarget);
            if (playerTarget == null || enemyRawTarget == null)
                return;

            UpdateEnemyFramingTarget(rt, playerTarget.position, enemyRawTarget.position, instant);
            RefreshTargetGroup(rt, enemyTarget, playerTarget, rt.enemyFramingTarget);
            CaptureDiagnostics(rt, enemyTarget, playerTarget, enemyRawTarget, rt.enemyFramingTarget);
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

        private void UpdateEnemyFramingTarget(LockCameraRigRuntime rt, Vector3 playerPos, Vector3 enemyRawPos, bool instant)
        {
            Vector3 desired = ResolveEnemyFramingPosition(playerPos, enemyRawPos);

            if (instant)
            {
                rt.enemyFramingTarget.position = desired;
                rt.enemyFramingTargetVelocity = Vector3.zero;
                return;
            }

            rt.enemyFramingTarget.position = Vector3.SmoothDamp(
                rt.enemyFramingTarget.position,
                desired,
                ref rt.enemyFramingTargetVelocity,
                SoftLockEnemyFramingSmoothTime);
        }

        private Vector3 ResolveEnemyFramingPosition(Vector3 playerPos, Vector3 enemyRawPos)
        {
            float maxY = playerPos.y + Mathf.Max(0f, _o.softLockEnemyMaxVerticalInfluence);
            float y = enemyRawPos.y;

            if (y > maxY)
            {
                float excess = y - maxY;
                y = maxY + excess * SoftLockEnemyHighCompression;
            }

            return new Vector3(enemyRawPos.x, y, enemyRawPos.z);
        }

        private void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget, Transform playerTarget, Transform enemyFramingTarget)
        {
            if (rt.targetGroup == null || playerTarget == null || enemyFramingTarget == null)
                return;

            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != enemyTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != playerTarget
                || targets[1].target != enemyFramingTarget;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = playerTarget },
                    new CinemachineTargetGroup.Target { target = enemyFramingTarget }
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

        private static void CaptureDiagnostics(LockCameraRigRuntime rt, Transform enemyTarget, Transform playerTarget, Transform enemyRawTarget, Transform enemyFramingTarget)
        {
            Vector3 playerPos = playerTarget != null ? playerTarget.position : Vector3.zero;
            Vector3 enemyRawPos = enemyRawTarget != null ? enemyRawTarget.position : Vector3.zero;
            Vector3 enemyFramingPos = enemyFramingTarget != null ? enemyFramingTarget.position : enemyRawPos;
            Vector3 center = (playerPos + enemyFramingPos) * 0.5f;
            Vector3 dir = enemyTarget != null ? enemyFramingPos - playerPos : Vector3.forward;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.forward;

            rt.dbgLabel = "SoftLockFramingTarget";
            rt.dbgCombatCenter = center;
            rt.dbgCombatDir = dir;
            rt.dbgCombatDist = dist;
            rt.dbgRawSide = enemyRawPos.y - enemyFramingPos.y;
            rt.dbgSideAmount = 0f;
            rt.dbgDesiredAnchorPos = playerPos;
            rt.dbgTargetGroupPos = center;
            rt.dbgYawSource = "ActorCameraTarget+EnemyFramingTarget";
            rt.dbgSectorZone = "Phase0VerticalLimited";
            rt.dbgTrend = "basic";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}