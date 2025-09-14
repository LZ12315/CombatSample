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

    // 输入状态 //
    private Vector2 rawMove = Vector2.zero;
    private Vector2 rawLook = Vector2.zero;
    Dictionary<Enums.InputButton, ButtonInputCounter> buttonCounters = new ();

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

        // 
        UpdateButtonCounter();

        // 更新Camera视角
        controlledActor.cameraControl.HandleCameraRotation(rawLook);

        // 处理角色移动
        controlledActor.logicInput.InputMove(rawMove);
    }

    void SendButtonInputData(Enums.InputButton button, Enums.InputState state)
    {
        InputButtonData buttonInput = new InputButtonData();
        buttonInput.SetValue(button, state);
    }

    void SendJoystickInputData(Enums.InputJoystick joystick, Enums.InputState state, Enums.JoystickVigor vigor)
    {
        InputJoystickData JoystickInput = new InputJoystickData();
        JoystickInput.SetValue(joystick, state, vigor);
    }

    #region 获取Input

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float moveDistance = rawMove.magnitude;

        if (moveDistance > 0.01f)
        {
            if(rawMove.sqrMagnitude >= 0.6f)
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.InputState.Press, Enums.JoystickVigor.Hard);
            else
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.InputState.Press, Enums.JoystickVigor.Light);
        }
        else
            SendJoystickInputData(Enums.InputJoystick.None, Enums.InputState.Release, Enums.JoystickVigor.None);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        rawLook = context.ReadValue<Vector2>();
    }

    public void OnDodge(InputAction.CallbackContext context)
    {

    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                InvokeButtonCounter(Enums.InputButton.LightAttack);
                SendButtonInputData(Enums.InputButton.LightAttack, Enums.InputState.Press);
                break;

            case InputActionPhase.Canceled:
                DisableButtonCounter(Enums.InputButton.LightAttack);
                SendButtonInputData(Enums.InputButton.LightAttack, Enums.InputState.Release);
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
                SendButtonInputData(Enums.InputButton.HeavyAttack, Enums.InputState.Press);
                break;

            case InputActionPhase.Canceled:
                DisableButtonCounter(Enums.InputButton.HeavyAttack);
                SendButtonInputData(Enums.InputButton.HeavyAttack, Enums.InputState.Release);
                break;
        }
    }

    #endregion

    #region Input工具

    private struct ButtonInputCounter
    {
        public bool active;
        public int count;

        public void Init()
        {
            active = true;
            count = 0;
        }

        public void Disable()
        {
            active = false;
            count = 0;
        }
    }

    void InvokeButtonCounter(Enums.InputButton button)
    {
        if(buttonCounters.ContainsKey(button))
            buttonCounters[button].Init();
        else
        {
            ButtonInputCounter counter = new ButtonInputCounter();
            counter.Init();
            buttonCounters.Add(button, counter);
        }
    }

    void UpdateButtonCounter()
    {
        foreach (var pair in buttonCounters)
        {
            var counter = pair.Value;
            if(!counter.active) return;

            if(counter.count == 40)
            {
                SendButtonInputData(pair.Key, Enums.InputState.Hold);
                counter.Disable();
                continue;
            }

            counter.count++;
        }
    }

    void DisableButtonCounter(Enums.InputButton button)
    {
        if (!buttonCounters.ContainsKey(button)) return;

        var counter = buttonCounters[button];
        counter.Disable();
    }

    // 将输入向量转换为角度
    Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.1f)
            return Enums.InputJoystick.None;

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