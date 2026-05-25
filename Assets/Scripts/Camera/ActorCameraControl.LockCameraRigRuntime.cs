using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: LockCameraRigRuntime
    // ==================================================================
    // Per-lock-camera runtime objects and smoothed state.
    // HardLock uses anchor + target group. SoftLock uses a follow frame plus
    // a target group. The follow frame provides a stable third-person rig
    // target for Cinemachine3rdPersonFollow.
    // ==================================================================

    private sealed class LockCameraRigRuntime
    {
        public Transform anchor;
        public Transform followFrame;
        public CinemachineTargetGroup targetGroup;

        public float followFrameYaw;
        public float followFrameYawVelocity;

        public bool targetGroupDirty = true;
        public Transform trackedLockTarget;
        public Enums.PlayerCameraState trackedState;

        // Smoothed values used by hard lock.
        public float smoothedSide;
        public float sideSmoothVelocity;
        public Vector3 anchorPositionVelocity = Vector3.zero;
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
        public float dbgSectorDelta;
        public bool dbgSectorInside;
        public float dbgSectorTargetYaw;
        public string dbgYawSource;
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
        public float prevAbsSectorDelta = -1f;
        public float currentYawReturnSpeed;
        public float yawReturnSpeedVelocity;
        public float dbgAbsSectorDelta;
        public float dbgPrevAbsSectorDelta;
        public float dbgCorrectionWeight;
        public float dbgHalfAngle;
        public float dbgInnerHoldHalfAngle;
        public string dbgSectorZone;
        public string dbgTrend;
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

        public void CreateSoftLockBasicRuntime(Transform parent)
        {
            DestroyAnchorOnly();
            CreateFollowFrame(parent);
            CreateTargetGroup(parent, "Runtime_SoftLockTargetGroup");
            if (targetGroup != null)
            {
                targetGroup.gameObject.name = "Runtime_SoftLockTargetGroup";
                targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupAverage;
                targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
                targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
            }
        }

        private void CreateFollowFrame(Transform parent)
        {
            if (followFrame != null) return;
            var go = new GameObject("Runtime_SoftLockFollowFrame");
            followFrame = go.transform;
            followFrame.position = parent.position;
            followFrame.rotation = Quaternion.identity;
            followFrameYaw = followFrame.eulerAngles.y;
            followFrameYawVelocity = 0f;
        }

        public void CreateTargetGroup(Transform parent)
        {
            CreateTargetGroup(parent, "Runtime_LockTargetGroup");
        }

        private void CreateTargetGroup(Transform parent, string name)
        {
            if (targetGroup != null) return;
            var go = new GameObject(name);
            targetGroup = go.AddComponent<CinemachineTargetGroup>();
            targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
            targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
            targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
            targetGroupDirty = true;
        }

        private void DestroyAnchorOnly()
        {
            if (anchor == null) return;
            if (Application.isPlaying) Object.Destroy(anchor.gameObject);
            else Object.DestroyImmediate(anchor.gameObject);
            anchor = null;
        }

        public void DestroyRuntime()
        {
            if (targetGroup != null)
            {
                if (Application.isPlaying) Object.Destroy(targetGroup.gameObject);
                else Object.DestroyImmediate(targetGroup.gameObject);
                targetGroup = null;
            }

            if (followFrame != null)
            {
                if (Application.isPlaying) Object.Destroy(followFrame.gameObject);
                else Object.DestroyImmediate(followFrame.gameObject);
                followFrame = null;
                followFrameYaw = 0f;
                followFrameYawVelocity = 0f;
            }

            DestroyAnchorOnly();
            trackedLockTarget = null;
        }
    }
}