using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CombatSample.Consts;
using System;
using DG.Tweening.Core.Easing;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    PlayerInputControl actions;
    public Actor controlledActor;

    [Header("????")]
    public bool debug = false;
    public float timeScale = 0.1f;

    [Header("????????")]
    [SerializeField] int ShortPress_Frame = 40;
    [SerializeField] int LongPress_Frame = 120;

    [SerializeField] float joystickHard_Distance = 0.6f;
    [SerializeField] float joystick_DeadZone = 0.1f;

    [Header("??????")]
    private Vector2 rawMove = Vector2.zero;
    private Vector2 rawLook = Vector2.zero;
    Dictionary<Enums.InputButton, InputPressState> buttonStates = new ();
    Dictionary<Enums.InputJoystick, InputPressState> joystickStates = new ();

    public static PlayerInputController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        actions = new PlayerInputControl();
        if (actions.asset == null)
        {
            Debug.LogError($"{nameof(PlayerInputControl)} failed to build InputActionAsset.", this);
            enabled = false;
            return;
        }

        actions.Player.SetCallbacks(this);
    }

    void OnDestroy()
    {
        actions?.Dispose();
        actions = null;
    }

    private void Start()
    {
        SetControlledActor(FindFirstObjectByType<Actor>());

        // ????????????????????
        Cursor.lockState = CursorLockMode.Locked;
        // ??Locked????????????????????????????
        Cursor.visible = false;

        if (debug)
            Time.timeScale = timeScale;
    }

    private void OnEnable()
    {
        actions?.Enable();
    }

    private void OnDisable()
    {
        actions?.Disable();
    }

    public void SetControlledActor(Actor controlledActor)
    {
        if(controlledActor == null) return;

        this.controlledActor = controlledActor;
    }

    void Update()
    {
        if (controlledActor == null) return;

        // ??ESC????????????????????????
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ????Input
        UpdateInputState();
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

    #region ???Input

    public bool GetInputState(Enums.InputButton button)
    {
        if (buttonStates.ContainsKey(button))
            return buttonStates[button].isActive;
        else
        {
            buttonStates[button] = new InputPressState(false);
            return false;
        }
    }

    public bool GetInputState(Enums.InputJoystick joystick)
    {
        if (joystickStates.ContainsKey(joystick))
            return joystickStates[joystick].isActive;
        else
        {
            joystickStates[joystick] = new InputPressState(false);
            return false;
        }
    }

    #endregion

    #region InputSystem???

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float distance = rawMove.sqrMagnitude;

        switch (context.phase)
        {
            //????????Performed??????Started
            //?????????????????
            case InputActionPhase.Performed:
                if (distance >= joystickHard_Distance)
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Hard);
                else
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Light);

                SetInputState(CastVectorToDirection(rawMove), true);
                //SetInputState(Enums.InputJoystick.Idle, false);
                break;

            case InputActionPhase.Canceled:
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Idle);

                SetInputState(Enums.InputJoystick.Idle, true);
                break;
        }

        controlledActor.logicInput.InputMove(rawMove);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        rawLook = context.ReadValue<Vector2>();
        controlledActor.logicInput.InputLook(rawLook);
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

        // ???????????????????????????
        if (Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

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

    #region ????????

    // ??????????????????????????????? ????????
    // ????????????????????????? ?????????????
    public class InputPressState
    {
        public bool isActive;
        public int elapsedFrame;

        public InputPressState(bool active = false, int frame = 0)
        {
            isActive = active;
            elapsedFrame = frame;
        }
    }

    void SetInputState(Enums.InputButton button, bool active)
    {
        if(buttonStates.ContainsKey(button))
        {
            var state = buttonStates[button];
            state.isActive = active;
        }
        else
            buttonStates[button] = new InputPressState(active);

        if(!active)
        {
            var state = buttonStates[button];
            if(state.elapsedFrame == 0) return;

            if (state.elapsedFrame < ShortPress_Frame)
                SendButtonInputData(button, Enums.ButtonState.ShortPress);
            else
                SendButtonInputData(button, Enums.ButtonState.LongPress_Stop);
        }
    }

    void SetInputState(Enums.InputJoystick joystick, bool active)
    {
        foreach (var state in joystickStates.Values)
            state.isActive = false;

        if (joystickStates.ContainsKey(joystick))
        {
            var state = joystickStates[joystick];
            state.isActive = active;
        }
        else
            joystickStates[joystick] = new InputPressState(active);
    }

    void UpdateInputState()
    {
        foreach (var pair in buttonStates)
        {
            var state = pair.Value;
            if(!state.isActive)
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
                SendButtonInputData(pair.Key, Enums.ButtonState.LongPress_Stop);
                state.isActive = false;
                continue;
            }
        }

        foreach (var pair in joystickStates)
        {
            var state = pair.Value;
            if (!state.isActive)
            {
                state.elapsedFrame = 0;
                continue;
            }

            state.elapsedFrame++;
        }
    }

    // ?????????????????
    Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        if (input.sqrMagnitude < joystick_DeadZone)
            return Enums.InputJoystick.Idle;

        // ????????????????????????
        Vector2 normalized = input.normalized;

        // ?????????????????0-360???
        float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360; // ????0-360????

        // ???????????????
        return AngleToDirection(angle);
    }

    // ????????????????
    Enums.InputJoystick AngleToDirection(float angle)
    {
        // ?????????
        // ??: 315??-45?? (????? -45??45??)
        // ??: 45??-135??
        // ??: 135??-225??
        // ??: 225??-315??

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