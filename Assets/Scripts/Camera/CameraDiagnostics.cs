using Cinemachine;
using UnityEngine;

/// <summary>Optional snapshots read live state; no diagnostic copies are stored in the lock rigs.</summary>
internal sealed class CameraDiagnostics
{
    private readonly Object _context;
    private bool _enabled;
    private bool _everyFrame;
    private int _framesAfterTransition;
    private int _windowEndFrame = -1;
    private int _transitionId;

    public CameraDiagnostics(Object context) { _context = context; }

    public void Configure(bool enabled, int framesAfterTransition, bool everyFrame)
    {
        _enabled = enabled;
        _framesAfterTransition = framesAfterTransition;
        _everyFrame = everyFrame;
    }

    public void BeginTransition(string reason)
    {
        if (!_enabled) return;
        _transitionId++;
        _windowEndFrame = Mathf.Max(_windowEndFrame,
            Time.frameCount + Mathf.Max(1, _framesAfterTransition));
        Debug.Log($"[CameraDebug #{_transitionId} f={Time.frameCount}] BEGIN {reason}", _context);
    }

    public void LogSnapshot(
        string phase, Enums.PlayerCameraState state, Actor actor,
        CinemachineFreeLook freeLook, SoftLockCameraRig softRig, HardLockCameraRig hardRig)
    {
        if (!_enabled || (!_everyFrame && Time.frameCount > _windowEndFrame)) return;

        Camera main = Camera.main;
        var brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        var combat = actor != null ? actor.combater : null;
        var enemy = combat != null ? combat.CombatTarget : null;
        string mainInfo = main != null
            ? $"pos={Vector(main.transform.position)} euler={Vector(main.transform.eulerAngles)} fov={main.fieldOfView:F2}"
            : "null";
        string brainInfo = brain != null
            ? $"active={Name(brain.ActiveVirtualCamera?.VirtualCameraGameObject)} blend={brain.ActiveBlend?.Description ?? "none"}"
            : "null";

        Debug.Log(
            $"[CameraDebug #{_transitionId} f={Time.frameCount}] {phase}\n" +
            $"  state={state} lock={(combat != null ? combat.LockMode.ToString() : "null")} target={Name(enemy)}\n" +
            $"  main={mainInfo} brain={brainInfo}\n" +
            $"  free={FormatCamera(freeLook, brain)}\n" +
            $"  soft={FormatCamera(softRig?.Camera, brain)} group={FormatGroup(softRig?.TargetGroup)}\n" +
            $"  hard={FormatCamera(hardRig?.Camera, brain)} followDistance={hardRig?.FollowDistance ?? 0f:F2} group={FormatGroup(hardRig?.TargetGroup)}",
            _context);
    }

    public static void ValidateImpulseListeners(
        CinemachineFreeLook freeLook, CinemachineVirtualCamera softLock, CinemachineVirtualCamera hardLock)
    {
        ValidateImpulseListener(freeLook);
        ValidateImpulseListener(softLock);
        ValidateImpulseListener(hardLock);
    }

    private static void ValidateImpulseListener(CinemachineVirtualCameraBase camera)
    {
        if (camera != null && camera.GetComponent<CinemachineImpulseListener>() == null)
            Debug.LogWarning($"Virtual camera '{camera.Name}' has no CinemachineImpulseListener. Impact screen shake will not affect it.", camera);
    }

    private static string FormatCamera(CinemachineVirtualCameraBase camera, CinemachineBrain brain)
    {
        if (camera == null) return "null";
        string live = brain != null ? brain.IsLive(camera).ToString() : "unknown";
        var state = camera.State;
        return $"{camera.Name} priority={camera.Priority} live={live} " +
            $"follow={FormatTarget(camera.Follow)} lookAt={FormatTarget(camera.LookAt)} " +
            $"raw={Vector(state.RawPosition)} final={Vector(state.FinalPosition)} " +
            $"euler={Vector(state.FinalOrientation.eulerAngles)} fov={state.Lens.FieldOfView:F2}";
    }

    private static string FormatGroup(CinemachineTargetGroup group)
    {
        if (group == null) return "null";
        string members = "";
        var targets = group.m_Targets;
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (i > 0) members += ", ";
                members += $"{FormatTarget(targets[i].target)} w={targets[i].weight:F2} r={targets[i].radius:F2}";
            }
        }
        return $"{Vector(group.transform.position)} [{members}]";
    }

    private static string FormatTarget(Transform target) => target != null ? $"{target.name}@{Vector(target.position)}" : "null";
    private static string Name(Object value) => value != null ? value.name : "null";
    private static string Vector(Vector3 value) => $"({value.x:F2},{value.y:F2},{value.z:F2})";
}
