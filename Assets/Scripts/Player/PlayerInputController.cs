using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    PlayerInput playerInput;
    PlayerInputControl actions;

    public Actor controlledActor;
    public CinemachineFreeLook vcam;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        actions = new PlayerInputControl();
        playerInput.actions = actions.asset;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        actions.Player.SetCallbacks(this);
    }

    private void Start()
    {
        SetControlledActor(FindFirstObjectByType<Actor>());
    }

    private void OnEnable()
    {
        actions.Enable();
    }

    private void OnDisable()
    {
        actions.Disable();
    }

    public void SetControlledActor(Actor controlledActor)
    {
        this.controlledActor = controlledActor;
        vcam.Follow = controlledActor.transform;
        vcam.LookAt = controlledActor.transform;
    }

    void Update()
    {
        if(controlledActor == null) return;

        //controlledActor.logicInput.InputMove(ConvertFromCameraLocalToWorld(rawMove));
        controlledActor.logicInput.InputMove(rawMove);
    }

    Vector2 rawMove = Vector2.zero;
    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
    }

    private Vector3 ConvertFromCameraLocalToWorld(Vector2 move)
    {
        var cam = Camera.main;
        var movement = move.magnitude;
        if (cam != null && movement > 0.1f)
        {
            var direction = cam.transform.TransformDirection(new Vector3(rawMove.x, 0, rawMove.y));
            direction.y = 0;
            direction.Normalize();
            return direction;
        }

        return Vector3.zero;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
    }

    public void OnFire(InputAction.CallbackContext context)
    {
    }

    public void OnJump(InputAction.CallbackContext context)
    {
    }

}
