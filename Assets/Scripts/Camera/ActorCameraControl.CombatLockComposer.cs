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

        // -- Composition interpolation ----------------------------------

        private float ComputeCompositionT(float combatDist)
        {
            return Mathf.InverseLerp(_o.compositionNearDist, _o.compositionFarDist, combatDist);
        }

        // -- Combat follow anchor ---------------------------------------

        public void UpdateCombatFollowAnchor(LockCameraRigRuntime rt, Transform enemyTarget, bool instant = false)
        {
            if (rt == null || rt.anchor == null || enemyTarget == null || _o.actor == null) return;

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

            // ---- side tracking with dead zone --------------------------
            // Only update the side target when the camera crosses a
            // meaningful threshold. This prevents the micro‑adjustments
            // that make the soft‑lock feel mechanical.
            Camera mainCam = Camera.main;
            float rawSide = 0f;
            if (mainCam != null)
            {
                Vector3 playerToCam = mainCam.transform.position - playerPos;
                playerToCam.y = 0f;
                if (playerToCam.sqrMagnitude > 0.001f)
                    rawSide = Vector3.Dot(right, playerToCam.normalized);
            }

            if (instant)
            {
                rt.smoothedSide = rawSide;
                rt.sideSmoothVelocity = 0f;
                rt.anchorPositionVelocity = Vector3.zero;
                rt.anchorYawVelocity = 0f;
            }
            else
            {
                const float sideDeadZone = 0.15f;
                float sideDelta = Mathf.Abs(rawSide - rt.smoothedSide);
                if (sideDelta > sideDeadZone
                    || Mathf.Abs(rawSide) < 0.05f
                    || Mathf.Abs(rt.smoothedSide) < 0.05f)
                {
                    rt.smoothedSide = Mathf.SmoothDamp(
                        rt.smoothedSide, rawSide,
                        ref rt.sideSmoothVelocity, _o.sideSmoothTime);
                }
            }

            float sideSign = rt.smoothedSide >= 0f ? 1f : -1f;

            // ---- anchor position ---------------------------------------
            float t = ComputeCompositionT(combatDist);
            float centerBias = Mathf.Lerp(_o.centerBiasNear, _o.centerBiasFar, t);
            float forwardBias = combatDist * centerBias;
            Vector3 combatCenter = playerPosXZ + combatDir * forwardBias;
            combatCenter.y = (playerPos.y + enemyPos.y) * 0.5f + _o.heightOffset;

            float sideScale = Mathf.Lerp(_o.sideBiasNear, _o.sideBiasFar, t);
            float sideAmount = Mathf.Min(combatDist * sideScale, combatDist * 0.5f) * sideSign;
            Vector3 desiredAnchorPos = combatCenter + right * sideAmount;
            desiredAnchorPos.y = combatCenter.y;

            if (instant)
                rt.anchor.position = desiredAnchorPos;
            else
                rt.anchor.position = Vector3.SmoothDamp(
                    rt.anchor.position, desiredAnchorPos,
                    ref rt.anchorPositionVelocity, _o.positionSmoothTime);

            // ---- follow distance & FOV (distance‑driven) --------------
            rt.currentFollowDistance = Mathf.Lerp(_o.followDistNear, _o.followDistFar, t);

            // Anchor yaw: face toward the combat center from behind
            Vector3 desiredCamPos = combatCenter
                - combatDir * (rt.currentFollowDistance * 0.6f)
                + right * (sideSign * rt.currentFollowDistance * 0.5f);
            desiredCamPos.y = combatCenter.y + _o.heightOffset * 0.3f;

            Vector3 toCamera = desiredCamPos - rt.anchor.position;
            if (toCamera.sqrMagnitude > 0.001f)
            {
                Vector3 anchorForward = -toCamera.normalized;
                float targetYaw = Mathf.Atan2(anchorForward.x, anchorForward.z) * Mathf.Rad2Deg;
                rt.currentAnchorYaw = instant
                    ? targetYaw
                    : Mathf.SmoothDampAngle(rt.currentAnchorYaw, targetYaw, ref rt.anchorYawVelocity, _o.rotationSmoothTime);
                rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);
            }

            ConfigureTransposerForCombat(rt);
            ConfigureGroupComposerForCombat(rt, combatDist);
            ApplyLockCameraFov(rt, combatDist);
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
                        new CinemachineTargetGroup.Target { target = _o.transform, weight = _o.playerWeightNear, radius = _o.playerRadiusNear },
                        new CinemachineTargetGroup.Target { target = lockTarget, weight = _o.enemyWeightNear, radius = _o.enemyRadiusNear }
                    };
                }
                else
                {
                    rt.targetGroup.m_Targets = new[]
                    {
                        new CinemachineTargetGroup.Target { target = _o.transform, weight = _o.playerWeightNear, radius = _o.playerRadiusNear }
                    };
                }

                rt.trackedState = state;
                rt.trackedLockTarget = lockTarget;
                rt.targetGroupDirty = false;
                targets = rt.targetGroup.m_Targets;
            }

            // Per-frame: update weights/radii to match combat distance.
            // Player weight is always >= enemy weight to keep the player as primary subject.
            if (lockMode && targets != null && targets.Length == 2)
            {
                float dist = Vector3.Distance(_o.transform.position, lockTarget.position);
                float t = ComputeCompositionT(dist);

                targets[0].weight = Mathf.Lerp(_o.playerWeightNear, _o.playerWeightFar, t);
                targets[0].radius = Mathf.Lerp(_o.playerRadiusNear, _o.playerRadiusFar, t);
                targets[1].weight = Mathf.Lerp(_o.enemyWeightNear, _o.enemyWeightFar, t);
                targets[1].radius = Mathf.Lerp(_o.enemyRadiusNear, _o.enemyRadiusFar, t);
            }
            else if (!lockMode && targets != null && targets.Length >= 1)
            {
                targets[0].weight = _o.playerWeightNear;
                targets[0].radius = _o.playerRadiusNear;
            }
        }

        // -- Transposer / Composer --------------------------------------

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

        private void ConfigureGroupComposerForCombat(LockCameraRigRuntime rt, float combatDist)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;

            float t = ComputeCompositionT(combatDist);
            float framingSize = Mathf.Lerp(_o.framingSizeNear, _o.framingSizeFar, t);
            float maxFov = Mathf.Max(_o.fovFar, 68f);

            // CinemachineGroupComposer (Aim component)
            var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
            if (groupComposer != null)
            {
                groupComposer.m_GroupFramingSize = framingSize;
                groupComposer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyThenZoom;
                groupComposer.m_MaximumFOV = maxFov;
                groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
                return;
            }

            // CinemachineFramingTransposer (Body component)
            var framingTransposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framingTransposer != null)
            {
                framingTransposer.m_GroupFramingSize = framingSize;
                framingTransposer.m_AdjustmentMode = CinemachineFramingTransposer.AdjustmentMode.DollyThenZoom;
                framingTransposer.m_MaximumFOV = maxFov;
            }
        }

        private void ApplyLockCameraFov(LockCameraRigRuntime rt, float combatDist)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;

            float t = ComputeCompositionT(combatDist);
            float targetFov = Mathf.Lerp(_o.fovNear, _o.fovFar, t);

            var lens = vcam.m_Lens;
            lens.FieldOfView = targetFov;
            vcam.m_Lens = lens;
        }
    }
}
