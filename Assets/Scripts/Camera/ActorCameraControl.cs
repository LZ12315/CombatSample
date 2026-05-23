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
    [Header("Actor And Camera Rigs")]
    [Tooltip("相机关注的 Actor，通常为玩家角色。软锁定/硬锁定的目标来源。")]
    public Actor actor;

    [Tooltip("自由移动时使用的相机（FreeLook）。")]
    public CinemachineFreeLook normalFreeLookCamera;
    [Tooltip("软锁定时使用的虚拟相机。")]
    public CinemachineVirtualCamera softLockCamera;
    [Tooltip("硬锁定时使用的虚拟相机。")]
    public CinemachineVirtualCamera hardLockCamera;

    [HideInInspector]
    public float pivotRotationSpeed = 10f;
    [HideInInspector]
    public float freeModeResetSpeed = 5f;

    [HideInInspector]
    public float fixedOffsetAngle = 15f;
    [HideInInspector]
    public float offsetSmoothTime = 0.2f;
    [HideInInspector]
    public float shoulderSwitchThreshold = 0.1f;

#pragma warning disable CS0414 // Deprecated serialized fields kept for Unity inspector compatibility.
    [SerializeField, HideInInspector] private float combatCenterBias = 0.4f;
    [SerializeField, HideInInspector] private float minFollowDistance = 4f;
    [SerializeField, HideInInspector] private float maxFollowDistance = 12f;
    [SerializeField, HideInInspector] private float followDistanceScale = 1.0f;
    [SerializeField, HideInInspector] private float sideOffsetScale = 0.6f;
#pragma warning restore CS0414

    [Header("Lock Camera - Stability")]
    [Tooltip("锁定相机焦点的高度偏移。值越低镜头越平；值越高越俯视。")]
    [SerializeField] private float heightOffset = 0.6f;
    [Tooltip("锁定锚点位置的平滑时间。值越大跟随越迟缓；值越小越灵敏。")]
    [SerializeField] private float positionSmoothTime = 0.35f;
    [Tooltip("锁定锚点水平旋转（Yaw）的平滑时间。值越大旋转越慢；值越小旋转越快。")]
    [SerializeField] private float rotationSmoothTime = 0.3f;

    [Header("Lock Camera - Information Framing")]
    [Tooltip("锁定镜头的主要画面大小。值越大，玩家和敌人在屏幕中越大；过高可能裁切武器或特效。")]
    [Range(0.45f, 0.9f)]
    [SerializeField] private float lockFramingSize = 0.68f;
    [Tooltip("目标组基础边距。值越小空白越少；过低可能让武器、特效或脚步贴边。")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float lockTargetPadding = 1.2f;

    [Header("Lock Camera - Soft Lock Lateral Inertia")]
    [Tooltip("软锁定左右惰性死区（世界空间米）。玩家在死区内横向移动时相机保持侧向位置不追；超过死区后相机开始匀速追赶。")]
    [Range(0.2f, 2.5f)]
    [SerializeField] private float softLockSideDeadZone = 0.8f;
    [Tooltip("软锁定侧向追赶速度（米/秒）。玩家超出左右死区后相机以此速度向公式位置追赶。")]
    [Range(1f, 10f)]
    [SerializeField] private float softLockSideCatchUpSpeed = 3.5f;

    [SerializeField, HideInInspector] private float sideSmoothTime = 0.8f;

    [SerializeField, HideInInspector] private float lockCenterBias = 0.45f;
    [SerializeField, HideInInspector] private float lockSideBias = 0.22f;
    [SerializeField, HideInInspector] private float lockFov = 45f;
    [SerializeField, HideInInspector] private float lockBaseFollowDistance = 3.0f;
    [SerializeField, HideInInspector] private float lockDistancePerCombatMeter = 0.45f;
    [SerializeField, HideInInspector] private float lockMaxFollowDistance = 11f;
    [SerializeField, HideInInspector] private float lockPlayerWeight = 1.25f;
    [SerializeField, HideInInspector] private float lockEnemyWeight = 0.95f;

#pragma warning disable CS0414 // Legacy near/far camera fields kept for Unity serialized compatibility.
    [SerializeField, HideInInspector] private float compositionNearDist = 3f;
    [SerializeField, HideInInspector] private float compositionFarDist = 18f;
    [SerializeField, HideInInspector] private float followDistNear = 4.0f;
    [SerializeField, HideInInspector] private float followDistFar = 11f;
    [SerializeField, HideInInspector] private float fovNear = 42f;
    [SerializeField, HideInInspector] private float fovFar = 50f;
    [SerializeField, HideInInspector] private float centerBiasNear = 0.42f;
    [SerializeField, HideInInspector] private float centerBiasFar = 0.50f;
    [SerializeField, HideInInspector] private float sideBiasNear = 0.15f;
    [SerializeField, HideInInspector] private float sideBiasFar = 0.30f;
    [SerializeField, HideInInspector] private float playerWeightNear = 1.3f;
    [SerializeField, HideInInspector] private float playerWeightFar = 1.2f;
    [SerializeField, HideInInspector] private float enemyWeightNear = 1.0f;
    [SerializeField, HideInInspector] private float enemyWeightFar = 0.9f;
    [SerializeField, HideInInspector] private float framingSizeNear = 0.72f;
    [SerializeField, HideInInspector] private float framingSizeFar = 0.62f;
    [SerializeField, HideInInspector] private float playerRadiusNear = 1.0f;
    [SerializeField, HideInInspector] private float playerRadiusFar = 1.8f;
    [SerializeField, HideInInspector] private float enemyRadiusNear = 1.0f;
    [SerializeField, HideInInspector] private float enemyRadiusFar = 2.0f;
#pragma warning restore CS0414

    [Header("Camera Diagnostics")]
    [Tooltip("记录相机状态切换及切换前后的快照日志。")]
    [SerializeField] private bool debugCameraTransitions = false;
    [Tooltip("状态切换后额外输出 debug 快照的帧数。")]
    [SerializeField] private int debugFramesAfterTransition = 8;
    [Tooltip("每帧 LateUpdate 输出相机快照。日志量极大，仅在调试时开启。")]
    [SerializeField] private bool debugCameraEveryLateUpdate = false;
    [Tooltip("每次相机更新后输出 Cinemachine Brain 日志。日志量极大，仅在调试时开启。")]
    [SerializeField] private bool debugBrainAfterUpdate = false;

    // ==================================================================
    // Runtime State
    // ==================================================================

    [SerializeField, HideInInspector] private Enums.PlayerCameraState currentState;
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> _stateToCameraMap;

    private enum LockCameraUpdateMode
    {
        Formula,
        LiveSoftLock
    }

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
                LockCameraUpdateMode mode = currentState == Enums.PlayerCameraState.SoftLock
                    ? LockCameraUpdateMode.LiveSoftLock
                    : LockCameraUpdateMode.Formula;
                _composer.UpdateCombatFollowAnchor(activeRt, enemyTarget, mode: mode);
                _composer.RefreshTargetGroup(activeRt, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(activeRt);
                activeRt.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: SoftLock runtime (no inertia) ---
        if (currentState != Enums.PlayerCameraState.SoftLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(softLockCamera);
            if (!isLive)
            {
                _composer.UpdateCombatFollowAnchor(_softRuntime, enemyTarget, mode: LockCameraUpdateMode.Formula);
                _composer.RefreshTargetGroup(_softRuntime, enemyTarget, currentState);
                _rigRouter.ApplyCameraBindingForRuntime(_softRuntime);
                _softRuntime.targetGroup?.DoUpdate();
            }
        }

        // --- Background pre-warm: HardLock runtime (no inertia) ---
        if (currentState != Enums.PlayerCameraState.HardLock && hasEnemy)
        {
            bool isLive = brain != null && brain.IsLive(hardLockCamera);
            if (!isLive)
            {
                _composer.UpdateCombatFollowAnchor(_hardRuntime, enemyTarget, mode: LockCameraUpdateMode.Formula);
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
            LockCameraRigRuntime incoming = GetActiveRuntime();
            if (incoming != null)
            {
                LockCameraUpdateMode mode = newState == Enums.PlayerCameraState.SoftLock
                    ? LockCameraUpdateMode.LiveSoftLock
                    : LockCameraUpdateMode.Formula;
                _composer.UpdateCombatFollowAnchor(incoming, enemyTarget, instant: true, mode: mode);
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
