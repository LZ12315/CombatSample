using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[DefaultExecutionOrder(-20)]
public class ActorCameraControl : MonoBehaviour, ICameraFrameProvider
{
    public Actor actor;

    [Header("Cameras")]
    public CinemachineFreeLook normalFreeLookCamera;
    public CinemachineVirtualCamera softLockCamera;
    public CinemachineVirtualCamera hardLockCamera;

    [Header("Pivot")]
    [Tooltip("Deprecated in Camera phase 1. Kept for serialized compatibility.")]
    public float pivotRotationSpeed = 10f;
    [Tooltip("Deprecated in Camera phase 1. Kept for serialized compatibility.")]
    public float freeModeResetSpeed = 5f;

    [Header("Lock-on Offset")]
    [Tooltip("Deprecated in Camera phase 1. Kept for serialized compatibility.")]
    public float fixedOffsetAngle = 15f;
    [Tooltip("Deprecated in Camera phase 1. Kept for serialized compatibility.")]
    public float offsetSmoothTime = 0.2f;
    [Tooltip("Deprecated in Camera phase 1. Kept for serialized compatibility.")]
    public float shoulderSwitchThreshold = 0.1f;

    [Header("Combat Follow Anchor")]
#pragma warning disable CS0414 // Deprecated serialized fields kept for Unity inspector compatibility.
    [Tooltip("Deprecated. Kept for serialized compatibility. Use Distance-Driven Composition parameters instead.")]
    [SerializeField] private float combatCenterBias = 0.4f;
    [Tooltip("Deprecated. Kept for serialized compatibility. Use followDistNear / followDistFar instead.")]
    [SerializeField] private float minFollowDistance = 4f;
    [Tooltip("Deprecated. Kept for serialized compatibility. Use followDistNear / followDistFar instead.")]
    [SerializeField] private float maxFollowDistance = 12f;
    [Tooltip("Deprecated. Kept for serialized compatibility. Use followDistNear / followDistFar instead.")]
    [SerializeField] private float followDistanceScale = 1.0f;
    [Tooltip("Deprecated. Kept for serialized compatibility. Use sideBiasNear / sideBiasFar instead.")]
    [SerializeField] private float sideOffsetScale = 0.6f;
#pragma warning restore CS0414
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private float positionSmoothTime = 0.3f;
    [SerializeField] private float rotationSmoothTime = 0.2f;
    [SerializeField] private float sideSmoothTime = 0.5f;

    [Header("Distance-Driven Composition")]
    [SerializeField] private float compositionNearDist = 5f;
    [SerializeField] private float compositionFarDist = 22f;
    [SerializeField] private float followDistNear = 8f;
    [SerializeField] private float followDistFar = 22f;
    [SerializeField] private float fovNear = 50f;
    [SerializeField] private float fovFar = 65f;
    [SerializeField] private float centerBiasNear = 0.38f;
    [SerializeField] private float centerBiasFar = 0.55f;
    [SerializeField] private float sideBiasNear = 0.28f;
    [SerializeField] private float sideBiasFar = 0.42f;
    [SerializeField] private float playerWeightNear = 1.2f;
    [SerializeField] private float playerWeightFar = 1.3f;
    [SerializeField] private float enemyWeightNear = 1.0f;
    [SerializeField] private float enemyWeightFar = 0.9f;
    [SerializeField] private float playerRadiusNear = 2f;
    [SerializeField] private float playerRadiusFar = 3.5f;
    [SerializeField] private float enemyRadiusNear = 2f;
    [SerializeField] private float enemyRadiusFar = 4f;
    [SerializeField] private float framingSizeNear = 0.82f;
    [SerializeField] private float framingSizeFar = 0.60f;

    [Header("Camera Diagnostics")]
    [SerializeField] private bool debugCameraTransitions = false;
    [SerializeField] private int debugFramesAfterTransition = 8;
    [SerializeField] private bool debugCameraEveryLateUpdate = false;
    [SerializeField] private bool debugBrainAfterUpdate = false;

    // ------------------------------------------------------------------
    // Per-camera runtime data
    // ------------------------------------------------------------------
    private sealed class LockCameraRuntime
    {
        public Transform anchor;
        public CinemachineTargetGroup targetGroup;

        public bool targetGroupDirty = true;
        public Transform trackedLockTarget;
        public Enums.PlayerCameraState trackedState;

        // smoothed values
        public float smoothedSide;
        public float sideSmoothVelocity;
        public Vector3 anchorPositionVelocity = Vector3.zero;
        public float anchorYawVelocity;
        public float currentAnchorYaw;
        public float currentFollowDistance = 8f;

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
                if (Application.isPlaying) Destroy(targetGroup.gameObject);
                else DestroyImmediate(targetGroup.gameObject);
                targetGroup = null;
            }
            if (anchor != null)
            {
                if (Application.isPlaying) Destroy(anchor.gameObject);
                else DestroyImmediate(anchor.gameObject);
                anchor = null;
            }
            trackedLockTarget = null;
        }
    }

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------
    [SerializeField] private Enums.PlayerCameraState currentState;
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> _stateToCameraMap;

    private LockCameraRuntime _softRuntime;
    private LockCameraRuntime _hardRuntime;

    // Backward-compat passthrough: points to active lock runtime's target group.
    // Updated by SyncCompatReferences() on state change.
    private CinemachineTargetGroup _targetGroup;

    public CinemachineTargetGroup TargetGroup => _targetGroup;

    // debug bookkeeping
    private int _debugWindowEndFrame = -1;
    private int _debugTransitionId;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------
    public Enums.PlayerCameraState CinemachineState
    {
        get => currentState;
        set => SetCameraState(value);
    }

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------
    private void Awake()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        InitializeCameraMap();

        _softRuntime = new LockCameraRuntime();
        _hardRuntime = new LockCameraRuntime();

        // Keep runtime objects in world space so blended-out cameras can truly freeze.
        _softRuntime.CreateAnchor(transform);
        _softRuntime.CreateTargetGroup(transform);
        _hardRuntime.CreateAnchor(transform);
        _hardRuntime.CreateTargetGroup(transform);

        ConfigureLockCameraTransitions();
    }

    private void OnEnable()
    {
        RegisterCameraFrameProvider();
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineBrainUpdated);
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineBrainUpdated);
    }

    private void Start()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        RegisterCameraFrameProvider();

        transform.localRotation = Quaternion.identity;
        UpdateFollowAnchor(_softRuntime);
        UpdateFollowAnchor(_hardRuntime);
        ApplyPresentationState(ResolvePresentationState(), true);

        ValidateImpulseListenersOnVirtualCameras();
    }

    private void LateUpdate()
    {
        RefreshCameraRuntime();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineBrainUpdated);
        UnregisterCameraFrameProvider();
    }

    private void OnDestroy()
    {
        _softRuntime?.DestroyRuntime();
        _hardRuntime?.DestroyRuntime();
        _softRuntime = null;
        _hardRuntime = null;
        _targetGroup = null;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private LockCameraRuntime GetActiveRuntime()
    {
        return currentState switch
        {
            Enums.PlayerCameraState.SoftLock => _softRuntime,
            Enums.PlayerCameraState.HardLock => _hardRuntime,
            _ => null,
        };
    }

    private CinemachineBrain ResolveBrain()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return null;
        return mainCam.GetComponent<CinemachineBrain>();
    }

    private void SyncCompatReferences()
    {
        LockCameraRuntime active = GetActiveRuntime();
        _targetGroup = active?.targetGroup;
    }

    private float ComputeCompositionT(float combatDist)
    {
        return Mathf.InverseLerp(compositionNearDist, compositionFarDist, combatDist);
    }

    // ------------------------------------------------------------------
    // Init
    // ------------------------------------------------------------------
    static void ValidateImpulseListenerOn(CinemachineVirtualCameraBase vcam)
    {
        if (vcam == null) return;
        if (vcam.GetComponent<CinemachineImpulseListener>() != null) return;
        Debug.LogWarning($"Virtual camera '{vcam.Name}' has no CinemachineImpulseListener. Impact screen shake will not affect it.", vcam);
    }

    void ValidateImpulseListenersOnVirtualCameras()
    {
        ValidateImpulseListenerOn(normalFreeLookCamera);
        ValidateImpulseListenerOn(softLockCamera);
        ValidateImpulseListenerOn(hardLockCamera);
    }

    void InitializeCameraMap()
    {
        _stateToCameraMap = new Dictionary<Enums.PlayerCameraState, ICinemachineCamera>
        {
            { Enums.PlayerCameraState.Free, normalFreeLookCamera },
            { Enums.PlayerCameraState.SoftLock, softLockCamera },
            { Enums.PlayerCameraState.HardLock, hardLockCamera }
        };
    }

    void ConfigureLockCameraTransitions()
    {
        ConfigureLockCameraTransition(softLockCamera);
        ConfigureLockCameraTransition(hardLockCamera);
    }

    static void ConfigureLockCameraTransition(CinemachineVirtualCamera vcam)
    {
        if (vcam == null) return;
        vcam.m_Transitions.m_InheritPosition = false;
    }

    // ------------------------------------------------------------------
    // State API
    // ------------------------------------------------------------------
    public void UpdatePivotBehavior()
    {
        RefreshCameraRuntime();
    }

    public void SetCameraState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();

        LogCameraEvent($"SetCameraState request={newState} force={forceUpdate} beforeLock={FormatLockMode()} beforeTarget={FormatObjectName(GetCombatTarget())}");

        if (actor?.combater != null)
        {
            switch (newState)
            {
                case Enums.PlayerCameraState.Free:
                    actor.combater.ClearLock();
                    break;
                case Enums.PlayerCameraState.SoftLock:
                    actor.combater.TryEnterSoftLock();
                    break;
                case Enums.PlayerCameraState.HardLock:
                    actor.combater.TryEnterHardLock();
                    break;
            }
        }

        LogCameraEvent($"SetCameraState after authority lock={FormatLockMode()} target={FormatObjectName(GetCombatTarget())} resolved={ResolvePresentationState()}");
        ApplyPresentationState(ResolvePresentationState(), forceUpdate);
    }

    private Enums.PlayerCameraState ResolvePresentationState()
    {
        if (actor == null || actor.combater == null)
            return Enums.PlayerCameraState.Free;

        if (actor.combater.CombatTarget == null)
            return Enums.PlayerCameraState.Free;

        return actor.combater.LockMode switch
        {
            Enums.LockMode.SoftLock => Enums.PlayerCameraState.SoftLock,
            Enums.LockMode.HardLock => Enums.PlayerCameraState.HardLock,
            _ => Enums.PlayerCameraState.Free,
        };
    }

    // ------------------------------------------------------------------
    // Presentation state: only switches priority.
    // Runtime data is maintained separately in RefreshCameraRuntime.
    // ------------------------------------------------------------------
    private void ApplyPresentationState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        if (_stateToCameraMap == null)
            InitializeCameraMap();

        Transform enemyTarget = GetCombatTarget();
        if (newState != Enums.PlayerCameraState.Free && enemyTarget == null)
            newState = Enums.PlayerCameraState.Free;

        if (currentState == newState && !forceUpdate) return;

        Enums.PlayerCameraState previousState = currentState;
        StartCameraDebugWindow($"ApplyPresentationState {previousState}->{newState} force={forceUpdate}");
        LogCameraSnapshot("Apply.Before", enemyTarget);

        currentState = newState;
        SyncCompatReferences();

        bool enteringLock = previousState != newState
                         && newState != Enums.PlayerCameraState.Free
                         && enemyTarget != null;
        if (enteringLock)
        {
            // Snap incoming lock camera to target (avoid long blend arc).
            LockCameraRuntime incoming = GetActiveRuntime();
            if (incoming != null)
            {
                UpdateCombatFollowAnchor(incoming, enemyTarget, instant: true);
                RefreshTargetGroup(incoming, enemyTarget);
                ApplyCameraBindingForRuntime(incoming);
                incoming.targetGroup?.DoUpdate();
                PrepareIncomingLockCamera(newState);
            }
        }

        bool exitingToFree = previousState != Enums.PlayerCameraState.Free
                          && newState == Enums.PlayerCameraState.Free;
        if (exitingToFree)
        {
            PrepareIncomingFreeLookCamera();
        }

        ApplyCameraPriorities();
        LogCameraSnapshot("Apply.AfterPriorities", enemyTarget);
    }

    // ------------------------------------------------------------------
    // Per-frame runtime maintenance
    // ------------------------------------------------------------------
    private void RefreshCameraRuntime()
    {
        Enums.PlayerCameraState presentationState = ResolvePresentationState();
        Transform enemyTarget = GetCombatTarget();

        bool stateChanged = currentState != presentationState;
        if (stateChanged)
        {
            StartCameraDebugWindow($"RefreshCameraRuntime resolved change {currentState}->{presentationState}");
            LogCameraSnapshot("LateUpdate.BeforeStateApply", enemyTarget);
            ApplyPresentationState(presentationState, forceUpdate: true);
            // After state change, run one more maintenance pass for the new state.
        }

        LogCameraSnapshot("LateUpdate.BeforeRuntimeRefresh", enemyTarget);

        CinemachineBrain brain = ResolveBrain();
        bool hasEnemy = enemyTarget != null;

        // --- Active lock camera (always update) ---
        if (currentState != Enums.PlayerCameraState.Free && hasEnemy)
        {
            LockCameraRuntime activeRt = GetActiveRuntime();
            if (activeRt != null)
            {
                UpdateCombatFollowAnchor(activeRt, enemyTarget);
                RefreshTargetGroup(activeRt, enemyTarget);
                ApplyCameraBindingForRuntime(activeRt);
                activeRt.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: SoftLock runtime ---
        if (currentState != Enums.PlayerCameraState.SoftLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(softLockCamera);
            if (!isLive)
            {
                UpdateCombatFollowAnchor(_softRuntime, enemyTarget);
                RefreshTargetGroup(_softRuntime, enemyTarget);
                ApplyCameraBindingForRuntime(_softRuntime);
                _softRuntime.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: HardLock runtime ---
        if (currentState != Enums.PlayerCameraState.HardLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(hardLockCamera);
            if (!isLive)
            {
                UpdateCombatFollowAnchor(_hardRuntime, enemyTarget);
                RefreshTargetGroup(_hardRuntime, enemyTarget);
                ApplyCameraBindingForRuntime(_hardRuntime);
                _hardRuntime.targetGroup?.DoUpdate();
            }
        }

        ApplyCameraPriorities();

        LogCameraSnapshot("LateUpdate.AfterRuntimeRefresh", enemyTarget);
    }

    // ------------------------------------------------------------------
    // Anchor helpers
    // ------------------------------------------------------------------
    private void UpdateFollowAnchor(LockCameraRuntime rt)
    {
        if (rt == null || rt.anchor == null) return;
        rt.anchor.position = transform.position;
        rt.anchor.rotation = Quaternion.identity;
    }

    private void UpdateCombatFollowAnchor(LockCameraRuntime rt, Transform enemyTarget, bool instant = false)
    {
        if (rt == null || rt.anchor == null || enemyTarget == null || actor == null) return;

        Vector3 playerPos = transform.position;
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
            rt.smoothedSide = Mathf.SmoothDamp(rt.smoothedSide, rawSide, ref rt.sideSmoothVelocity, sideSmoothTime);
        }

        float sideSign = rt.smoothedSide >= 0f ? 1f : -1f;

        float t = ComputeCompositionT(combatDist);
        float centerBias = Mathf.Lerp(centerBiasNear, centerBiasFar, t);
        float forwardBias = combatDist * centerBias;
        Vector3 combatCenter = playerPosXZ + combatDir * forwardBias;
        combatCenter.y = (playerPos.y + enemyPos.y) * 0.5f + heightOffset;

        float sideScale = Mathf.Lerp(sideBiasNear, sideBiasFar, t);
        float sideAmount = Mathf.Min(combatDist * sideScale, combatDist * 0.5f) * sideSign;
        Vector3 desiredAnchorPos = combatCenter + right * sideAmount;
        desiredAnchorPos.y = combatCenter.y;

        if (instant)
            rt.anchor.position = desiredAnchorPos;
        else
            rt.anchor.position = Vector3.SmoothDamp(
                rt.anchor.position, desiredAnchorPos,
                ref rt.anchorPositionVelocity, positionSmoothTime);

        rt.currentFollowDistance = Mathf.Lerp(followDistNear, followDistFar, t);

        Vector3 desiredCamPos = combatCenter
            - combatDir * (rt.currentFollowDistance * 0.6f)
            + right * (sideSign * rt.currentFollowDistance * 0.5f);
        desiredCamPos.y = combatCenter.y + heightOffset * 0.3f;

        Vector3 toCamera = desiredCamPos - rt.anchor.position;
        if (toCamera.sqrMagnitude > 0.001f)
        {
            Vector3 anchorForward = -toCamera.normalized;
            float targetYaw = Mathf.Atan2(anchorForward.x, anchorForward.z) * Mathf.Rad2Deg;
            rt.currentAnchorYaw = instant
                ? targetYaw
                : Mathf.SmoothDampAngle(rt.currentAnchorYaw, targetYaw, ref rt.anchorYawVelocity, rotationSmoothTime);
            rt.anchor.rotation = Quaternion.Euler(0f, rt.currentAnchorYaw, 0f);
        }

        ConfigureTransposerForCombat(rt);
        ConfigureGroupComposerForCombat(rt, combatDist);
        ApplyLockCameraFov(rt, combatDist);
    }

    // ------------------------------------------------------------------
    // TargetGroup
    // ------------------------------------------------------------------
    private void RefreshTargetGroup(LockCameraRuntime rt, Transform enemyTarget)
    {
        if (rt == null || rt.targetGroup == null) return;

        bool lockMode = enemyTarget != null;
        Transform lockTarget = lockMode ? enemyTarget : null;
        int targetCount = lockMode ? 2 : 1;
        CinemachineTargetGroup.Target[] targets = rt.targetGroup.m_Targets;

        bool needsRebuild = rt.targetGroupDirty
            || rt.trackedState != currentState
            || rt.trackedLockTarget != lockTarget
            || targets == null
            || targets.Length != targetCount
            || (targets.Length > 0 && targets[0].target != transform);

        if (needsRebuild)
        {
            LogCameraEvent($"RefreshTargetGroup rebuild lockMode={lockMode} targetCount={targetCount} lockTarget={FormatObjectName(lockTarget)}");

            if (lockMode)
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = transform, weight = playerWeightNear, radius = playerRadiusNear },
                    new CinemachineTargetGroup.Target { target = lockTarget, weight = enemyWeightNear, radius = enemyRadiusNear }
                };
            }
            else
            {
                rt.targetGroup.m_Targets = new[]
                {
                    new CinemachineTargetGroup.Target { target = transform, weight = playerWeightNear, radius = playerRadiusNear }
                };
            }

            rt.trackedState = currentState;
            rt.trackedLockTarget = lockTarget;
            rt.targetGroupDirty = false;
            targets = rt.targetGroup.m_Targets;
        }

        // Per-frame: update weights/radii to match combat distance.
        // Player weight is always >= enemy weight to keep the player as primary subject.
        if (lockMode && targets != null && targets.Length == 2)
        {
            float dist = Vector3.Distance(transform.position, lockTarget.position);
            float t = ComputeCompositionT(dist);

            targets[0].weight = Mathf.Lerp(playerWeightNear, playerWeightFar, t);
            targets[0].radius = Mathf.Lerp(playerRadiusNear, playerRadiusFar, t);
            targets[1].weight = Mathf.Lerp(enemyWeightNear, enemyWeightFar, t);
            targets[1].radius = Mathf.Lerp(enemyRadiusNear, enemyRadiusFar, t);
        }
        else if (!lockMode && targets != null && targets.Length >= 1)
        {
            targets[0].weight = playerWeightNear;
            targets[0].radius = playerRadiusNear;
        }
    }

    // ------------------------------------------------------------------
    // Camera binding
    // ------------------------------------------------------------------
    private void ApplyCameraBindingForRuntime(LockCameraRuntime rt)
    {
        if (rt == null) return;
        CinemachineVirtualCamera vcam = rt == _softRuntime ? softLockCamera : hardLockCamera;
        if (vcam == null) return;
        vcam.LookAt = rt.targetGroup != null ? rt.targetGroup.transform : null;
        vcam.Follow = rt.anchor;
    }

    private void ConfigureTransposerForCombat(LockCameraRuntime rt)
    {
        if (rt == null) return;
        CinemachineVirtualCamera vcam = rt == _softRuntime ? softLockCamera : hardLockCamera;
        if (vcam == null) return;
        CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null) return;
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(0f, 0f, -rt.currentFollowDistance);
    }

    private void ConfigureGroupComposerForCombat(LockCameraRuntime rt, float combatDist)
    {
        if (rt == null) return;
        CinemachineVirtualCamera vcam = rt == _softRuntime ? softLockCamera : hardLockCamera;
        if (vcam == null) return;

        float t = ComputeCompositionT(combatDist);
        float framingSize = Mathf.Lerp(framingSizeNear, framingSizeFar, t);
        float maxFov = Mathf.Max(fovFar, 68f);

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

    private void ApplyLockCameraFov(LockCameraRuntime rt, float combatDist)
    {
        if (rt == null) return;
        CinemachineVirtualCamera vcam = rt == _softRuntime ? softLockCamera : hardLockCamera;
        if (vcam == null) return;

        float t = ComputeCompositionT(combatDist);
        float targetFov = Mathf.Lerp(fovNear, fovFar, t);

        var lens = vcam.m_Lens;
        lens.FieldOfView = targetFov;
        vcam.m_Lens = lens;
    }

    // ------------------------------------------------------------------
    // Transition helpers
    // ------------------------------------------------------------------
    private void PrepareIncomingLockCamera(Enums.PlayerCameraState state)
    {
        CinemachineVirtualCamera incoming = state switch
        {
            Enums.PlayerCameraState.SoftLock => softLockCamera,
            Enums.PlayerCameraState.HardLock => hardLockCamera,
            _ => null
        };
        if (incoming == null) return;

        incoming.PreviousStateIsValid = false;
        incoming.InternalUpdateCameraState(Vector3.up, -1f);
    }

    private void PrepareIncomingFreeLookCamera()
    {
        if (normalFreeLookCamera == null) return;

        CinemachineBrain brain = ResolveBrain();
        if (brain != null
            && (brain.IsLive(normalFreeLookCamera) || brain.IsLiveInBlend(normalFreeLookCamera)))
        {
            LogCameraEvent("PrepareIncomingFreeLookCamera skipped because FreeLook is already live/in blend");
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        normalFreeLookCamera.ForceCameraPosition(mainCam.transform.position, mainCam.transform.rotation);
    }

    // ------------------------------------------------------------------
    // Priorities
    // ------------------------------------------------------------------
    private void ApplyCameraPriorities()
    {
        if (_stateToCameraMap == null)
            InitializeCameraMap();

        foreach (var kvp in _stateToCameraMap)
        {
            if (kvp.Value == null) continue;
            if (kvp.Value is CinemachineVirtualCameraBase camBase)
                camBase.Priority = kvp.Key == currentState ? 20 : 10;
        }
    }

    // ------------------------------------------------------------------
    // Combat target access
    // ------------------------------------------------------------------
    private Transform GetCombatTarget()
    {
        return actor != null && actor.combater != null
            ? actor.combater.CombatTarget?.transform
            : null;
    }

    // ------------------------------------------------------------------
    // ICameraFrameProvider
    // ------------------------------------------------------------------
    private ActorLogicInput ResolveLogicInput()
    {
        if (actor == null) return null;
        return actor.logicInput != null ? actor.logicInput : actor.GetComponent<ActorLogicInput>();
    }

    private void RegisterCameraFrameProvider()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        ResolveLogicInput()?.SetCameraFrameProvider(this);
    }

    private void UnregisterCameraFrameProvider()
    {
        ResolveLogicInput()?.ClearCameraFrameProvider(this);
    }

    public Vector3 ToWorldMoveDirection(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.01f) return Vector3.zero;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Vector3 fallback = new Vector3(moveInput.x, 0f, moveInput.y);
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
        }

        Transform mainCam = mainCamera.transform;
        Vector3 forward = mainCam.forward;
        Vector3 right = mainCam.right;
        forward.y = 0;
        right.y = 0;

        if (forward.sqrMagnitude > 0.0001f)
            forward.Normalize();
        if (right.sqrMagnitude > 0.0001f)
            right.Normalize();

        return (forward * moveInput.y) + (right * moveInput.x);
    }

    public Vector3 CalculateWorldDirection(Vector2 rawMove)
    {
        return ToWorldMoveDirection(rawMove);
    }

    // ------------------------------------------------------------------
    // Debug (default-off)
    // ------------------------------------------------------------------
    private void OnCinemachineBrainUpdated(CinemachineBrain brain)
    {
        if (!debugBrainAfterUpdate) return;
        LogCameraSnapshot("Brain.AfterUpdate", GetCombatTarget(), brain);
    }

    private void StartCameraDebugWindow(string reason)
    {
        if (!debugCameraTransitions) return;
        _debugTransitionId++;
        _debugWindowEndFrame = Mathf.Max(
            _debugWindowEndFrame,
            Time.frameCount + Mathf.Max(1, debugFramesAfterTransition));
        Debug.Log($"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] BEGIN {reason}", this);
    }

    private bool ShouldLogCameraDebug()
    {
        return debugCameraTransitions
            && (debugCameraEveryLateUpdate || Time.frameCount <= _debugWindowEndFrame);
    }

    private void LogCameraEvent(string message)
    {
        if (!ShouldLogCameraDebug()) return;
        Debug.Log($"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] {message}", this);
    }

    private void LogCameraSnapshot(string phase, Transform enemyTarget, CinemachineBrain brainOverride = null)
    {
        if (!ShouldLogCameraDebug()) return;

        Camera mainCam = Camera.main;
        CinemachineBrain brain = brainOverride ?? (mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null);

        string mainInfo = mainCam != null
            ? $"{FormatVector(mainCam.transform.position)} yaw={FormatYaw(mainCam.transform.rotation)}"
            : "null";

        string softAnchorInfo = _softRuntime?.anchor != null
            ? $"{FormatVector(_softRuntime.anchor.position)} yaw={FormatYaw(_softRuntime.anchor.rotation)}"
            : "null";
        string hardAnchorInfo = _hardRuntime?.anchor != null
            ? $"{FormatVector(_hardRuntime.anchor.position)} yaw={FormatYaw(_hardRuntime.anchor.rotation)}"
            : "null";

        string enemyInfo = enemyTarget != null
            ? $"{enemyTarget.name}@{FormatVector(enemyTarget.position)}"
            : "null";

        Debug.Log(
            $"[CameraDebug #{_debugTransitionId} f={Time.frameCount}] {phase}\n" +
            $"  state current={currentState} resolved={ResolvePresentationState()} lock={FormatLockMode()} combatTarget={FormatObjectName(GetCombatTarget())}\n" +
            $"  main={mainInfo} brain={FormatBrain(brain)}\n" +
            $"  player={FormatVector(transform.position)} enemy={enemyInfo} distances={FormatDistances(enemyTarget, mainCam)}\n" +
            $"  softAnchor={softAnchorInfo} softFollowDist={_softRuntime?.currentFollowDistance ?? 0f:F2} softSmoothedSide={_softRuntime?.smoothedSide ?? 0f:F2} softTG={FormatTargetGroupFor(_softRuntime)}\n" +
            $"  hardAnchor={hardAnchorInfo} hardFollowDist={_hardRuntime?.currentFollowDistance ?? 0f:F2} hardSmoothedSide={_hardRuntime?.smoothedSide ?? 0f:F2} hardTG={FormatTargetGroupFor(_hardRuntime)}\n" +
            $"  free={FormatCamera(normalFreeLookCamera, brain)}\n" +
            $"  soft={FormatCamera(softLockCamera, brain)}\n" +
            $"  hard={FormatCamera(hardLockCamera, brain)}",
            this);
    }

    private string FormatBrain(CinemachineBrain brain)
    {
        if (brain == null) return "null";
        string activeName = brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "null";
        string blend = brain.ActiveBlend != null ? brain.ActiveBlend.Description : "none";
        return $"{brain.name} active={activeName} blending={brain.IsBlending} blend={blend}";
    }

    private string FormatCamera(ICinemachineCamera camera, CinemachineBrain brain)
    {
        if (camera == null) return "null";
        string liveInfo = brain != null
            ? $" live={brain.IsLive(camera)} liveBlend={brain.IsLiveInBlend(camera)}"
            : string.Empty;
        return $"{camera.Name} P={camera.Priority}{liveInfo} follow={FormatObjectName(camera.Follow)} lookAt={FormatObjectName(camera.LookAt)} raw={FormatVector(camera.State.RawPosition)} final={FormatVector(camera.State.FinalPosition)}";
    }

    private string FormatTargetGroupFor(LockCameraRuntime rt)
    {
        if (rt?.targetGroup == null) return "null";
        return $"pos={FormatVector(rt.targetGroup.transform.position)} targets={FormatTargetGroupTargetsFor(rt)}";
    }

    private string FormatTargetGroupTargetsFor(LockCameraRuntime rt)
    {
        if (rt?.targetGroup?.m_Targets == null) return "null";
        var targets = rt.targetGroup.m_Targets;
        if (targets.Length == 0) return "[]";
        string result = "[";
        for (int i = 0; i < targets.Length; i++)
        {
            if (i > 0) result += ", ";
            result += $"{FormatObjectName(targets[i].target)} w={targets[i].weight:F2} r={targets[i].radius:F2}";
        }
        return result + "]";
    }

    private string FormatDistances(Transform enemyTarget, Camera mainCam)
    {
        float playerEnemy = enemyTarget != null
            ? Vector3.Distance(transform.position, enemyTarget.position)
            : -1f;
        float mainPlayer = mainCam != null
            ? Vector3.Distance(mainCam.transform.position, transform.position)
            : -1f;
        float mainEnemy = mainCam != null && enemyTarget != null
            ? Vector3.Distance(mainCam.transform.position, enemyTarget.position)
            : -1f;
        return $"playerEnemy={FormatFloat(playerEnemy)} mainPlayer={FormatFloat(mainPlayer)} mainEnemy={FormatFloat(mainEnemy)}";
    }

    private string FormatLockMode()
    {
        return actor != null && actor.combater != null
            ? actor.combater.LockMode.ToString()
            : "null";
    }

    private static string FormatObjectName(Object obj) => obj != null ? obj.name : "null";
    private static string FormatVector(Vector3 value) => $"({value.x:F2},{value.y:F2},{value.z:F2})";
    private static string FormatYaw(Quaternion rotation) => rotation.eulerAngles.y.ToString("F1");
    private static string FormatFloat(float value) => value >= 0f ? value.ToString("F2") : "n/a";
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}
