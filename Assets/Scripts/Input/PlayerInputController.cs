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
    public float timeScale = 0.1f;
    [Header("输入设置")]
    [SerializeField] int ShortPress_Frame = 40;
    [SerializeField] int LongPress_Frame = 120;

    [SerializeField] float joystickHard_Distance = 0.6f;

    // 输入状态 //
    private Vector2 rawMove = Vector2.zero;
    private Vector2 rawLook = Vector2.zero;
    Dictionary<Enums.InputButton, ButtonInputCounter> buttonCounters = new ();
    InputJoystickData lastJoystickInput;

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
        UpdateInput();

        // 更新Camera视角
        controlledActor.logicInput.InputLook(rawLook);

        // 处理角色移动
        controlledActor.logicInput.InputMove(rawMove);
    }

    void SendButtonInputData(Enums.InputButton button, Enums.ButtonState state)
    {
        InputButtonData buttonInput = new InputButtonData(button, state);

        controlledActor.logicInput.GetInputData(buttonInput);

        //Debug.Log(buttonInput.inputButton + "   " + buttonInput.buttonState);
    }

    void SendJoystickInputData(Enums.InputJoystick joystick, Enums.JoystickVigor vigor)
    {
        InputJoystickData joystickInput = new InputJoystickData(joystick, vigor);

        lastJoystickInput = joystickInput;
        controlledActor.logicInput.GetInputData(joystickInput);

        //Debug.Log(joystickInput.inputJoystick + "   " + joystickInput.joystickVigor);
    }

    #region 获取Input

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float distance = rawMove.sqrMagnitude;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                if (distance >= joystickHard_Distance)
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Hard);
                else
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Light);
                break;

            case InputActionPhase.Canceled:
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Idle);
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
                InvokeButtonCounter(Enums.InputButton.Dodge);
                break;

            case InputActionPhase.Canceled:
                IntrigueButtonCounter(Enums.InputButton.Dodge);
                break;
        }
    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                InvokeButtonCounter(Enums.InputButton.LightAttack);
                break;

            case InputActionPhase.Canceled:
                IntrigueButtonCounter(Enums.InputButton.LightAttack);
                break;
        }
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                InvokeButtonCounter(Enums.InputButton.HeavyAttack);
                break;

            case InputActionPhase.Canceled:
                IntrigueButtonCounter(Enums.InputButton.HeavyAttack);
                break;
        }
    }

    #endregion

    #region Input工具

    private class ButtonInputCounter
    {
        public bool active;
        public int count;

        public void Start()
        {
            active = true;
            count = 0;
        }

        public void Cancel()
        {
            active = false;
            count = 0;
        }
    }

    void InvokeButtonCounter(Enums.InputButton button)
    {
        if(buttonCounters.ContainsKey(button))
            buttonCounters[button].Start();
        else
        {
            ButtonInputCounter counter = new ButtonInputCounter();
            buttonCounters.Add(button, counter);
            counter.Start();
        }
    }

    void UpdateInput()
    {
        foreach (var pair in buttonCounters)
        {
            var counter = pair.Value;
            if (!counter.active) continue;

            counter.count++;

            if (counter.count == ShortPress_Frame)
            {
                SendButtonInputData(pair.Key, Enums.ButtonState.LongPress_Start);
                continue;
            }

            if (counter.count > LongPress_Frame)
            {
                SendButtonInputData(pair.Key, Enums.ButtonState.LongPress_Cancel);
                counter.Cancel();
                continue;
            }
        }

        if(lastJoystickInput != null)
            controlledActor.logicInput.GetInputData(lastJoystickInput);
    }

    void IntrigueButtonCounter(Enums.InputButton button)
    {
        if (!buttonCounters.ContainsKey(button)) return;

        var counter = buttonCounters[button];

        if(!counter.active) return;

        if(counter.count < ShortPress_Frame)
            SendButtonInputData(button, Enums.ButtonState.ShortPress);
        else
            SendButtonInputData(button, Enums.ButtonState.LongPress_Cancel);

        counter.Cancel();
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