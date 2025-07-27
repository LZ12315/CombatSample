using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    PlayerInput playerInput;
    PlayerInputControl actions;

    public Actor controlledActor;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        actions = new PlayerInputControl();
        playerInput.actions = actions.asset;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        actions.Player.SetCallbacks(this);
    }

    private void OnEnable()
    {
        actions.Enable();
    }

    private void OnDisable()
    {
        actions.Disable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        throw new System.NotImplementedException();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        throw new System.NotImplementedException();
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        throw new System.NotImplementedException();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        throw new System.NotImplementedException();
    }
}
