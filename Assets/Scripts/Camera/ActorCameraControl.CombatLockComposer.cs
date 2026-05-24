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
        /// <summary>
        /// Call before UpdateCombatFollowAnchor when the lock target may have changed.
        /// Resets per-runtime yaw-gate trend and speed state so the new target starts clean.
        /// </summary>
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
            if (rt == null || rt.anchor == null || enemyTarget == null || _o.actor == null) return;

            CombatFrame frame = BuildCombatFrame(enemyTarget);
            float rawSide = ReadCameraSide(frame);
            UpdateShoulderSide(rt, rawSide, instant);

            float sideAmount = ResolveSideAmount(rt, frame);

            if (_o._diagnostics.ShouldCaptureDiagnostics)
            {
                rt.dbgLabel = rt == _o._softRuntime ? "SoftLock" : "HardLock";
                rt.dbgCombatCenter = frame.Center;
                rt.dbgCombatDir = frame.CombatDir;
                rt.dbgCombatDist = frame.Distance;
                rt.dbgRawSide = rawSide;
                rt.dbgSideAmount = sideAmount;
                rt.dbgDesiredAnchorPos = frame.Center + frame.Right * sideAmount;
            }

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

        private float ResolveSideAmount(LockCameraRigRuntime rt, CombatFrame frame)
        {
            float sideSign = rt.smoothedSide >= 0f ? 1f : -1f;
            return Mathf.Min(frame.Distance * _o.lockSideBias, frame.Distance * 0.5f) * sideSign;
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

            // Formula target yaw (fallback / instant init)
            Vector3 desiredCamPos = frame.Center
                - frame.CombatDir * (rt.currentFollowDistance * 0.6f)
                + frame.Right * (sideSign * rt.currentFollowDistance * 0.5f);
            desiredCamPos.y = frame.Center.y + _o.heightOffset * 0.3f;

            float formulaYaw;
            {
                Vector3 toCam = desiredCamPos - rt.anchor.position;
                formulaYaw = toCam.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(-toCam.normalized.x, -toCam.normalized.z) * Mathf.Rad2Deg
                    : rt.currentAnchorYaw;
            }

            rt.dbgYawBefore = rt.currentAnchorYaw;
            rt.dbgFormulaYaw = formulaYaw;

            if (instant)
            {
                rt.currentAnchorYaw = formulaYaw;
                rt.anchorYawVelocity = 0f;
                rt.dbgYawSource = "InstantFormula";
                // Reset damped-return state.
                rt.currentYawReturnSpeed = 0f;
                rt.yawReturnSpeedVelocity = 0f;
                rt.prevAbsSectorDelta = -1f;
                rt.dbgTrend = "init";
                rt.dbgTargetReturnSpeed = 0f;
                rt.dbgYawAppliedDelta = 0f;
            }
            else
            {
                rt.currentAnchorYaw = ResolveSectorGatedYaw(
                    rt, frame, formulaYaw);
            }

            rt.dbgYawAfter = rt.currentAnchorYaw;
            rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);
        }

        // Damped-return speed smoothing time (seconds).
        private const float ReturnSpeedSmoothTime = 0.25f;
        // Minimum abs-delta change (degrees) to classify as outward/inward.
        private const float TrendEpsilon = 0.1f;

        private float ResolveSectorGatedYaw(
            LockCameraRigRuntime rt, CombatFrame frame, float formulaYaw)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                rt.dbgSectorInside = false;
                rt.dbgSectorTargetYaw = formulaYaw;
                rt.dbgYawSource = "NoCameraFallback";
                rt.dbgBoundaryYaw = formulaYaw;
                rt.dbgCorrectionWeight = 0f;
                rt.dbgHalfAngle = 0f;
                rt.dbgInnerHoldHalfAngle = 0f;
                rt.dbgSectorZone = "NoCamera";
                rt.dbgBoundaryDirYaw = 0f;
                rt.dbgBoundaryCamPos = Vector3.zero;
                rt.dbgBoundaryRadius = 0f;
                rt.dbgTrend = "init";
                rt.dbgTargetReturnSpeed = 0f;
                rt.dbgYawAppliedDelta = 0f;
                rt.currentYawReturnSpeed = 0f;
                rt.yawReturnSpeedVelocity = 0f;
                rt.prevAbsSectorDelta = -1f;
                return formulaYaw;
            }

            Vector3 enemyXZ = new Vector3(frame.PlayerPos.x, 0f, frame.PlayerPos.z)
                + frame.CombatDir * frame.Distance;
            Vector3 playerFlat = new Vector3(frame.PlayerPos.x, 0f, frame.PlayerPos.z);
            Vector3 eToPlayer = (playerFlat - enemyXZ).normalized;
            Vector3 camFlat = new Vector3(mainCam.transform.position.x, 0f, mainCam.transform.position.z);
            Vector3 eToCam = (camFlat - enemyXZ).normalized;

            float sectorDelta = Vector3.SignedAngle(eToPlayer, eToCam, Vector3.up);
            float halfAngle = _o.lockYawSectorHalfAngle;
            float absDelta = Mathf.Abs(sectorDelta);

            // --- detect trend ---
            float prevAbsDelta = rt.prevAbsSectorDelta;
            string trend;
            if (prevAbsDelta < -0.5f)
            {
                trend = "init";
            }
            else if (absDelta > prevAbsDelta + TrendEpsilon)
            {
                trend = "outward";
            }
            else if (absDelta < prevAbsDelta - TrendEpsilon)
            {
                trend = "inward";
            }
            else
            {
                trend = "stable";
            }

            rt.dbgAbsSectorDelta = absDelta;
            rt.dbgPrevAbsSectorDelta = rt.prevAbsSectorDelta; // snapshot BEFORE mutation
            rt.prevAbsSectorDelta = absDelta;
            rt.dbgTrend = trend;

            // --- soft edge parameters ---
            float safeInnerOffset = Mathf.Clamp(_o.lockYawSectorInnerOffset, 0f, halfAngle);
            float innerHoldHalfAngle = halfAngle - safeInnerOffset;

            // --- raw input diagnostics ---
            rt.dbgSectorDelta = sectorDelta;
            rt.dbgSectorInside = absDelta <= halfAngle;
            rt.dbgEnemyToPlayerYaw = Mathf.Atan2(eToPlayer.x, eToPlayer.z) * Mathf.Rad2Deg;
            rt.dbgEnemyToCameraYaw = Mathf.Atan2(eToCam.x, eToCam.z) * Mathf.Rad2Deg;
            rt.dbgHalfAngle = halfAngle;
            rt.dbgInnerHoldHalfAngle = innerHoldHalfAngle;

            // --- resolve zone and static correction weight ---
            float correctionWeight;
            string yawSource;
            string sectorZone;

            if (absDelta <= innerHoldHalfAngle)
            {
                correctionWeight = 0f;
                yawSource = "InsideHold";
                sectorZone = "hold";
            }
            else if (absDelta <= halfAngle)
            {
                float t = (absDelta - innerHoldHalfAngle) / Mathf.Max(safeInnerOffset, 0.001f);
                correctionWeight = Mathf.SmoothStep(0f, 1f, t);
                yawSource = "SoftEdge";
                sectorZone = "soft";
            }
            else
            {
                correctionWeight = 1f;
                yawSource = "OutsideBoundary";
                sectorZone = "outside";
            }

            rt.dbgCorrectionWeight = correctionWeight;
            rt.dbgSectorZone = sectorZone;

            // --- resolve target return speed ---
            float maxSpeed = _o.lockYawSectorReturnSpeed;
            float targetReturnSpeed;
            bool shouldCorrect;

            if (correctionWeight <= 0f)
            {
                targetReturnSpeed = 0f;
                shouldCorrect = false;
            }
            else if (trend == "inward")
            {
                // Player is moving back toward sector center — stop correcting.
                targetReturnSpeed = 0f;
                shouldCorrect = false;
            }
            else
            {
                // Outward, stable, or init: apply weighted speed.
                targetReturnSpeed = correctionWeight * maxSpeed;
                shouldCorrect = true;
            }

            // --- dampen return speed (always, for debug continuity) ---
            rt.currentYawReturnSpeed = Mathf.SmoothDamp(
                rt.currentYawReturnSpeed, targetReturnSpeed,
                ref rt.yawReturnSpeedVelocity, ReturnSpeedSmoothTime);

            rt.dbgTargetReturnSpeed = targetReturnSpeed;

            if (!shouldCorrect)
            {
                // Don't apply yaw correction even if residual speed exists.
                // Speed damping continues for debug, but MoveTowardsAngle is skipped.
                rt.dbgYawSource = yawSource;
                rt.dbgSectorTargetYaw = formulaYaw;
                rt.dbgBoundaryYaw = formulaYaw;
                rt.dbgBoundaryDirYaw = 0f;
                rt.dbgBoundaryCamPos = Vector3.zero;
                rt.dbgBoundaryRadius = 0f;
                rt.dbgYawAppliedDelta = 0f;
                return rt.currentAnchorYaw;
            }

            float speedStep = rt.currentYawReturnSpeed * Time.deltaTime;
            if (speedStep <= 0.0001f)
            {
                rt.dbgYawSource = yawSource;
                rt.dbgSectorTargetYaw = formulaYaw;
                rt.dbgBoundaryYaw = formulaYaw;
                rt.dbgBoundaryDirYaw = 0f;
                rt.dbgBoundaryCamPos = Vector3.zero;
                rt.dbgBoundaryRadius = 0f;
                rt.dbgYawAppliedDelta = 0f;
                return rt.currentAnchorYaw;
            }

            // --- compute boundary yaw on outer sector edge ---
            float boundarySign = Mathf.Sign(sectorDelta);
            float boundaryAngle = boundarySign * halfAngle;
            Vector3 boundaryDir = Quaternion.Euler(0f, boundaryAngle, 0f) * eToPlayer;

            Vector3 anchorXZ = new Vector3(rt.anchor.position.x, 0f, rt.anchor.position.z);
            float followDistance = Mathf.Max(rt.currentFollowDistance, 0.01f);
            float currentBoundaryRadius = Vector3.Dot(camFlat - enemyXZ, boundaryDir);
            float boundaryRadius = ResolveBoundaryRadiusOnFollowCircle(
                enemyXZ, anchorXZ, boundaryDir, followDistance, currentBoundaryRadius);

            if (boundaryRadius <= 0.01f)
            {
                boundaryRadius = (camFlat - enemyXZ).magnitude;
            }

            if (boundaryRadius <= 0.01f)
            {
                boundaryRadius = followDistance;
            }

            Vector3 boundaryCamPos = enemyXZ + boundaryDir * boundaryRadius;
            boundaryCamPos.y = frame.Center.y + _o.heightOffset * 0.3f;

            Vector3 toCam = boundaryCamPos - rt.anchor.position;
            float boundaryYaw = toCam.sqrMagnitude > 0.001f
                ? Mathf.Atan2(-toCam.normalized.x, -toCam.normalized.z) * Mathf.Rad2Deg
                : rt.currentAnchorYaw;

            float yawBefore = rt.currentAnchorYaw;
            float yawAfter = Mathf.MoveTowardsAngle(yawBefore, boundaryYaw, speedStep);

            // --- output diagnostics ---
            rt.dbgYawSource = yawSource;
            rt.dbgSectorTargetYaw = boundaryYaw;
            rt.dbgBoundaryYaw = boundaryYaw;
            rt.dbgBoundaryDirYaw = Mathf.Atan2(boundaryDir.x, boundaryDir.z) * Mathf.Rad2Deg;
            rt.dbgBoundaryCamPos = boundaryCamPos;
            rt.dbgBoundaryRadius = boundaryRadius;
            rt.dbgYawAppliedDelta = Mathf.DeltaAngle(yawBefore, yawAfter);

            return yawAfter;
        }

        private static float ResolveBoundaryRadiusOnFollowCircle(
            Vector3 enemyXZ, Vector3 anchorXZ, Vector3 boundaryDir,
            float followDistance, float currentBoundaryRadius)
        {
            Vector3 enemyToAnchor = enemyXZ - anchorXZ;
            float b = Vector3.Dot(boundaryDir, enemyToAnchor);
            float c = enemyToAnchor.sqrMagnitude - followDistance * followDistance;
            float discriminant = b * b - c;

            if (discriminant < 0f)
            {
                return 0f;
            }

            float root = Mathf.Sqrt(discriminant);
            float nearRadius = -b - root;
            float farRadius = -b + root;
            return PickPositiveBoundaryRadius(nearRadius, farRadius, currentBoundaryRadius);
        }

        private static float PickPositiveBoundaryRadius(
            float first, float second, float reference)
        {
            const float minRadius = 0.01f;
            bool firstValid = first > minRadius;
            bool secondValid = second > minRadius;

            if (firstValid && secondValid)
            {
                if (reference > minRadius)
                {
                    return Mathf.Abs(first - reference) <= Mathf.Abs(second - reference)
                        ? first
                        : second;
                }

                return Mathf.Max(first, second);
            }

            if (firstValid) return first;
            if (secondValid) return second;
            return 0f;
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
                bool targetChanged = rt.trackedLockTarget != lockTarget;

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

                // Reset yaw-gate damped-return state when the lock target changes
                // so trend detection and residual speed don't carry over.
                if (targetChanged)
                {
                    rt.prevAbsSectorDelta = -1f;
                    rt.currentYawReturnSpeed = 0f;
                    rt.yawReturnSpeedVelocity = 0f;
                }
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
