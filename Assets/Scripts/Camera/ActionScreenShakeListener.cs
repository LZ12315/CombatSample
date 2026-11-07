using Cinemachine;
using UnityEngine;

/// <summary>
/// Dedicated native receiver for Action intervals. Installed only on the character camera's owned rigs.
/// Hit/parry listeners keep their existing channels, direction interpretation and reaction settings.
/// FreeLook is received once after its internal orbit blend, not once per child orbit.
/// </summary>
[AddComponentMenu("")]
internal sealed class ActionScreenShakeListener : CinemachineImpulseListener
{
    internal static void Install(CinemachineVirtualCameraBase camera)
    {
        if (camera == null || camera.GetComponent<ActionScreenShakeListener>() != null) return;
        foreach (var existing in camera.GetComponents<CinemachineImpulseListener>())
            if ((existing.m_ChannelMask & ActionScreenShakeRequest.Channel) != 0)
            {
                Debug.LogError($"[Action Shake] '{camera.name}' already receives reserved channel 2. "
                    + "Remove that channel from the shared listener before installing the dedicated Action receiver.", camera);
                return;
            }
        camera.gameObject.AddComponent<ActionScreenShakeListener>();
    }

    protected override void Awake() { Configure(); base.Awake(); }
    protected override void OnEnable() { Configure(); base.OnEnable(); }
    private void Configure()
    {
        m_ApplyAfter = CinemachineCore.Stage.Finalize;
        m_ChannelMask = ActionScreenShakeRequest.Channel;
        m_Gain = 1f;
        m_Use2DDistance = false;
        m_UseCameraSpace = true;
        m_ReactionSettings.m_SecondaryNoise = null;
        m_ReactionSettings.m_AmplitudeGain = 0f;
    }
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (vcam != VirtualCamera) return;
        Configure();
        base.PostPipelineStageCallback(vcam, stage, ref state, deltaTime);
    }
}
