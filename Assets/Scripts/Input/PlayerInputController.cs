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

    [Header("调试")]
    public bool debug = false;
    public float timeScale = 0.1f;

    [Header("输入设置")]
    [SerializeField] int ShortPress_Frame = 40;
    [SerializeField] int LongPress_Frame = 120;

    [SerializeField] float joystickHard_Distance = 0.6f;

    // 输入状态 //
    private Vector2 rawMove = Vector2.zero;
    private Vector2 rawLook = Vector2.zero;
    Dictionary<Enums.InputButton, InputPressState> buttonStates = new ();
    Dictionary<Enums.InputJoystick, InputPressState> joystickStates = new ();

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

        if(debug)
            Time.timeScale = timeScale;
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
        if(controlledActor == null) return;

        this.controlledActor = controlledActor;
    }

    void Update()
    {
        if (controlledActor == null) return;

        // 更新Input
        UpdateInputState();

        // 处理角色移动
        controlledActor.logicInput.InputMove(rawMove);
    }

    void SendButtonInputData(Enums.InputButton button, Enums.ButtonState state)
    {
        InputButtonData buttonInput = new InputButtonData(button, state);

        controlledActor.logicInput.GetInputData(buttonInput);

        if (debug)
            Debug.Log(buttonInput.inputButton + "   " + buttonInput.buttonState);
    }

    void SendJoystickInputData(Enums.InputJoystick joystick, Enums.JoystickVigor vigor)
    {
        InputJoystickData joystickInput = new InputJoystickData(joystick, vigor);

        controlledActor.logicInput.GetInputData(joystickInput);

        if (debug)
            Debug.Log(joystickInput.inputJoystick + "   " + joystickInput.joystickVigor);
    }

    #region 获取Input

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float distance = rawMove.sqrMagnitude;

        switch (context.phase)
        {
            //这里应使用Performed而不是Started
            //从而持续获取新的输入
            case InputActionPhase.Performed:
                if (distance >= joystickHard_Distance)
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Hard);
                else
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Light);

                SetInputState(CastVectorToDirection(rawMove), true);
                break;

            case InputActionPhase.Canceled:
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Idle);

                SetInputState(CastVectorToDirection(rawMove), false);
                break;
        }

    }

    public void OnLook(InputAction.CallbackContext context)
    {
        rawLook = context.ReadValue<Vector2>();
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                SetInputState(Enums.InputButton.Dodge, true);
                break;

            case InputActionPhase.Canceled:
                SetInputState(Enums.InputButton.Dodge, false);
                break;
        }
    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                SetInputState(Enums.InputButton.LightAttack, true);
                break;

            case InputActionPhase.Canceled:
                SetInputState(Enums.InputButton.LightAttack, false);
                break;
        }
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                SetInputState(Enums.InputButton.HeavyAttack, true);
                break;

            case InputActionPhase.Canceled:
                SetInputState(Enums.InputButton.HeavyAttack, false);
                break;
        }
    }

    #endregion

    #region Input工具

    // 数据需要频繁被引用修改且数据量不大 用类很合适
    // 并且修改字典里的类可以直接引用 相比起结构体更方便
    public class InputPressState
    {
        public bool isPressed;
        public int elapsedFrame;

        public InputPressState(bool pressed = false, int frame = 0)
        {
            isPressed = pressed;
            elapsedFrame = frame;
        }
    }

    void SetInputState(Enums.InputButton button, bool isPressed)
    {
        if(buttonStates.ContainsKey(button))
        {
            var state = buttonStates[button];
            state.isPressed = isPressed;
        }
        else
            buttonStates[button] = new InputPressState(isPressed);

        if(!isPressed)
        {
            var state = buttonStates[button];
            if(state.elapsedFrame == 0) return;

            if (state.elapsedFrame < ShortPress_Frame)
                SendButtonInputData(button, Enums.ButtonState.ShortPress);
            else
                SendButtonInputData(button, Enums.ButtonState.LongPress_Cancel);
        }
    }

    void SetInputState(Enums.InputJoystick joystick, bool isPressed)
    {
        if(joystickStates.ContainsKey(joystick))
        {
            var state = joystickStates[joystick];
            state.isPressed = isPressed;
        }
        else
            joystickStates[joystick] = new InputPressState(isPressed);

        if (!isPressed)
        {
            foreach(var state in joystickStates.Values)
                state.isPressed = false;
        }
    }

    public bool GetInputState(Enums.InputButton button)
    {
        if (buttonStates.ContainsKey(button))
            return buttonStates[button].isPressed;
        else
        {
            buttonStates[button] = new InputPressState();
            return false;
        }
    }

    public bool GetInputState(Enums.InputJoystick joystick)
    {
        if (joystickStates.ContainsKey(joystick))
            return joystickStates[joystick].isPressed;
        else
        {
            joystickStates[joystick] = new InputPressState();
            return false;
        }
    }

    void UpdateInputState()
    {
        foreach (var pair in buttonStates)
        {
            var state = pair.Value;
            if(!state.isPressed)
            {
                state.elapsedFrame = 0;
                continue;
            }
            state.elapsedFrame++;

            if (state.elapsedFrame == ShortPress_Frame)
            {
                SendButtonInputData(pair.Key, Enums.ButtonState.LongPress_Start);
                continue;
            }

            if (state.elapsedFrame > LongPress_Frame)
            {
                SendButtonInputData(pair.Key, Enums.ButtonState.LongPress_Cancel);
                state.isPressed = false;
                continue;
            }
        }

        foreach (var pair in joystickStates)
        {
            var state = pair.Value;
            if (!state.isPressed)
            {
                state.elapsedFrame = 0;
                continue;
            }

            state.elapsedFrame++;
        }
    }

    // 将输入向量转换为角度
    Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.1f)
            return Enums.InputJoystick.Idle;

        // 归一化输入向量以确保方向准确
        Vector2 normalized = input.normalized;

        // 计算输入向量的角度（0-360度）
        float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360; // 转换为0-360范围

        // 将角度映射到方向枚举
        return AngleToDirection(angle);
    }

    // 将角度转换为方向枚举
    Enums.InputJoystick AngleToDirection(float angle)
    {
        // 方向分区：
        // 东: 315°-45° (实际是 -45°到45°)
        // 北: 45°-135°
        // 西: 135°-225°
        // 南: 225°-315°

        if (angle <= 45f || angle >= 315f)
            return Enums.InputJoystick.East;

        if (angle <= 135f)
            return Enums.InputJoystick.North;

        if (angle <= 225f)
            return Enums.InputJoystick.West;

        return Enums.InputJoystick.South;
    }

    #endregion

}