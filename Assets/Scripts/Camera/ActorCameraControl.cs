using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[DefaultExecutionOrder(-20)]
public class ActorCameraControl : MonoBehaviour, ICameraFrameProvider
{
    public Actor actor;

    private CinemachineTargetGroup _targetGroup;
    public CinemachineTargetGroup TargetGroup => _targetGroup;

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
    [SerializeField] private float combatCenterBias = 0.4f;
    [SerializeField] private float minFollowDistance = 4f;
    [SerializeField] private float maxFollowDistance = 12f;
    [SerializeField] private float followDistanceScale = 1.0f;
    [SerializeField] private float sideOffsetScale = 0.6f;
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private float positionSmoothTime = 0.3f;
    [SerializeField] private float rotationSmoothTime = 0.2f;
    [SerializeField] private float sideSmoothTime = 0.5f;

    [SerializeField] private Enums.PlayerCameraState currentState;
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> _stateToCameraMap;
    private Transform _followAnchor;
    private Transform _trackedLockTarget;
    private Enums.PlayerCameraState _trackedState;
    private bool _targetGroupDirty = true;
    private float _smoothedSide = 0f;
    private float _sideSmoothVelocity = 0f;
    private Vector3 _anchorPositionVelocity = Vector3.zero;
    private float _anchorYawVelocity = 0f;
    private float _currentAnchorYaw = 0f;
    private float _currentCombatFollowDistance = 8f;

    public Enums.PlayerCameraState CinemachineState
    {
        get => currentState;
        set => SetCameraState(value);
    }

    private void Awake()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        InitializeCameraMap();
        CreateRuntimeFollowAnchor();
        CreateRuntimeTargetGroup();
    }

    private void OnEnable()
    {
        RegisterCameraFrameProvider();
    }

    private void Start()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        RegisterCameraFrameProvider();

        transform.localRotation = Quaternion.identity;
        UpdateFollowAnchor();
        SetCameraState(currentState, true);

        ValidateImpulseListenersOnVirtualCameras();
    }

    private void LateUpdate()
    {
        RefreshCameraRuntime();
    }

    private void OnDisable()
    {
        UnregisterCameraFrameProvider();
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(ref _targetGroup);
        DestroyRuntimeObject(ref _followAnchor);
    }

    private void DestroyRuntimeObject<T>(ref T runtimeObject) where T : Component
    {
        if (runtimeObject == null) return;

        GameObject gameObjectToDestroy = runtimeObject.gameObject;
        runtimeObject = null;

        if (Application.isPlaying)
            Destroy(gameObjectToDestroy);
        else
            DestroyImmediate(gameObjectToDestroy);
    }

    static void ValidateImpulseListenerOn(CinemachineVirtualCameraBase vcam)
    {
        if (vcam == null) return;
        if (vcam.GetComponent<CinemachineImpulseListener>() != null) return;
        Debug.LogWarning($"Virtual camera '{vcam.name}' has no CinemachineImpulseListener. Impact screen shake will not affect it.", vcam);
    }

    void ValidateImpulseListenersOnVirtualCameras()
    {
        if (normalFreeLookCamera != null) ValidateImpulseListenerOn(normalFreeLookCamera);
        if (softLockCamera != null) ValidateImpulseListenerOn(softLockCamera);
        if (hardLockCamera != null) ValidateImpulseListenerOn(hardLockCamera);
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

    public void UpdatePivotBehavior()
    {
        RefreshCameraRuntime();
    }

    public void SetCameraState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        CreateRuntimeFollowAnchor();
        CreateRuntimeTargetGroup();

        if (_stateToCameraMap == null)
            InitializeCameraMap();

        Transform enemyTarget = GetCombatTarget();
        if (newState != Enums.PlayerCameraState.Free && enemyTarget == null)
            newState = Enums.PlayerCameraState.Free;

        if (currentState == newState && !forceUpdate) return;

        currentState = newState;
        _targetGroupDirty = true;

        bool inCombat = currentState != Enums.PlayerCameraState.Free && enemyTarget != null;
        if (inCombat)
            UpdateCombatFollowAnchor(enemyTarget);
        else
            UpdateFollowAnchor();

        RefreshTargetGroup(enemyTarget);
        ApplyCameraBindings();
        ApplyCameraPriorities();
        _targetGroup?.DoUpdate();
    }

    private void CreateRuntimeTargetGroup()
    {
        if (_targetGroup != null) return;

        GameObject groupObj = new GameObject("Runtime_TargetGroup");
        _targetGroup = groupObj.AddComponent<CinemachineTargetGroup>();
        _targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
        _targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
        _targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;

        _targetGroupDirty = true;
        RefreshTargetGroup(null);
    }

    private void CreateRuntimeFollowAnchor()
    {
        if (_followAnchor != null) return;

        GameObject anchorObj = new GameObject("Runtime_CameraFollowAnchor");
        _followAnchor = anchorObj.transform;
        UpdateFollowAnchor();
    }

    private void UpdateFollowAnchor()
    {
        if (_followAnchor == null) return;

        _followAnchor.position = transform.position;
        _followAnchor.rotation = Quaternion.identity;
    }

    private void UpdateCombatFollowAnchor(Transform enemyTarget)
    {
        if (_followAnchor == null || enemyTarget == null || actor == null) return;

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

        _smoothedSide = Mathf.SmoothDamp(_smoothedSide, rawSide, ref _sideSmoothVelocity, sideSmoothTime);
        float sideSign = _smoothedSide >= 0f ? 1f : -1f;

        float forwardBias = Mathf.Clamp(combatDist * combatCenterBias, 0f, combatDist * 0.5f);
        Vector3 combatCenter = playerPosXZ + combatDir * forwardBias;
        combatCenter.y = (playerPos.y + enemyPos.y) * 0.5f + heightOffset;

        float sideAmount = Mathf.Min(combatDist * sideOffsetScale, combatDist * 0.5f) * sideSign;
        Vector3 desiredAnchorPos = combatCenter + right * sideAmount;
        desiredAnchorPos.y = combatCenter.y;

        _followAnchor.position = Vector3.SmoothDamp(
            _followAnchor.position, desiredAnchorPos,
            ref _anchorPositionVelocity, positionSmoothTime);

        _currentCombatFollowDistance = Mathf.Clamp(
            combatDist * followDistanceScale, minFollowDistance, maxFollowDistance);

        Vector3 desiredCamPos = combatCenter
            - combatDir * (_currentCombatFollowDistance * 0.6f)
            + right * (sideSign * _currentCombatFollowDistance * 0.5f);
        desiredCamPos.y = combatCenter.y + heightOffset * 0.3f;

        Vector3 toCamera = desiredCamPos - _followAnchor.position;
        if (toCamera.sqrMagnitude > 0.001f)
        {
            Vector3 anchorForward = -toCamera.normalized;
            float targetYaw = Mathf.Atan2(anchorForward.x, anchorForward.z) * Mathf.Rad2Deg;
            _currentAnchorYaw = Mathf.SmoothDampAngle(_currentAnchorYaw, targetYaw, ref _anchorYawVelocity, rotationSmoothTime);
            _followAnchor.rotation = Quaternion.Euler(0f, _currentAnchorYaw, 0f);
        }

        ConfigureTransposerForCombat(softLockCamera);
        ConfigureTransposerForCombat(hardLockCamera);
    }

    private void RefreshCameraRuntime()
    {
        CreateRuntimeFollowAnchor();
        CreateRuntimeTargetGroup();

        Transform enemyTarget = GetCombatTarget();
        if (currentState != Enums.PlayerCameraState.Free && enemyTarget == null)
        {
            SetCameraState(Enums.PlayerCameraState.Free);
            return;
        }

        bool inCombat = currentState != Enums.PlayerCameraState.Free && enemyTarget != null;
        if (inCombat)
            UpdateCombatFollowAnchor(enemyTarget);
        else
            UpdateFollowAnchor();

        RefreshTargetGroup(enemyTarget);
        ApplyCameraBindings();
        ApplyCameraPriorities();
        _targetGroup.DoUpdate();
    }

    private void RefreshTargetGroup(Transform enemyTarget)
    {
        if (_targetGroup == null) return;

        bool lockMode = currentState != Enums.PlayerCameraState.Free && enemyTarget != null;
        Transform lockTarget = lockMode ? enemyTarget : null;
        int targetCount = lockMode ? 2 : 1;
        CinemachineTargetGroup.Target[] targets = _targetGroup.m_Targets;

        bool needsRebuild = _targetGroupDirty
            || _trackedState != currentState
            || _trackedLockTarget != lockTarget
            || targets == null
            || targets.Length != targetCount
            || targets[0].target != transform;

        if (!needsRebuild) return;

        if (lockMode)
        {
            _targetGroup.m_Targets = new[]
            {
                new CinemachineTargetGroup.Target { target = transform, weight = 1f, radius = 2f },
                new CinemachineTargetGroup.Target { target = lockTarget, weight = 1f, radius = 2f }
            };
        }
        else
        {
            _targetGroup.m_Targets = new[]
            {
                new CinemachineTargetGroup.Target { target = transform, weight = 1f, radius = 2f }
            };
        }

        _trackedState = currentState;
        _trackedLockTarget = lockTarget;
        _targetGroupDirty = false;
    }

    private void ApplyCameraBindings()
    {
        Transform lookAtTarget = currentState == Enums.PlayerCameraState.Free || _targetGroup == null
            ? null
            : _targetGroup.transform;

        ApplyLockCameraBinding(softLockCamera, lookAtTarget);
        ApplyLockCameraBinding(hardLockCamera, lookAtTarget);
    }

    private void ApplyLockCameraBinding(CinemachineVirtualCamera vcam, Transform lookAtTarget)
    {
        if (vcam == null) return;
        vcam.LookAt = lookAtTarget;
        vcam.Follow = _followAnchor;
    }

    private void ConfigureTransposerForCombat(CinemachineVirtualCamera vcam)
    {
        if (vcam == null) return;
        CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null) return;
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(0f, 0f, -_currentCombatFollowDistance);
    }

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

    private Transform GetCombatTarget()
    {
        return actor != null && actor.combater != null
            ? actor.combater.CombatTarget?.transform
            : null;
    }

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
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}
