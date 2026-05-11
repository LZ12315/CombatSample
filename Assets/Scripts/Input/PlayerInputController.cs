using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    PlayerInputControl actions;
    public Actor controlledActor;
    [SerializeField] private ActorLogicInput logicInput;

    [Header("Debug")]
    public bool debug = false;
    public float timeScale = 0.1f;

    [Header("Press Times")]
    [SerializeField] int ShortPress_Frame = 40;
    [SerializeField] int LongPress_Frame = 120;

    [SerializeField] float joystickHard_Distance = 0.6f;
    [SerializeField] float joystick_DeadZone = 0.1f;

    [Header("Raw Axes")]
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
        // 锁定鼠标用于视角；第一人称常用。
        Cursor.lockState = CursorLockMode.Locked;
        // Locked 下隐藏指针，减少 UI/SkinnedMesh 一类问题。
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

    void Update()
    {
        if (controlledActor == null) return;

        // ESC：解锁鼠标，方便调试或切出。
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // R：暂停/恢复 Editor 运行，方便逐帧检查（用 Game 视图顶部的 Step 按钮逐帧前进）。
        if (Input.GetKeyDown(KeyCode.R))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPaused = !UnityEditor.EditorApplication.isPaused;
#endif
        }

        // 每帧更新长按帧计数并派发 Short/Long 事件。
        UpdateInputState();
    }

    void SendButtonInputData(Enums.InputButton button, Enums.ButtonState state)
    {
        ActorLogicInput logicInput = ResolveLogicInput();
        if (logicInput == null) return;

        InputButtonData buttonInput = new InputButtonData(button, state);

        logicInput.GetInputData(buttonInput);

        if (debug)
            Debug.Log(buttonInput.inputButton + "   " + buttonInput.buttonState);
    }

    void SendJoystickInputData(Enums.InputJoystick joystick, Enums.JoystickVigor vigor)
    {
        ActorLogicInput logicInput = ResolveLogicInput();
        if (logicInput == null) return;

        InputJoystickData joystickInput = new InputJoystickData(joystick, vigor);

        logicInput.GetInputData(joystickInput);

        if (debug)
            Debug.Log(joystickInput.inputJoystick + "   " + joystickInput.joystickVigor);
    }

    #region 输入状态查询

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

    #region Input System 回调

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float distance = rawMove.sqrMagnitude;

        switch (context.phase)
        {
            // 摇杆用 Performed 持续上报；不用 Started（避免只响一次）。
            // 推力达硬阈值发 Hard，否则 Light。
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

        ResolveLogicInput()?.InputMove(rawMove);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        rawLook = context.ReadValue<Vector2>();
        ResolveLogicInput()?.InputLook(rawLook);
    }

    public void OnLock(InputAction.CallbackContext context)
    {
        if (controlledActor == null || !context.performed) return;

        controlledActor.combater?.ToggleSoftLock();
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

        // 若指针已解锁（例如点过 UI），点攻击时重新锁定并隐藏。
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

    public void OnJump(InputAction.CallbackContext context)
    {
        if (controlledActor == null) return;

        switch (context.phase)
        {
            case InputActionPhase.Started:
                SetInputState(Enums.InputButton.Jump, true);
                break;

            case InputActionPhase.Canceled:
                SetInputState(Enums.InputButton.Jump, false);
                break;
        }
    }

    #endregion

    #region 按压计时与八向

    // isActive：当前是否按住；elapsedFrame：已持续帧数。
    // 松手时根据帧数发 ShortPress 或 LongPress_Stop。
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

    // 摇杆向量 → 八向枚举（含 Idle）。
    Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        if (input.sqrMagnitude < joystick_DeadZone)
            return Enums.InputJoystick.Idle;

        Vector2 normalized = input.normalized;

        // Atan2 得带符号角，换算到 0–360。
        float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        return AngleToDirection(angle);
    }

    // 东：315°–45°（跨过 0°）；北：45°–135°；西：135°–225°；南：225°–315°。
    Enums.InputJoystick AngleToDirection(float angle)
    {
        if (angle <= 45f || angle >= 315f)
            return Enums.InputJoystick.East;

        if (angle <= 135f)
            return Enums.InputJoystick.North;

        if (angle <= 225f)
            return Enums.InputJoystick.West;

        return Enums.InputJoystick.South;
    }

    #endregion

    private ActorLogicInput ResolveLogicInput()
    {
        if (logicInput != null)
            return logicInput;

        if (controlledActor == null)
            return null;

        logicInput = controlledActor.GetComponent<ActorLogicInput>();
        return logicInput;
    }

}
