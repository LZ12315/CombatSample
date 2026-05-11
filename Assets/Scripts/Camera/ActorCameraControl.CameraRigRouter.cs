using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public partial class ActorCameraControl
{
    // ==================================================================
    // Nested: CameraRigRouter
    // ==================================================================
    // Owns the camera map, priority switching, brain resolving,
    // incoming camera preparation, and camera binding.
    // ==================================================================

    private class CameraRigRouter
    {
        private readonly ActorCameraControl _o;

        public CameraRigRouter(ActorCameraControl owner) { _o = owner; }

        // -- Camera map -------------------------------------------------

        public void InitializeCameraMap()
        {
            _o._stateToCameraMap = new Dictionary<Enums.PlayerCameraState, ICinemachineCamera>
            {
                { Enums.PlayerCameraState.Free, _o.normalFreeLookCamera },
                { Enums.PlayerCameraState.SoftLock, _o.softLockCamera },
                { Enums.PlayerCameraState.HardLock, _o.hardLockCamera }
            };
        }

        // -- Priorities -------------------------------------------------

        public void ApplyCameraPriorities(Enums.PlayerCameraState currentState)
        {
            if (_o._stateToCameraMap == null)
                InitializeCameraMap();

            foreach (var kvp in _o._stateToCameraMap)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value is CinemachineVirtualCameraBase camBase)
                    camBase.Priority = kvp.Key == currentState ? 20 : 10;
            }
        }

        // -- Brain ------------------------------------------------------

        public CinemachineBrain ResolveBrain()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return null;
            return mainCam.GetComponent<CinemachineBrain>();
        }

        // -- Impulse validation -----------------------------------------

        public void ValidateImpulseListenersOnVirtualCameras()
        {
            ValidateImpulseListenerOn(_o.normalFreeLookCamera);
            ValidateImpulseListenerOn(_o.softLockCamera);
            ValidateImpulseListenerOn(_o.hardLockCamera);
        }

        private static void ValidateImpulseListenerOn(CinemachineVirtualCameraBase vcam)
        {
            if (vcam == null) return;
            if (vcam.GetComponent<CinemachineImpulseListener>() != null) return;
            Debug.LogWarning($"Virtual camera '{vcam.Name}' has no CinemachineImpulseListener. Impact screen shake will not affect it.", vcam);
        }

        // -- Lock camera transitions ------------------------------------

        public void ConfigureLockCameraTransitions()
        {
            ConfigureLockCameraTransition(_o.softLockCamera);
            ConfigureLockCameraTransition(_o.hardLockCamera);
        }

        private static void ConfigureLockCameraTransition(CinemachineVirtualCamera vcam)
        {
            if (vcam == null) return;
            vcam.m_Transitions.m_InheritPosition = false;
        }

        // -- Incoming camera preparation --------------------------------

        public void PrepareIncomingLockCamera(Enums.PlayerCameraState state)
        {
            CinemachineVirtualCamera incoming = state switch
            {
                Enums.PlayerCameraState.SoftLock => _o.softLockCamera,
                Enums.PlayerCameraState.HardLock => _o.hardLockCamera,
                _ => null
            };
            if (incoming == null) return;

            incoming.PreviousStateIsValid = false;
            incoming.InternalUpdateCameraState(Vector3.up, -1f);
        }

        public void PrepareIncomingFreeLookCamera()
        {
            if (_o.normalFreeLookCamera == null) return;

            CinemachineBrain brain = ResolveBrain();
            if (brain != null
                && (brain.IsLive(_o.normalFreeLookCamera) || brain.IsLiveInBlend(_o.normalFreeLookCamera)))
            {
                _o._diagnostics.LogCameraEvent("PrepareIncomingFreeLookCamera skipped because FreeLook is already live/in blend");
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam == null) return;
            _o.normalFreeLookCamera.ForceCameraPosition(mainCam.transform.position, mainCam.transform.rotation);
        }

        // -- Camera binding ---------------------------------------------

        public void ApplyCameraBindingForRuntime(LockCameraRigRuntime rt)
        {
            if (rt == null) return;
            CinemachineVirtualCamera vcam = rt == _o._softRuntime ? _o.softLockCamera : _o.hardLockCamera;
            if (vcam == null) return;
            vcam.LookAt = rt.targetGroup != null ? rt.targetGroup.transform : null;
            vcam.Follow = rt.anchor;
        }
    }
}
