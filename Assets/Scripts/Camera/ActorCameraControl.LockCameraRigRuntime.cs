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
        public CinemachineTargetGroup targetGroup;

        public bool targetGroupDirty = true;
        public Transform trackedLockTarget;
        public Enums.PlayerCameraState trackedState;

        // Smoothed values
        public float smoothedSide;
        public float sideSmoothVelocity;
        public Vector3 anchorPositionVelocity = Vector3.zero;
        public float anchorYawVelocity;
        public float currentAnchorYaw;
        public float currentFollowDistance = 8f;

        // Current lateral offset from the combat center.
        public float currentSideAmount;

        public void CreateAnchor(Transform parent)
        {
            if (anchor != null) return;
            var go = new GameObject("Runtime_LockAnchor");
            anchor = go.transform;
            anchor.position = parent.position;
            anchor.rotation = Quaternion.identity;
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
