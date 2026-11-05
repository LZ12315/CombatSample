using UnityEngine;
using Cinemachine;

/// <summary>
/// Observes combat state, schedules camera rigs and handles Cinemachine transitions.
/// Soft/hard rigs own composition and their independent runtime targets.
/// Serialized tuning stays here to preserve existing scene and prefab references.
/// </summary>
[DefaultExecutionOrder(-20)]
public class ActorCameraControl : MonoBehaviour
{
    [Header("Actor And Camera Rigs")]
    [Tooltip("相机关注的 Actor，通常为玩家角色。软锁定/硬锁定的目标来源。")]
    public Actor actor;

    [Tooltip("自由移动时使用的相机（FreeLook）。")]
    public CinemachineFreeLook normalFreeLookCamera;
    [Tooltip("软锁定时使用的虚拟相机。")]
    public CinemachineVirtualCamera softLockCamera;
    [Tooltip("硬锁定时使用的虚拟相机。")]
    public CinemachineVirtualCamera hardLockCamera;

    [Header("Soft Lock - Basic")]
    [SerializeField] private float softLockPlayerWeight = 1.15f;
    [SerializeField] private float softLockEnemyWeight = 1.05f;
    [SerializeField] private float softLockPlayerRadius = 1.1f;
    [SerializeField] private float softLockEnemyRadius = 1.0f;

    [Header("Hard Lock - Anchor Stability")]
    [Tooltip("硬锁定相机焦点的高度偏移。值越低镜头越平；值越高越俯视。")]
    [SerializeField] private float heightOffset = 0.6f;
    [Tooltip("硬锁定锚点位置的平滑时间。值越大跟随越迟缓；值越小越灵敏。")]
    [SerializeField] private float positionSmoothTime = 0.35f;
    [Tooltip("硬锁定锚点水平旋转（Yaw）的平滑时间。值越大旋转越慢；值越小旋转越快。")]
    [SerializeField] private float rotationSmoothTime = 0.3f;

    [Header("Hard Lock - Information Framing")]
    [Tooltip("硬锁定镜头的主要画面大小。值越大，玩家和敌人在屏幕中越大；过高可能裁切武器或特效。")]
    [Range(0.45f, 0.9f)]
    [SerializeField] private float lockFramingSize = 0.68f;
    [Tooltip("目标组基础边距。值越小空白越少；过低可能让武器、特效或脚步贴边。")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float lockTargetPadding = 1.2f;

    [SerializeField, HideInInspector] private float lockCenterBias = 0.45f;
    [SerializeField, HideInInspector] private float lockFov = 45f;
    [SerializeField, HideInInspector] private float lockBaseFollowDistance = 3.0f;
    [SerializeField, HideInInspector] private float lockDistancePerCombatMeter = 0.45f;
    [SerializeField, HideInInspector] private float lockMaxFollowDistance = 11f;
    [SerializeField, HideInInspector] private float lockPlayerWeight = 1.25f;
    [SerializeField, HideInInspector] private float lockEnemyWeight = 0.95f;

    [Header("Camera Diagnostics")]
    [Tooltip("记录相机状态切换及切换前后的快照日志。")]
    [SerializeField] private bool debugCameraTransitions = false;
    [Tooltip("状态切换后额外输出 debug 快照的帧数。")]
    [SerializeField] private int debugFramesAfterTransition = 8;
    [Tooltip("每帧 LateUpdate 输出相机快照。日志量极大，仅在调试时开启。")]
    [SerializeField] private bool debugCameraEveryLateUpdate = false;

    [SerializeField, HideInInspector] private Enums.PlayerCameraState currentState;
    private SoftLockCameraRig _softRig;
    private HardLockCameraRig _hardRig;
    private CameraDiagnostics _diagnostics;

    // Presentation is read-only. Request lock changes through ActorCombater.
    public Enums.PlayerCameraState CinemachineState => currentState;
    public CinemachineTargetGroup TargetGroup => currentState switch
    {
        Enums.PlayerCameraState.SoftLock => _softRig?.TargetGroup,
        Enums.PlayerCameraState.HardLock => _hardRig?.TargetGroup,
        _ => null
    };

    private void Awake()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        // Lock entry is explicitly initialized, not inherited from the outgoing camera.
        if (softLockCamera != null) softLockCamera.m_Transitions.m_InheritPosition = false;
        if (hardLockCamera != null) hardLockCamera.m_Transitions.m_InheritPosition = false;
        ActionScreenShakeListener.Install(normalFreeLookCamera);
        ActionScreenShakeListener.Install(softLockCamera);
        ActionScreenShakeListener.Install(hardLockCamera);
        _diagnostics = new CameraDiagnostics(this);
        ConfigureDiagnostics();
        _softRig = new SoftLockCameraRig(softLockCamera);
        _hardRig = new HardLockCameraRig(hardLockCamera, transform.position);
    }

    private void Start()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        transform.localRotation = Quaternion.identity;
        _hardRig.InitializeAnchor(transform.position);
        ApplyPresentationState(ResolvePresentationState(), forceUpdate: true);
        CameraDiagnostics.ValidateImpulseListeners(normalFreeLookCamera, softLockCamera, hardLockCamera);
    }

    private void LateUpdate()
    {
        ConfigureDiagnostics();
        RefreshCameraRuntime();
    }

    private void OnDestroy()
    {
        _softRig?.DestroyRuntime();
        _hardRig?.DestroyRuntime();
        _softRig = null;
        _hardRig = null;
    }

    private void RefreshCameraRuntime()
    {
        var presentationState = ResolvePresentationState();
        Transform enemyTarget = GetCombatTarget();
        if (currentState != presentationState)
            ApplyPresentationState(presentationState);

        var brain = ResolveBrain();
        if (enemyTarget != null)
        {
            // Keep the original order: active rig first, then idle soft/hard rigs.
            RefreshActiveRig(enemyTarget);

            // IsLive includes blend participants. Do not refresh an outgoing rig's
            // world-space targets until it has left the blend; preserve its snapshot.
            if (currentState != Enums.PlayerCameraState.SoftLock
                && (brain == null || !brain.IsLive(softLockCamera)))
                RefreshSoftRig(enemyTarget);

            if (currentState != Enums.PlayerCameraState.HardLock
                && (brain == null || !brain.IsLive(hardLockCamera)))
                RefreshHardRig(enemyTarget);
        }

        ApplyPriorities();
        LogSnapshot("LateUpdate.AfterRuntimeRefresh");
    }

    private Enums.PlayerCameraState ResolvePresentationState()
    {
        if (GetCombatTarget() == null)
            return Enums.PlayerCameraState.Free;

        return actor.combater.LockMode switch
        {
            Enums.LockMode.SoftLock => Enums.PlayerCameraState.SoftLock,
            Enums.LockMode.HardLock => Enums.PlayerCameraState.HardLock,
            _ => Enums.PlayerCameraState.Free
        };
    }

    private void ApplyPresentationState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        Transform enemyTarget = GetCombatTarget();
        if (newState != Enums.PlayerCameraState.Free && enemyTarget == null)
            newState = Enums.PlayerCameraState.Free;
        if (currentState == newState && !forceUpdate) return;

        var previousState = currentState;
        _diagnostics.BeginTransition($"{previousState}->{newState} force={forceUpdate}");
        LogSnapshot("Apply.Before");
        currentState = newState;

        if (previousState != newState && newState != Enums.PlayerCameraState.Free && enemyTarget != null)
        {
            RefreshActiveRig(enemyTarget, instant: true);
            PrepareIncomingLockCamera(newState);
        }
        if (previousState != Enums.PlayerCameraState.Free && newState == Enums.PlayerCameraState.Free)
            PrepareIncomingFreeLookCamera();

        ApplyPriorities();
        LogSnapshot("Apply.AfterPriorities");
    }

    // Cinemachine selection and transitions. Lock rigs bind their own targets.
    private void ApplyPriorities()
    {
        if (normalFreeLookCamera != null)
            normalFreeLookCamera.Priority = currentState == Enums.PlayerCameraState.Free ? 20 : 10;
        if (softLockCamera != null)
            softLockCamera.Priority = currentState == Enums.PlayerCameraState.SoftLock ? 20 : 10;
        if (hardLockCamera != null)
            hardLockCamera.Priority = currentState == Enums.PlayerCameraState.HardLock ? 20 : 10;
    }

    private static CinemachineBrain ResolveBrain()
    {
        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
    }

    private void PrepareIncomingLockCamera(Enums.PlayerCameraState state)
    {
        var incoming = state == Enums.PlayerCameraState.SoftLock ? softLockCamera
            : state == Enums.PlayerCameraState.HardLock ? hardLockCamera : null;
        if (incoming == null) return;
        incoming.PreviousStateIsValid = false;
        incoming.InternalUpdateCameraState(Vector3.up, -1f);
    }

    private void PrepareIncomingFreeLookCamera()
    {
        if (normalFreeLookCamera == null) return;
        var brain = ResolveBrain();
        if (brain != null && (brain.IsLive(normalFreeLookCamera) || brain.IsLiveInBlend(normalFreeLookCamera)))
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            normalFreeLookCamera.ForceCameraPosition(mainCamera.transform.position, mainCamera.transform.rotation);
    }

    private void RefreshActiveRig(Transform enemyTarget, bool instant = false)
    {
        switch (currentState)
        {
            case Enums.PlayerCameraState.SoftLock:
                RefreshSoftRig(enemyTarget);
                break;
            case Enums.PlayerCameraState.HardLock:
                RefreshHardRig(enemyTarget, instant);
                break;
        }
    }

    private void RefreshSoftRig(Transform enemyTarget)
    {
        _softRig.Refresh(actor, enemyTarget, new SoftLockCameraRig.Settings
        {
            PlayerWeight = softLockPlayerWeight,
            EnemyWeight = softLockEnemyWeight,
            PlayerRadius = softLockPlayerRadius,
            EnemyRadius = softLockEnemyRadius
        });
    }

    private void RefreshHardRig(Transform enemyTarget, bool instant = false)
    {
        _hardRig.Refresh(transform, enemyTarget, new HardLockCameraRig.Settings
        {
            HeightOffset = heightOffset,
            PositionSmoothTime = positionSmoothTime,
            RotationSmoothTime = rotationSmoothTime,
            FramingSize = lockFramingSize,
            TargetPadding = lockTargetPadding,
            CenterBias = lockCenterBias,
            Fov = lockFov,
            BaseFollowDistance = lockBaseFollowDistance,
            DistancePerCombatMeter = lockDistancePerCombatMeter,
            MaxFollowDistance = lockMaxFollowDistance,
            PlayerWeight = lockPlayerWeight,
            EnemyWeight = lockEnemyWeight
        }, instant);
    }

    private Transform GetCombatTarget() => actor != null && actor.combater != null
        ? actor.combater.CombatTarget?.transform : null;

    private void ConfigureDiagnostics() => _diagnostics.Configure(
        debugCameraTransitions, debugFramesAfterTransition, debugCameraEveryLateUpdate);

    private void LogSnapshot(string phase) => _diagnostics.LogSnapshot(
        phase, currentState, actor, normalFreeLookCamera, _softRig, _hardRig);

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
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}

/// <summary>
/// Shared allocation details kept in this file; each lock rig owns and destroys
/// its own objects. This helper has no state or scheduling responsibility.
/// </summary>
internal static class LockCameraRuntimeObjects
{
    public static Transform CreateTransform(string name, Vector3 position)
    {
        var target = new GameObject(name).transform;
        // Deliberately unparented: a blending-out rig must not inherit actor motion.
        target.SetPositionAndRotation(position, Quaternion.identity);
        return target;
    }

    public static CinemachineTargetGroup CreateTargetGroup(
        string name, CinemachineTargetGroup.PositionMode positionMode)
    {
        var target = CreateTransform(name, Vector3.zero);
        var group = target.gameObject.AddComponent<CinemachineTargetGroup>();
        group.m_PositionMode = positionMode;
        group.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
        group.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
        return group;
    }

    public static void DestroyTransform(ref Transform target)
    {
        if (target != null)
            Destroy(target.gameObject);
        target = null;
    }

    public static void Destroy(GameObject target)
    {
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
