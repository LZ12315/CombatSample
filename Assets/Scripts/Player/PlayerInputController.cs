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

    [Header("输入设置")]
    public int ShortPress_Frame = 40;
    public int Hold_Frame = 120;

    // 输入状态 //
    private Vector2 _rawMove = Vector2.zero;
    private float _moveDistance = 0f;
    private Vector2 _rawLook = Vector2.zero;

    // 攻击状态 //
    private int _lightPressFrames = 0;
    private bool _lightAttackPressed = false;
    private int _heavyPressFrames = 0;
    private bool _heavyAttackPressed = false;

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
    }

    void Update()
    {
        if (controlledActor == null) return;

        // 更新攻击帧计数器
        UpdateAttackCounters();

        // 更新Camera视角
        controlledActor.cameraControl.HandleCameraRotation(_rawLook);

        // 处理角色移动
        Vector3 moveDir = controlledActor.cameraControl.CalculateMovementDirection(_rawMove);
        controlledActor.logicInput.InputMove(moveDir, _moveDistance);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _rawMove = context.ReadValue<Vector2>();
        _moveDistance = _rawMove.magnitude;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        _rawLook = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {

    }

    private void UpdateAttackCounters()
    {
        if (_lightAttackPressed) _lightPressFrames++;
        if (_heavyAttackPressed) _heavyPressFrames++;
    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                _lightAttackPressed = true;
                _lightPressFrames = 0;
                break;

            case InputActionPhase.Canceled:
                _lightAttackPressed = false;

                if (_lightPressFrames <= ShortPress_Frame)
                {
                    controlledActor.logicInput.InputAction(Enums.InputType.LightAttack);
                }
                else if (_lightPressFrames >= Hold_Frame)
                {
                    controlledActor.logicInput.InputAction(Enums.InputType.LightAttack_Hold);
                }
                break;
        }
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                _heavyAttackPressed = true;
                _heavyPressFrames = 0;
                break;

            case InputActionPhase.Canceled:
                _heavyAttackPressed = false;

                if (_heavyPressFrames <= ShortPress_Frame)
                {
                    controlledActor.logicInput.InputAction(Enums.InputType.HeavyAttack);
                }
                else if (_heavyPressFrames >= Hold_Frame)
                {
                    controlledActor.logicInput.InputAction(Enums.InputType.HeavyAttack_Hold);
                }
                break;
        }
    }
}
