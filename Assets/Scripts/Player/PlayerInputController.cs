using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CombatSample.Consts;

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
    private Vector2 _rawLook = Vector2.zero;

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
        UpdateInputCounters();

        // 更新Camera视角
        controlledActor.cameraControl.HandleCameraRotation(_rawLook);

        // 处理角色移动
        controlledActor.logicInput.InputMove(_rawMove);
    }

    //private class InputCounter
    //{
    //    public Enums.InputButton inputButton = Enums.InputButton.None;
    //    public int inputCounter = 0;
    //    public bool isHolded = false;
    //    private PlayerInputController inputController;

    //    public void InitCounter(PlayerInputController controller, Enums.InputButton button)
    //    {
    //        inputButton = button;
    //        inputController = controller;
    //    }

    //    public void UpdateCounter()
    //    {
    //        inputCounter++;

    //        if (inputCounter == inputController.ShortPress_Frame + 1)
    //            SendInputData(Enums.InputState.Hold);
    //    }

    //    private void SendInputData(Enums.InputState state)
    //    {
    //        InputButtonData inputButtonData = new InputButtonData(inputButton, state);
    //        inputController.controlledActor.logicInput.GetInputData(inputButtonData);
    //    }

    //}

    private void UpdateInputCounters()
    {
        if (_lightAttackPressed) _lightPressFrames++;
        if (_heavyAttackPressed) _heavyPressFrames++;
    }

    Vector2 _validMove = Vector2.zero;
    double moveInputTime = 0;
    public void OnMove(InputAction.CallbackContext context)
    {
        _rawMove = context.ReadValue<Vector2>();
        float moveDistance = _rawMove.magnitude;

        if (moveDistance > 0.01)
        {
            _validMove = _rawMove;
            moveInputTime = Time.time;
        }
        else if(moveInputTime != 0)
        {
            double _movePressFrames = (Time.time - moveInputTime) * 60;
            InputJoystickData moveInputData = new InputJoystickData();
            if (_movePressFrames <= ShortPress_Frame)
            {
                moveInputData.inputJoystick = CastVectorToDirection(_validMove);
                moveInputData.inputState = Enums.InputState.ShortPress;
                controlledActor.logicInput.GetInputData(moveInputData);
            }
        }

    }

    Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        return Enums.InputJoystick.None;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        _rawLook = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {

    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        InputButtonData lightAttackInputData = new InputButtonData();
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
                    lightAttackInputData.SetValue(Enums.InputButton.LightAttack, Enums.InputState.ShortPress);
                    controlledActor.logicInput.GetInputData(lightAttackInputData);
                }
                else if (_lightPressFrames > ShortPress_Frame)
                {
                    lightAttackInputData.SetValue(Enums.InputButton.LightAttack, Enums.InputState.Release);
                    controlledActor.logicInput.GetInputData(lightAttackInputData);
                }
                break;
        }
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        InputButtonData heavyAttackInputData = new InputButtonData();
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
                    heavyAttackInputData.SetValue(Enums.InputButton.HeavyAttack, Enums.InputState.ShortPress);
                    controlledActor.logicInput.GetInputData(heavyAttackInputData);
                }
                else if (_heavyPressFrames >= Hold_Frame)
                {
                    heavyAttackInputData.SetValue(Enums.InputButton.HeavyAttack, Enums.InputState.Release);
                    controlledActor.logicInput.GetInputData(heavyAttackInputData);
                }
                break;
        }
    }

    public void OnDodge(InputAction.CallbackContext context)
    {

    }
}