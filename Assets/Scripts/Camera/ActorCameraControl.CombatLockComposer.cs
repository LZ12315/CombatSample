using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: CombatLockComposer
    // ==================================================================
    // Owns combat anchor math, TargetGroup weights/radii,
    // Transposer/GroupComposer/FramingTransposer config, and lock FOV.
    // ==================================================================

    private class CombatLockComposer
    {
        private readonly ActorCameraControl _o;

        public CombatLockComposer(ActorCameraControl owner) { _o = owner; }

        // -- Combat follow anchor ---------------------------------------

        /// <summary>
        /// Updates the lock camera in clear layers: combat frame, side amount,
        /// anchor pose, then Cinemachine settings.
        /// </summary>
        public void UpdateCombatFollowAnchor(
            LockCameraRigRuntime rt, Transform enemyTarget,
            bool instant = false, LockCameraUpdateMode mode = LockCameraUpdateMode.Formula)
        {
            if (rt == null || rt.anchor == null || enemyTarget == null || _o.actor == null) return;

            CombatFrame frame = BuildCombatFrame(enemyTarget);
            float rawSide = ReadCameraSide(frame);
            UpdateShoulderSide(rt, rawSide, instant);

            float sideAmount = ResolveSideAmount(rt, frame, instant, mode);
            ApplyAnchorPose(rt, frame, sideAmount, instant);
            ApplyCinemachineSettings(rt);
        }

        private struct CombatFrame
        {
            public Vector3 PlayerPos;
            public Vector3 CombatDir;
            public Vector3 Right;
            public Vector3 Center;
            public float Distance;
        }

        private CombatFrame BuildCombatFrame(Transform enemyTarget)
        {
            Vector3 playerPos = _o.transform.position;
            Vector3 enemyPos = enemyTarget.position;

            Vector3 playerPosXZ = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 enemyPosXZ = new Vector3(enemyPos.x, 0f, enemyPos.z);
            Vector3 combatDir = (enemyPosXZ - playerPosXZ).normalized;
            float combatDist = Vector3.Distance(playerPosXZ, enemyPosXZ);

            if (combatDist < 0.01f)
            {
                Camera cam = Camera.main;
                combatDir = cam != null ? cam.transform.forward : Vector3.forward;
                combatDir.y = 0f;
                combatDir.Normalize();
            }

            Vector3 right = Vector3.Cross(Vector3.up, combatDir).normalized;

            float forwardBias = combatDist * _o.lockCenterBias;
            Vector3 center = playerPosXZ + combatDir * forwardBias;
            center.y = (playerPos.y + enemyPos.y) * 0.5f + _o.heightOffset;

            return new CombatFrame
            {
                PlayerPos = playerPos,
                CombatDir = combatDir,
                Right = right,
                Center = center,
                Distance = combatDist
            };
        }

        private float ReadCameraSide(CombatFrame frame)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return 0f;

            Vector3 playerToCam = mainCam.transform.position - frame.PlayerPos;
            playerToCam.y = 0f;
            if (playerToCam.sqrMagnitude <= 0.001f) return 0f;

            return Vector3.Dot(frame.Right, playerToCam.normalized);
        }

        private void UpdateShoulderSide(LockCameraRigRuntime rt, float rawSide, bool instant)
        {
            if (instant)
            {
                rt.smoothedSide = rawSide;
                rt.sideSmoothVelocity = 0f;
                rt.anchorPositionVelocity = Vector3.zero;
                rt.anchorYawVelocity = 0f;
                return;
            }

            const float shoulderDeadZone = 0.15f;
            float sideDelta = Mathf.Abs(rawSide - rt.smoothedSide);
            if (sideDelta > shoulderDeadZone
                || Mathf.Abs(rawSide) < 0.05f
                || Mathf.Abs(rt.smoothedSide) < 0.05f)
            {
                rt.smoothedSide = Mathf.SmoothDamp(
                    rt.smoothedSide, rawSide,
                    ref rt.sideSmoothVelocity, _o.sideSmoothTime);
            }
        }

        private float ResolveSideAmount(
            LockCameraRigRuntime rt, CombatFrame frame,
            bool instant, LockCameraUpdateMode mode)
        {
            float sideSign = rt.smoothedSide >= 0f ? 1f : -1f;
            float formulaSideAmount = Mathf.Min(frame.Distance * _o.lockSideBias, frame.Distance * 0.5f) * sideSign;

            if (mode != LockCameraUpdateMode.LiveSoftLock)
            {
                rt.currentSideAmount = formulaSideAmount;
                return rt.currentSideAmount;
            }

            if (instant)
            {
                rt.currentSideAmount = formulaSideAmount;
                return rt.currentSideAmount;
            }

            float sideGap = formulaSideAmount - rt.currentSideAmount;
            if (Mathf.Abs(sideGap) > _o.softLockSideDeadZone)
            {
                float sideTarget = formulaSideAmount
                    - Mathf.Sign(sideGap) * _o.softLockSideDeadZone;
                float maxStep = _o.softLockSideCatchUpSpeed * Time.deltaTime;
                rt.currentSideAmount = Mathf.MoveTowards(rt.currentSideAmount, sideTarget, maxStep);
            }

            return rt.currentSideAmount;
        }

        private void ApplyAnchorPose(
            LockCameraRigRuntime rt, CombatFrame frame,
            float sideAmount, bool instant)
        {
            Vector3 desiredAnchorPos = frame.Center + frame.Right * sideAmount;
            desiredAnchorPos.y = frame.Center.y;

            if (instant)
            {
                rt.anchor.position = desiredAnchorPos;
            }
            else
            {
                rt.anchor.position = Vector3.SmoothDamp(
                    rt.anchor.position, desiredAnchorPos,
                    ref rt.anchorPositionVelocity, _o.positionSmoothTime);
            }

            rt.currentFollowDistance = ResolveFollowDistance(frame);

            float sideSign = Mathf.Abs(sideAmount) > 0.001f
                ? Mathf.Sign(sideAmount)
                : (rt.smoothedSide >= 0f ? 1f : -1f);
            Vector3 desiredCamPos = frame.Center
                - frame.CombatDir * (rt.currentFollowDistance * 0.6f)
                + frame.Right * (sideSign * rt.currentFollowDistance * 0.5f);
            desiredCamPos.y = frame.Center.y + _o.heightOffset * 0.3f;

            Vector3 toCamera = desiredCamPos - rt.anchor.position;
            if (toCamera.sqrMagnitude <= 0.001f) return;

            Vector3 anchorForward = -toCamera.normalized;
            float targetYaw = Mathf.Atan2(anchorForward.x, anchorForward.z) * Mathf.Rad2Deg;
            rt.currentAnchorYaw = instant
                ? targetYaw
                : Mathf.SmoothDampAngle(rt.currentAnchorYaw, targetYaw,
                    ref rt.anchorYawVelocity, _o.rotationSmoothTime);
            rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);
        }

        private void ApplyCinemachineSettings(LockCameraRigRuntime rt)
        {
            ConfigureTransposerForCombat(rt);
            ConfigureGroupComposerForCombat(rt);
            ApplyLockCameraFov(rt);
        }

        private float ResolveFollowDistance(CombatFrame frame)
        {
            return Mathf.Min(
                _o.lockBaseFollowDistance + frame.Distance * _o.lockDistancePerCombatMeter,
                _o.lockMaxFollowDistance);
        }

        private float ResolveFov()
        {
            return _o.lockFov;
        }

        // -- TargetGroup ------------------------------------------------

        public void RefreshTargetGroup(LockCameraRigRuntime rt, Transform enemyTarget, Enums.PlayerCameraState state)
        {
            if (rt == null || rt.targetGroup == null) return;

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
                _o._diagnostics.LogCameraEvent($"RefreshTargetGroup rebuild lockMode={lockMode} targetCount={targetCount} lockTarget={ActorCameraControl.FormatObjectName(lockTarget)}");

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

            float framingSize = _o.lockFramingSize;
            float maxFov = Mathf.Max(_o.lockFov, 68f);

            var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
            if (groupComposer != null)
            {
                groupComposer.m_GroupFramingSize = framingSize;
                groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyThenZoom;
                groupComposer.m_MaximumFOV = maxFov;
                groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
                return;
            }

            var framingTransposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framingTransposer != null)
            {
                framingTransposer.m_GroupFramingSize = framingSize;
                framingTransposer.m_AdjustmentMode = CinemachineFramingTransposer.AdjustmentMode.DollyThenZoom;
                framingTransposer.m_MaximumFOV = maxFov;
            }
        }

        private void ApplyLockCameraFov(LockCameraRigRuntime rt)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;

            float targetFov = ResolveFov();

            var lens = vcam.m_Lens;
            lens.FieldOfView = targetFov;
            vcam.m_Lens = lens;
        }
    }
}
