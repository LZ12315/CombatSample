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
    [SerializeField, Tooltip("Deprecated serialized value. Runtime input is published on Actor.PlayerInput.")]
    private float _bufferValidTime = 0.2f;

    public Vector2 MoveInput => PlayerInputController.Instance != null &&
                                PlayerInputController.Instance.IsControlledActor(actor)
        ? PlayerInputController.Instance.RawMove
        : Vector2.zero;

    public Vector2 LookInput => PlayerInputController.Instance != null &&
                                PlayerInputController.Instance.IsControlledActor(actor)
        ? PlayerInputController.Instance.RawLook
        : Vector2.zero;

    public LocomotionIntent LatestLocomotionIntent => actor != null && actor.actorLocomotion != null
        ? actor.actorLocomotion.EffectiveIntent
        : LocomotionIntent.Idle;

    public IReadOnlyList<PlayerInputEvent> InputBuffer => actor != null && actor.PlayerInput != null
        ? actor.PlayerInput.Events
        : Array.Empty<PlayerInputEvent>();

    private void Awake()
    {
        actor = actor != null ? actor : GetComponent<Actor>();
        _bufferValidTime = Mathf.Max(0f, _bufferValidTime);
    }

    public void ClearBuffer()
    {
        actor?.PlayerInput?.ClearEvents();
    }
}
