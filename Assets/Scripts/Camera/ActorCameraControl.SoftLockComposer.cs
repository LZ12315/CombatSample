using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    [Header("Soft Lock - Basic")]
    [Tooltip("玩家观察代理高度。Phase 0 只用它保证玩家完整可见。")]
    [SerializeField] private float softLockPlayerViewHeight = 0.8f;
    [Tooltip("敌人观察代理高度。Phase 0 只用它保证敌人进入基础构图。")]
    [SerializeField] private float softLockEnemyViewHeight = 1.0f;
    [Tooltip("玩家在 SoftLock TargetGroup 中的权重。玩家应保持主体。")]
    [SerializeField] private float softLockPlayerWeight = 1.15f;
    [Tooltip("敌人在 SoftLock TargetGroup 中的权重。Phase 0 固定，不做动态权重。")]
    [SerializeField] private float softLockEnemyWeight = 1.05f;
    [Tooltip("玩家目标半径。影响 GroupComposer 对玩家占屏空间的估计。")]
    [SerializeField] private float softLockPlayerRadius = 1.1f;
    [Tooltip("敌人目标半径。")]
    [SerializeField] private float softLockEnemyRadius = 1.0f;
    [Tooltip("Player/Enemy 观察代理平滑时间。Phase 0 保持很小，避免引入复杂滞后。")]
    [SerializeField] private float softLockProxySmoothTime = 0.08f;

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

            rt.CreateSoftLockBasicRuntime(_o.transform);
            if (rt.playerViewProxy == null || rt.enemyViewProxy == null || rt.targetGroup == null)
                return;

            Vector3 playerTarget = ResolvePlayerViewPosition();
            Vector3 enemyTargetPos = ResolveEnemyViewPosition(enemyTarget);

            MoveProxy(rt.playerViewProxy, playerTarget, ref rt.playerViewProxyVelocity, instant);
            MoveProxy(rt.enemyViewProxy, enemyTargetPos, ref rt.enemyViewProxyVelocity, instant);

            RefreshTargetGroup(rt, enemyTarget);
            CaptureDiagnostics(rt, enemyTarget, playerTarget, enemyTargetPos);
        }

        private Vector3 ResolvePlayerViewPosition()
        {
            Vector3 p = _o.transform.position;
            p.y += _o.softLockPlayerViewHeight;
            return p;
        }

        private Vector3 ResolveEnemyViewPosition(Transform enemyTarget)
        {
            Vector3 p = enemyTarget.position;
            p.y += _o.softLockEnemyViewHeight;
            return p;
        }

        private void MoveProxy(Transform proxy, Vector3 target, ref Vector3 velocity, bool instant)
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
                Mathf.Max(0.001f, _o.softLockProxySmoothTime));
        }

        private void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget)
        {
            if (rt.targetGroup == null) return;

            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;
            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != Enums.PlayerCameraState.SoftLock
                || rt.trackedLockTarget != enemyTarget
                || targets == null
                || targets.Length != 2
                || targets[0].target != rt.playerViewProxy
                || targets[1].target != rt.enemyViewProxy;

            if (needsRebuild)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = rt.playerViewProxy },
                    new CinemachineTargetGroup.Target { target = rt.enemyViewProxy }
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
            Vector3 playerTarget,
            Vector3 enemyTargetPos)
        {
            Vector3 center = (playerTarget + enemyTargetPos) * 0.5f;
            Vector3 dir = enemyTarget != null ? enemyTarget.position - playerTarget : Vector3.forward;
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
            rt.dbgDesiredAnchorPos = playerTarget;
            rt.dbgYawSource = "Transposer+GroupComposer";
            rt.dbgSectorZone = "Phase0";
            rt.dbgTrend = "basic";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }
    }
}
