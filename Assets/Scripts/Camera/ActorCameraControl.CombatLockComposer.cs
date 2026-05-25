using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: CombatLockComposer
    // ==================================================================
    // Phase 0 split:
    // - SoftLock is owned by SoftLockComposer and does not use a camera anchor.
    // - HardLock keeps a simple anchor + TargetGroup path.
    // ==================================================================

    private class CombatLockComposer
    {
        private readonly ActorCameraControl _o;

        public CombatLockComposer(ActorCameraControl owner) { _o = owner; }

        public void ResetYawGateOnTargetChange(LockCameraRigRuntime rt, Transform newTarget)
        {
            if (rt == null) return;
            if (rt.trackedLockTarget != newTarget)
            {
                rt.prevAbsSectorDelta = -1f;
                rt.currentYawReturnSpeed = 0f;
                rt.yawReturnSpeedVelocity = 0f;
            }
        }

        public void UpdateCombatFollowAnchor(
            LockCameraRigRuntime rt, Transform enemyTarget,
            bool instant = false)
        {
            if (rt == null || enemyTarget == null || _o.actor == null) return;

            if (rt == _o._softRuntime)
            {
                _o.SoftComposer.Refresh(rt, enemyTarget, instant, updateStickySide: false);
                return;
            }

            if (rt.anchor == null) return;

            CombatFrame frame = BuildCombatFrame(enemyTarget);
            ApplyAnchorPose(rt, frame, instant);
            ConfigureTransposerForCombat(rt);
            ConfigureGroupComposerForCombat(rt);
            ApplyLockCameraFov(rt);
        }

        private struct CombatFrame
        {
            public Vector3 PlayerPos;
            public Vector3 EnemyPos;
            public Vector3 CombatDir;
            public Vector3 Center;
            public float Distance;
        }

        private CombatFrame BuildCombatFrame(Transform enemyTarget)
        {
            Vector3 playerPos = _o.transform.position;
            Vector3 enemyPos = enemyTarget.position;
            Vector3 playerXZ = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 enemyXZ = new Vector3(enemyPos.x, 0f, enemyPos.z);
            Vector3 toEnemy = enemyXZ - playerXZ;
            float distance = toEnemy.magnitude;
            Vector3 combatDir = distance > 0.01f ? toEnemy / distance : Vector3.forward;

            Vector3 center = Vector3.Lerp(playerPos, enemyPos, _o.lockCenterBias);
            center.y = (playerPos.y + enemyPos.y) * 0.5f + _o.heightOffset;

            return new CombatFrame
            {
                PlayerPos = playerPos,
                EnemyPos = enemyPos,
                CombatDir = combatDir,
                Center = center,
                Distance = distance
            };
        }

        private void ApplyAnchorPose(LockCameraRigRuntime rt, CombatFrame frame, bool instant)
        {
            Vector3 desiredAnchorPos = frame.Center;

            if (instant)
            {
                rt.anchor.position = desiredAnchorPos;
                rt.anchorPositionVelocity = Vector3.zero;
            }
            else
            {
                rt.anchor.position = Vector3.SmoothDamp(
                    rt.anchor.position,
                    desiredAnchorPos,
                    ref rt.anchorPositionVelocity,
                    Mathf.Max(0.001f, _o.positionSmoothTime));
            }

            rt.currentFollowDistance = ResolveFollowDistance(frame);

            float desiredYaw = Mathf.Atan2(frame.CombatDir.x, frame.CombatDir.z) * Mathf.Rad2Deg;
            if (instant)
            {
                rt.currentAnchorYaw = desiredYaw;
                rt.anchorYawVelocity = 0f;
            }
            else
            {
                rt.currentAnchorYaw = Mathf.SmoothDampAngle(
                    rt.currentAnchorYaw,
                    desiredYaw,
                    ref rt.anchorYawVelocity,
                    Mathf.Max(0.001f, _o.rotationSmoothTime));
            }

            rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);

            rt.dbgLabel = "HardLockBasic";
            rt.dbgCombatCenter = frame.Center;
            rt.dbgCombatDir = frame.CombatDir;
            rt.dbgCombatDist = frame.Distance;
            rt.dbgRawSide = 0f;
            rt.dbgSideAmount = 0f;
            rt.dbgDesiredAnchorPos = desiredAnchorPos;
            rt.dbgYawSource = "HardLockBasicYaw";
            rt.dbgSectorZone = "basic";
            rt.dbgTrend = "basic";
            rt.dbgCorrectionWeight = 0f;
            rt.dbgTargetReturnSpeed = 0f;
            rt.dbgYawAppliedDelta = 0f;
        }

        private float ResolveFollowDistance(CombatFrame frame)
        {
            return Mathf.Min(
                _o.lockBaseFollowDistance + frame.Distance * _o.lockDistancePerCombatMeter,
                _o.lockMaxFollowDistance);
        }

        // -- TargetGroup ------------------------------------------------

        public void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget, Enums.PlayerCameraState state)
        {
            if (rt == null || rt.targetGroup == null) return;

            if (rt == _o._softRuntime)
            {
                // SoftLockComposer owns the SoftLock target group.
                return;
            }

            bool lockMode = enemyTarget != null;
            Transform lockTarget = lockMode ? enemyTarget : null;
            int targetCount = lockMode ? 2 : 1;
            CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;

            bool needsRebuild = rt.targetGroupDirty
                || rt.trackedState != state
                || rt.trackedLockTarget != lockTarget
                || targets == null
                || targets.Length != targetCount
                || (targets.Length > 0 && targets[0].target != _o.transform);

            if (needsRebuild)
            {
                if (lockMode)
                {
                    rt.targetGroup.m_Targets = new[]
                    {
                        new CinemachineTargetGroup.Target { target = _o.transform, weight = _o.lockPlayerWeight, radius = _o.lockTargetPadding },
                        new CinemachineTargetGroup.Target { target = lockTarget, weight = _o.lockEnemyWeight, radius = _o.lockTargetPadding }
                    };
                }
                else
                {
                    rt.targetGroup.m_Targets = new[]
                    {
                        new CinemachineTargetGroup.Target { target = _o.transform, weight = _o.lockPlayerWeight, radius = _o.lockTargetPadding }
                    };
                }

                rt.trackedState = state;
                rt.trackedLockTarget = lockTarget;
                rt.targetGroupDirty = false;
                targets = rt.targetGroup.m_Targets;
            }

            if (lockMode && targets != null && targets.Length == 2)
            {
                targets[0].weight = _o.lockPlayerWeight;
                targets[0].radius = _o.lockTargetPadding;
                targets[1].weight = _o.lockEnemyWeight;
                targets[1].radius = _o.lockTargetPadding;
            }
            else if (!lockMode && targets != null && targets.Length >= 1)
            {
                targets[0].weight = _o.lockPlayerWeight;
                targets[0].radius = _o.lockTargetPadding;
            }
        }

        // -- Cinemachine helpers -----------------------------------------

        private void ConfigureTransposerForCombat(LockCameraRigRuntime rt)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;
            CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer == null) return;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = new Vector3(0f, 0f, -rt.currentFollowDistance);
        }

        private void ConfigureGroupComposerForCombat(LockCameraRigRuntime rt)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;

            var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
            if (groupComposer == null) return;

            groupComposer.m_GroupFramingSize = _o.lockFramingSize;
            groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyThenZoom;
            groupComposer.m_MaximumFOV = Mathf.Max(_o.lockFov, 68f);
            groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
        }

        private void ApplyLockCameraFov(LockCameraRigRuntime rt)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;

            var lens = vcam.m_Lens;
            lens.FieldOfView = _o.lockFov;
            vcam.m_Lens = lens;
        }
    }
}
