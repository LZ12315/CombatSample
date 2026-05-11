using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[DefaultExecutionOrder(-20)]
public partial class ActorCameraControl : MonoBehaviour
{
    // ==================================================================
    // Serialized Fields
    // ==================================================================

    /// <summary>
    /// Semantic: focused/presentation actor. Does NOT imply Actor owns Camera.
    /// CameraControl is an external observer that focuses on an Actor.
    /// </summary>
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

    // ==================================================================
    // Runtime State
    // ==================================================================

    [SerializeField] private Enums.PlayerCameraState currentState;
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> _stateToCameraMap;

    private LockCameraRigRuntime _softRuntime;
    private LockCameraRigRuntime _hardRuntime;

    /// <summary>
    /// Backward-compat passthrough: points to active lock runtime's target group.
    /// Updated by SyncCompatReferences() on state change.
    /// </summary>
    private CinemachineTargetGroup _targetGroup;

    // Internal helpers (non-Mono, created in Awake)
    private CameraRigRouter _rigRouter;
    private CombatLockComposer _composer;
    private CameraDiagnostics _diagnostics;

    // ==================================================================
    // Public API
    // ==================================================================

    public CinemachineTargetGroup TargetGroup => _targetGroup;

    public Enums.PlayerCameraState CinemachineState
    {
        get => currentState;
        set => SetCameraState(value);
    }

    /// <summary>
    /// Legacy compatibility: SetCameraState calls ActorCombater.TryEnterSoftLock/ClearLock.
    /// Future: input/gameplay layer should request lock; camera should only read Combat state.
    /// </summary>
    public void SetCameraState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();

        _diagnostics.LogCameraEvent($"SetCameraState request={newState} force={forceUpdate} beforeLock={FormatLockMode()} beforeTarget={FormatObjectName(GetCombatTarget())}");

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

        _diagnostics.LogCameraEvent($"SetCameraState after authority lock={FormatLockMode()} target={FormatObjectName(GetCombatTarget())} resolved={ResolvePresentationState()}");
        ApplyPresentationState(ResolvePresentationState(), forceUpdate);
    }

    public void UpdatePivotBehavior()
    {
        RefreshCameraRuntime();
    }

    // ==================================================================
    // Unity Lifecycle
    // ==================================================================

    private void Awake()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();

        _rigRouter = new CameraRigRouter(this);
        _composer = new CombatLockComposer(this);
        _diagnostics = new CameraDiagnostics(this);

        _rigRouter.InitializeCameraMap();

        _softRuntime = new LockCameraRigRuntime();
        _hardRuntime = new LockCameraRigRuntime();

        // Keep runtime objects in world space so blended-out cameras can truly freeze.
        _softRuntime.CreateAnchor(transform);
        _softRuntime.CreateTargetGroup(transform);
        _hardRuntime.CreateAnchor(transform);
        _hardRuntime.CreateTargetGroup(transform);

        _rigRouter.ConfigureLockCameraTransitions();
    }

    private void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(_diagnostics.OnCinemachineBrainUpdated);
        CinemachineCore.CameraUpdatedEvent.AddListener(_diagnostics.OnCinemachineBrainUpdated);
    }

    private void Start()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();

        transform.localRotation = Quaternion.identity;
        UpdateFollowAnchor(_softRuntime);
        UpdateFollowAnchor(_hardRuntime);
        ApplyPresentationState(ResolvePresentationState(), true);

        _rigRouter.ValidateImpulseListenersOnVirtualCameras();
    }

    private void LateUpdate()
    {
        RefreshCameraRuntime();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(_diagnostics.OnCinemachineBrainUpdated);
    }

    private void OnDestroy()
    {
        _softRuntime?.DestroyRuntime();
        _hardRuntime?.DestroyRuntime();
        _softRuntime = null;
        _hardRuntime = null;
        _targetGroup = null;
    }

    // ==================================================================
    // High-Level Camera Flow
    // ==================================================================

    private void RefreshCameraRuntime()
    {
        Enums.PlayerCameraState presentationState = ResolvePresentationState();
        Transform enemyTarget = GetCombatTarget();

        bool stateChanged = currentState != presentationState;
        if (stateChanged)
        {
            _diagnostics.StartCameraDebugWindow($"RefreshCameraRuntime resolved change {currentState}->{presentationState}");
            _diagnostics.LogCameraSnapshot("LateUpdate.BeforeStateApply", enemyTarget);
            ApplyPresentationState(presentationState, forceUpdate: true);
        }

        _diagnostics.LogCameraSnapshot("LateUpdate.BeforeRuntimeRefresh", enemyTarget);

        CinemachineBrain brain = _rigRouter.ResolveBrain();
        bool hasEnemy = enemyTarget != null;

        // --- Active lock camera (always update) ---
        if (currentState != Enums.PlayerCameraState.Free && hasEnemy)
        {
            LockCameraRigRuntime activeRt = GetActiveRuntime();
            if (activeRt != null)
            {
                _composer.UpdateCombatFollowAnchor(activeRt, enemyTarget);
                _composer.RefreshTargetGroup(activeRt, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(activeRt);
                activeRt.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: SoftLock runtime ---
        if (currentState != Enums.PlayerCameraState.SoftLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(softLockCamera);
            if (!isLive)
            {
                _composer.UpdateCombatFollowAnchor(_softRuntime, enemyTarget);
                _composer.RefreshTargetGroup(_softRuntime, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(_softRuntime);
                _softRuntime.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: HardLock runtime ---
        if (currentState != Enums.PlayerCameraState.HardLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(hardLockCamera);
            if (!isLive)
            {
                _composer.UpdateCombatFollowAnchor(_hardRuntime, enemyTarget);
                _composer.RefreshTargetGroup(_hardRuntime, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(_hardRuntime);
                _hardRuntime.targetGroup?.DoUpdate();
            }
        }

        _rigRouter.ApplyCameraPriorities(currentState);
        _diagnostics.LogCameraSnapshot("LateUpdate.AfterRuntimeRefresh", enemyTarget);
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

    /// <summary>
    /// Presentation state: only switches priority.
    /// Runtime data is maintained separately in RefreshCameraRuntime.
    /// </summary>
    private void ApplyPresentationState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        if (_stateToCameraMap == null)
            _rigRouter.InitializeCameraMap();

        Transform enemyTarget = GetCombatTarget();
        if (newState != Enums.PlayerCameraState.Free && enemyTarget == null)
            newState = Enums.PlayerCameraState.Free;

        if (currentState == newState && !forceUpdate) return;

        Enums.PlayerCameraState previousState = currentState;
        _diagnostics.StartCameraDebugWindow($"ApplyPresentationState {previousState}->{newState} force={forceUpdate}");
        _diagnostics.LogCameraSnapshot("Apply.Before", enemyTarget);

        currentState = newState;
        SyncCompatReferences();

        bool enteringLock = previousState != newState
                         && newState != Enums.PlayerCameraState.Free
                         && enemyTarget != null;
        if (enteringLock)
        {
            // Snap incoming lock camera to target (avoid long blend arc).
            LockCameraRigRuntime incoming = GetActiveRuntime();
            if (incoming != null)
            {
                _composer.UpdateCombatFollowAnchor(incoming, enemyTarget, instant: true);
                _composer.RefreshTargetGroup(incoming, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(incoming);
                incoming.targetGroup?.DoUpdate();
                _rigRouter.PrepareIncomingLockCamera(newState);
            }
        }

        bool exitingToFree = previousState != Enums.PlayerCameraState.Free
                          && newState == Enums.PlayerCameraState.Free;
        if (exitingToFree)
        {
            _rigRouter.PrepareIncomingFreeLookCamera();
        }

        _rigRouter.ApplyCameraPriorities(currentState);
        _diagnostics.LogCameraSnapshot("Apply.AfterPriorities", enemyTarget);
    }

    // ==================================================================
    // Internal Helpers (orchestration glue)
    // ==================================================================

    private LockCameraRigRuntime GetActiveRuntime()
    {
        return currentState switch
        {
            Enums.PlayerCameraState.SoftLock => _softRuntime,
            Enums.PlayerCameraState.HardLock => _hardRuntime,
            _ => null,
        };
    }

    private void SyncCompatReferences()
    {
        LockCameraRigRuntime active = GetActiveRuntime();
        _targetGroup = active?.targetGroup;
    }

    private void UpdateFollowAnchor(LockCameraRigRuntime rt)
    {
        if (rt == null || rt.anchor == null) return;
        rt.anchor.position = transform.position;
        rt.anchor.rotation = Quaternion.identity;
    }

    private Transform GetCombatTarget()
    {
        return actor != null && actor.combater != null
            ? actor.combater.CombatTarget?.transform
            : null;
    }

    private string FormatLockMode()
    {
        return actor != null && actor.combater != null
            ? actor.combater.LockMode.ToString()
            : "null";
    }

    private static string FormatObjectName(Object obj) => obj != null ? obj.name : "null";

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

}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}
