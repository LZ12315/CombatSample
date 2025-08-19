using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    PlayerInput playerInput;
    PlayerInputControl actions;

    public Actor controlledActor;
    public CameraController cameraControl;

    public int ShortPress_Frame = 40;
    public int Hold_Frame = 120;

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
        cameraControl = Camera.main.GetComponent<CameraController>();
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
    }

    void Update()
    {
        if(controlledActor == null) return;

        Vector3 moveDir = cameraControl.RotationVertical * new Vector3(rawMove.x, 0, rawMove.y);
        controlledActor.logicInput.InputMove(moveDir, moveDistance);
    }

    Vector2 rawMove = Vector2.zero;
    float moveDistance = 0f;
    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        moveDistance = rawMove.magnitude;
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

    public void OnJump(InputAction.CallbackContext context)
    {

    }

    double LightPressStartTime = 0;
    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                LightPressStartTime = Time.time;
                break;

            case InputActionPhase.Canceled:
                int pressFrame = (int)((Time.time - LightPressStartTime) / Time.deltaTime);

                if (pressFrame > 0 && pressFrame <= ShortPress_Frame)
                    controlledActor.logicInput.InputAction(Enums.InputType.LightAttack);
                else if (pressFrame >= Hold_Frame)
                    controlledActor.logicInput.InputAction(Enums.InputType.LightAttack_Hold);
                break;
        }
    }

    double heavyPressStartTime = 0;
    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                heavyPressStartTime = Time.time;
                break;

            case InputActionPhase.Canceled:
                int pressFrame = (int)((Time.time - heavyPressStartTime) / Time.deltaTime);

                if (pressFrame > 0 && pressFrame <= ShortPress_Frame)
                    controlledActor.logicInput.InputAction(Enums.InputType.HeavyAttack);
                else if (pressFrame >= Hold_Frame)
                    controlledActor.logicInput.InputAction(Enums.InputType.HeavyAttack_Hold);
                break;
        }
    }

}
