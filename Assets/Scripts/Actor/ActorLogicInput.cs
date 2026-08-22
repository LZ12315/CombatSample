using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Actor))]
public class ActorLogicInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Actor actor;
    [SerializeField] private ActorCameraControl cameraControl;

    [Header("Input Buffer")]
    [SerializeField, Tooltip("Deprecated. Player input history now lives on PlayerInputController.")]
    private float _bufferValidTime = 0.2f;

    public Vector2 MoveInput => PlayerInputController.Instance != null &&
                                PlayerInputController.Instance.IsControlledActor(actor)
        ? PlayerInputController.Instance.RawMove
        : Vector2.zero;

    public Vector2 LookInput => PlayerInputController.Instance != null &&
                                PlayerInputController.Instance.IsControlledActor(actor)
        ? PlayerInputController.Instance.RawLook
        : Vector2.zero;

    public LocomotionIntent LatestLocomotionIntent => actor != null && actor.actorMotor != null
        ? actor.actorMotor.LocomotionIntent
        : LocomotionIntent.Idle;

    public IReadOnlyList<PlayerInputController.BufferedInput> InputBuffer =>
        PlayerInputController.Instance != null && PlayerInputController.Instance.IsControlledActor(actor)
            ? PlayerInputController.Instance.InputHistory
            : Array.Empty<PlayerInputController.BufferedInput>();

    private void Awake()
    {
        actor = actor != null ? actor : GetComponent<Actor>();
        _bufferValidTime = Mathf.Max(0f, _bufferValidTime);
    }

    public void InputMove(Vector2 moveInput)
    {
    }

    public void InputLook(Vector2 lookInput)
    {
    }

    public void GetInputData(InputData inputData)
    {
    }

    public void ClearBuffer()
    {
        if (PlayerInputController.Instance != null && PlayerInputController.Instance.IsControlledActor(actor))
            PlayerInputController.Instance.ClearInputHistory();
    }

    public void RegisterForInputEvent(object registrant, Action<InputData> callback)
    {
    }

    public void UnregisterFromInputEvent(object registrant)
    {
    }
}
