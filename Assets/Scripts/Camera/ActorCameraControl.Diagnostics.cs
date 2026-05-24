using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: CameraDiagnostics
    // ==================================================================
    // Isolated debug logging. All formatting and snapshot logic lives here
    // so it doesn't clutter the main camera flow.
    // ==================================================================

    private class CameraDiagnostics
    {
        private readonly ActorCameraControl _o;

        private int _debugWindowEndFrame = -1;
        private int _debugTransitionId;

        public CameraDiagnostics(ActorCameraControl owner) { _o = owner; }

        /// <summary>
        /// Returns true when any debug path that reads dbg* snapshot fields
        /// is active. CombatLockComposer uses this to skip diagnostic writes
        /// when debug is fully off.
        /// </summary>
        public bool ShouldCaptureDiagnostics =>
            _o.debugLockCameraGizmos
            || _o.debugBrainAfterUpdate
            || ShouldLogCameraDebug();

        // -- Brain callback ---------------------------------------------

        public void OnCinemachineBrainUpdated(CinemachineBrain brain)
        {
            if (!_o.debugBrainAfterUpdate) return;
            LogCameraSnapshot("Brain.AfterUpdate", _o.GetCombatTarget(), brain, force: true);
        }

        // -- Debug window -----------------------------------------------

        public void StartCameraDebugWindow(string reason)
        {
            if (!_o.debugCameraTransitions) return;
            _debugTransitionId++;
            _debugWindowEndFrame = Mathf.Max(
                _debugWindowEndFrame,
                Time.frameCount + Mathf.Max(1, _o.debugFramesAfterTransition));
            Debug.Log($"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] BEGIN {reason}", _o);
        }

        private bool ShouldLogCameraDebug()
        {
            return _o.debugCameraTransitions
                && (_o.debugCameraEveryLateUpdate || Time.frameCount <= _debugWindowEndFrame);
        }

        // -- Event logging ----------------------------------------------

        public void LogCameraEvent(string message)
        {
            if (!ShouldLogCameraDebug()) return;
            Debug.Log($"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] {message}", _o);
        }

        // -- Snapshot ---------------------------------------------------

        public void LogCameraSnapshot(
            string phase, Transform enemyTarget,
            CinemachineBrain brainOverride = null, bool force = false)
        {
            if (!force && !ShouldLogCameraDebug()) return;

            Camera mainCam = Camera.main;
            CinemachineBrain brain = brainOverride ?? (mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null);

            string mainInfo = mainCam != null
                ? $"{FormatVector(mainCam.transform.position)} yaw={FormatYaw(mainCam.transform.rotation)}"
                : "null";

            string softAnchorInfo = _o._softRuntime?.anchor != null
                ? $"{FormatVector(_o._softRuntime.anchor.position)} yaw={FormatYaw(_o._softRuntime.anchor.rotation)}"
                : "null";
            string hardAnchorInfo = _o._hardRuntime?.anchor != null
                ? $"{FormatVector(_o._hardRuntime.anchor.position)} yaw={FormatYaw(_o._hardRuntime.anchor.rotation)}"
                : "null";

            string enemyInfo = enemyTarget != null
                ? $"{enemyTarget.name}@{FormatVector(enemyTarget.position)}"
                : "null";

            Debug.Log(
                $"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] {phase}\n" +
                $"  state current={_o.currentState} resolved={_o.ResolvePresentationState()} lock={_o.FormatLockMode()} combatTarget={ActorCameraControl.FormatObjectName(_o.GetCombatTarget())}\n" +
                $"  main={mainInfo} brain={FormatBrain(brain)}\n" +
                $"  player={FormatVector(_o.transform.position)} enemy={enemyInfo} distances={FormatDistances(enemyTarget, mainCam)}\n" +
                $"  softAnchor={softAnchorInfo} softFollowDist={_o._softRuntime?.currentFollowDistance ?? 0f:F2} softSmoothedSide={_o._softRuntime?.smoothedSide ?? 0f:F2} softTG={FormatTargetGroupFor(_o._softRuntime)}\n" +
                $"  hardAnchor={hardAnchorInfo} hardFollowDist={_o._hardRuntime?.currentFollowDistance ?? 0f:F2} hardSmoothedSide={_o._hardRuntime?.smoothedSide ?? 0f:F2} hardTG={FormatTargetGroupFor(_o._hardRuntime)}\n" +
                $"  lockDiag soft={FormatLockDiagnosticsFor(_o._softRuntime)}\n" +
                $"  lockDiag hard={FormatLockDiagnosticsFor(_o._hardRuntime)}\n" +
                $"  free={FormatCamera(_o.normalFreeLookCamera, brain)}\n" +
                $"  soft={FormatCamera(_o.softLockCamera, brain)}\n" +
                $"  hard={FormatCamera(_o.hardLockCamera, brain)}",
                _o);
        }

        // -- Formatting helpers -----------------------------------------

        private static string FormatBrain(CinemachineBrain brain)
        {
            if (brain == null) return "null";
            string activeName = brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "null";
            string blend = brain.ActiveBlend != null ? brain.ActiveBlend.Description : "none";
            return $"{brain.name} active={activeName} blending={brain.IsBlending} blend={blend}";
        }

        private static string FormatCamera(ICinemachineCamera camera, CinemachineBrain brain)
        {
            if (camera == null) return "null";
            string liveInfo = brain != null
                ? $" live={brain.IsLive(camera)} liveBlend={brain.IsLiveInBlend(camera)}"
                : string.Empty;
            return $"{camera.Name} P={camera.Priority}{liveInfo} follow={ActorCameraControl.FormatObjectName(camera.Follow)} lookAt={ActorCameraControl.FormatObjectName(camera.LookAt)} raw={FormatVector(camera.State.RawPosition)} final={FormatVector(camera.State.FinalPosition)}";
        }

        private string FormatTargetGroupFor(LockCameraRigRuntime rt)
        {
            if (rt?.targetGroup == null) return "null";
            return $"pos={FormatVector(rt.targetGroup.transform.position)} targets={FormatTargetGroupTargetsFor(rt)}";
        }

        private static string FormatLockDiagnosticsFor(LockCameraRigRuntime rt)
        {
            if (rt == null) return "null";
            string label = string.IsNullOrEmpty(rt.dbgLabel) ? "uninitialized" : rt.dbgLabel;
            string activeTag = rt.dbgIsActiveRuntime ? "active" : "prewarm";
            return $"[{label}:{activeTag}] center={FormatVector(rt.dbgCombatCenter)} " +
                   $"dir={FormatVector(rt.dbgCombatDir)} dist={rt.dbgCombatDist:F2} " +
                   $"rawSide={rt.dbgRawSide:F2} sideAmount={rt.dbgSideAmount:F2} " +
                   $"desAnchor={FormatVector(rt.dbgDesiredAnchorPos)} tgPos={FormatVector(rt.dbgTargetGroupPos)}\n" +
                   $"  yawGate src={rt.dbgYawSource} zone={rt.dbgSectorZone} trend={rt.dbgTrend} " +
                   $"before={rt.dbgYawBefore:F1}° after={rt.dbgYawAfter:F1}° " +
                   $"appliedΔ={rt.dbgYawAppliedDelta:F3}°\n" +
                   $"  formula={rt.dbgFormulaYaw:F1}° boundary={rt.dbgBoundaryYaw:F1}° " +
                   $"sectorΔ={rt.dbgSectorDelta:F1}° absΔ={rt.dbgAbsSectorDelta:F1}° " +
                   $"prevAbsΔ={rt.dbgPrevAbsSectorDelta:F1}° inside={rt.dbgSectorInside}\n" +
                   $"  halfAngle(outer)={rt.dbgHalfAngle:F0}° innerHold={rt.dbgInnerHoldHalfAngle:F0}° " +
                   $"corrWeight={rt.dbgCorrectionWeight:F2} " +
                   $"tgtSpd={rt.dbgTargetReturnSpeed:F1} curSpd={rt.currentYawReturnSpeed:F1}\n" +
                   $"  e2p={rt.dbgEnemyToPlayerYaw:F1}° e2cam={rt.dbgEnemyToCameraYaw:F1}° " +
                   $"bndDir={rt.dbgBoundaryDirYaw:F1}° bndRadius={rt.dbgBoundaryRadius:F2} " +
                   $"bndCamPos={FormatVector(rt.dbgBoundaryCamPos)}";
        }

        private static string FormatTargetGroupTargetsFor(LockCameraRigRuntime rt)
        {
            if (rt?.targetGroup?.m_Targets == null) return "null";
            var targets = rt.targetGroup.m_Targets;
            if (targets.Length == 0) return "[]";
            string result = "[";
            for (int i = 0; i < targets.Length; i++)
            {
                if (i > 0) result += ", ";
                result += $"{ActorCameraControl.FormatObjectName(targets[i].target)} w={targets[i].weight:F2} r={targets[i].radius:F2}";
            }
            return result + "]";
        }

        private string FormatDistances(Transform enemyTarget, Camera mainCam)
        {
            float playerEnemy = enemyTarget != null
                ? Vector3.Distance(_o.transform.position, enemyTarget.position)
                : -1f;
            float mainPlayer = mainCam != null
                ? Vector3.Distance(mainCam.transform.position, _o.transform.position)
                : -1f;
            float mainEnemy = mainCam != null && enemyTarget != null
                ? Vector3.Distance(mainCam.transform.position, enemyTarget.position)
                : -1f;
            return $"playerEnemy={FormatFloat(playerEnemy)} mainPlayer={FormatFloat(mainPlayer)} mainEnemy={FormatFloat(mainEnemy)}";
        }

        private static string FormatVector(Vector3 value) => $"({value.x:F2},{value.y:F2},{value.z:F2})";
        private static string FormatYaw(Quaternion rotation) => rotation.eulerAngles.y.ToString("F1");
        private static string FormatFloat(float value) => value >= 0f ? value.ToString("F2") : "n/a";
    }
}
