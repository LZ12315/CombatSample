using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: LockCameraRigRuntime
    // ==================================================================
    // Per-lock-camera runtime objects and smoothed state.
    // Manages its own anchor, TargetGroup, and dirty tracking.
    // ==================================================================

    private sealed class LockCameraRigRuntime
    {
        public Transform anchor;
        public Transform aimProxy;
        public Transform playerViewProxy;
        public Transform enemyViewProxy;
        public Transform revealProxy;
        public CinemachineTargetGroup targetGroup;

        public bool targetGroupDirty = true;
        public Transform trackedLockTarget;
        public Enums.PlayerCameraState trackedState;

        // Smoothed values
        public float smoothedSide;
        public float sideSmoothVelocity;
        public Vector3 anchorPositionVelocity = Vector3.zero;
        public Vector3 aimProxyVelocity = Vector3.zero;
        public Vector3 playerViewProxyVelocity = Vector3.zero;
        public Vector3 enemyViewProxyVelocity = Vector3.zero;
        public Vector3 revealProxyVelocity = Vector3.zero;
        public float anchorYawVelocity;
        public float currentAnchorYaw;
        public float currentFollowDistance = 8f;

        // Diagnostic snapshot — captures the last frame's raw inputs and
        // resolved outputs so the diagnostics layer can log them without
        // reaching back into CombatLockComposer internals.
        public Vector3 dbgCombatCenter;
        public Vector3 dbgCombatDir;
        public float dbgCombatDist;
        public float dbgRawSide;
        public float dbgSideAmount;
        public Vector3 dbgDesiredAnchorPos;
        public Vector3 dbgTargetGroupPos;
        public string dbgLabel;
        // Yaw gate diagnostic
        public float dbgSectorDelta;
        public bool dbgSectorInside;
        public float dbgSectorTargetYaw;
        public string dbgYawSource;        // InstantFormula/InsideHold/SoftEdge/OutsideBoundary/NoCameraFallback
        public float dbgYawBefore;
        public float dbgYawAfter;
        public float dbgFormulaYaw;
        public float dbgBoundaryYaw;
        public float dbgEnemyToPlayerYaw;
        public float dbgEnemyToCameraYaw;
        public float dbgBoundaryDirYaw;
        public Vector3 dbgBoundaryCamPos;
        public float dbgBoundaryRadius;
        public bool dbgIsActiveRuntime;
        // Soft-edge / damped-return state
        public float prevAbsSectorDelta = -1f; // -1 = uninitialized
        public float currentYawReturnSpeed;
        public float yawReturnSpeedVelocity;
        // Damped-return diagnostic
        public float dbgAbsSectorDelta;    // snapshot of absDelta for this frame
        public float dbgPrevAbsSectorDelta; // snapshot of prevAbsDelta BEFORE this frame's update
        public float dbgCorrectionWeight;
        public float dbgHalfAngle;
        public float dbgInnerHoldHalfAngle;
        public string dbgSectorZone;       // hold/soft/outside/NoCamera
        public string dbgTrend;            // init/outward/inward/stable
        public float dbgTargetReturnSpeed;
        public float dbgYawAppliedDelta;

        public void CreateAnchor(Transform parent)
        {
            if (anchor != null) return;
            var go = new GameObject("Runtime_LockAnchor");
            anchor = go.transform;
            anchor.position = parent.position;
            anchor.rotation = Quaternion.identity;
        }

        public void CreateAimProxy(Transform parent)
        {
            if (aimProxy != null) return;
            aimProxy = CreateProxy(parent, "Runtime_LockAimProxy");
        }

        public void CreateSoftLockViewProxies(Transform parent)
        {
            if (playerViewProxy == null)
                playerViewProxy = CreateProxy(parent, "Runtime_SoftLockPlayerViewProxy");
            if (enemyViewProxy == null)
                enemyViewProxy = CreateProxy(parent, "Runtime_SoftLockEnemyViewProxy");
            if (revealProxy == null)
                revealProxy = CreateProxy(parent, "Runtime_SoftLockRevealProxy");
        }

        private static Transform CreateProxy(Transform parent, string name)
        {
            var go = new GameObject(name);
            Transform proxy = go.transform;
            proxy.position = parent.position;
            proxy.rotation = Quaternion.identity;
            return proxy;
        }

        public void CreateTargetGroup(Transform parent)
        {
            if (targetGroup != null) return;
            var go = new GameObject("Runtime_LockTargetGroup");
            targetGroup = go.AddComponent<CinemachineTargetGroup>();
            targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
            targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
            targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
            targetGroupDirty = true;
        }

        public void DestroyRuntime()
        {
            if (targetGroup != null)
            {
                if (Application.isPlaying) Object.Destroy(targetGroup.gameObject);
                else Object.DestroyImmediate(targetGroup.gameObject);
                targetGroup = null;
            }
            if (revealProxy != null)
            {
                if (Application.isPlaying) Object.Destroy(revealProxy.gameObject);
                else Object.DestroyImmediate(revealProxy.gameObject);
                revealProxy = null;
            }
            if (enemyViewProxy != null)
            {
                if (Application.isPlaying) Object.Destroy(enemyViewProxy.gameObject);
                else Object.DestroyImmediate(enemyViewProxy.gameObject);
                enemyViewProxy = null;
            }
            if (playerViewProxy != null)
            {
                if (Application.isPlaying) Object.Destroy(playerViewProxy.gameObject);
                else Object.DestroyImmediate(playerViewProxy.gameObject);
                playerViewProxy = null;
            }
            if (aimProxy != null)
            {
                if (Application.isPlaying) Object.Destroy(aimProxy.gameObject);
                else Object.DestroyImmediate(aimProxy.gameObject);
                aimProxy = null;
            }
            if (anchor != null)
            {
                if (Application.isPlaying) Object.Destroy(anchor.gameObject);
                else Object.DestroyImmediate(anchor.gameObject);
                anchor = null;
            }
            trackedLockTarget = null;
        }
    }
}
