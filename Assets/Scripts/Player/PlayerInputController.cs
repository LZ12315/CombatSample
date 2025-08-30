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
    public CinemachineFreeLook freelookCamera;
    private Camera mainCamera;

    public int ShortPress_Frame = 40;
    public int Hold_Frame = 120;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        actions = new PlayerInputControl();
        playerInput.actions = actions.asset;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        actions.Player.SetCallbacks(this);

        // 获取主相机引用
        mainCamera = Camera.main;
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
    }

    void Update()
    {
        if (controlledActor == null) return;

        controlledActor.logicInput.InputMove(CalculateMovementDirection(rawMove), moveDistance);
    }

    Vector3 CalculateMovementDirection(Vector2 input)
    {
        if (mainCamera == null)
        {
            // 如果主相机未设置，尝试获取
            mainCamera = Camera.main;
            if (mainCamera == null) return Vector3.zero;
        }

        // 使用主相机的方向`
        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = mainCamera.transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        // 将输入方向转换为世界空间方向
        Vector3 moveDirection = (cameraForward * input.y) + (cameraRight * input.x);

        return moveDirection;
    }

    Vector2 rawMove = Vector2.zero;
    float moveDistance = 0f;
    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        moveDistance = rawMove.magnitude;
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
