using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    private PlayerInputControl actions;

    public Actor controlledActor;

    [Header("Debug")]
    public bool debug = false;
    public float timeScale = 0.1f;

    [Header("Press Times")]
    [SerializeField] private int ShortPress_Frame = 40;
    [SerializeField] private int LongPress_Frame = 120;

    [SerializeField] private float joystickHard_Distance = 0.6f;
    [SerializeField] private float joystick_DeadZone = 0.1f;

    [Header("Raw Axes")]
    private Vector2 rawMove = Vector2.zero;
    private Vector2 rawLook = Vector2.zero;

    private readonly Dictionary<Enums.InputButton, InputPressState> buttonStates = new();
    private readonly Dictionary<Enums.InputJoystick, InputPressState> joystickStates = new();

    [Header("Input History")]
    [SerializeField, Tooltip("Max age in real seconds for buffered player input events.")]
    private float _bufferValidTime = 0.2f;

    private readonly List<BufferedInput> _inputHistory = new List<BufferedInput>(32);
    private readonly PlayerLocomotionIntentResolver _locomotionResolver = new PlayerLocomotionIntentResolver();
    private Actor _lastControlledActor;
    private ActorCameraControl _cachedCameraControl;

    public static PlayerInputController Instance { get; private set; }
    public Vector2 RawMove => rawMove;
    public Vector2 RawLook => rawLook;
    public IReadOnlyList<BufferedInput> InputHistory => _inputHistory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

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

    private void OnDestroy()
    {
        ClearPendingPlayerLocomotionIntent();
        if (Instance == this)
            Instance = null;

        actions?.Dispose();
        actions = null;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
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
        ClearPendingPlayerLocomotionIntent();
        actions?.Disable();
    }

    private void Update()
    {
        MaintainInputHistory();
        SyncControlledActorChange();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPaused = !UnityEditor.EditorApplication.isPaused;
#endif
        }

        UpdateInputState();
    }

    public bool GetInputState(Enums.InputButton button)
    {
        if (buttonStates.TryGetValue(button, out InputPressState state))
            return state.isActive;

        buttonStates[button] = new InputPressState(false);
        return false;
    }

    public bool GetInputState(Enums.InputJoystick joystick)
    {
        if (joystickStates.TryGetValue(joystick, out InputPressState state))
            return state.isActive;

        joystickStates[joystick] = new InputPressState(false);
        return false;
    }

    public bool IsControlledActor(Actor actor)
    {
        return actor != null && controlledActor == actor;
    }

    public void ClearInputHistory()
    {
        _inputHistory.Clear();
    }

    public bool TryConsumeInputHistory(IReadOnlyList<BufferedInput> entries)
    {
        if (entries == null || entries.Count == 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            BufferedInput entry = entries[i];
            if (entry == null || entry.IsConsumed || !_inputHistory.Contains(entry))
                return false;
        }

        for (int i = 0; i < entries.Count; i++)
            entries[i].Consume();

        return true;
    }

    internal void SubmitLocomotionIntentForFixedTick()
    {
        if (!isActiveAndEnabled)
            return;

        SyncControlledActorChange();
        if (controlledActor == null || controlledActor.actorMotor == null)
            return;

        ActorCameraControl cameraControl = ResolveCameraControl();
        LocomotionIntent intent = _locomotionResolver.Resolve(controlledActor, cameraControl, rawMove);
        controlledActor.actorMotor.SetLocomotionIntent(intent);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        rawMove = context.ReadValue<Vector2>();
        float distance = rawMove.sqrMagnitude;

        switch (context.phase)
        {
            case InputActionPhase.Performed:
                if (distance >= joystickHard_Distance)
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Hard);
                else
                    SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Light);

                SetInputState(CastVectorToDirection(rawMove), true);
                break;

            case InputActionPhase.Canceled:
                SendJoystickInputData(CastVectorToDirection(rawMove), Enums.JoystickVigor.Idle);
                SetInputState(Enums.InputJoystick.Idle, true);
                break;
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        rawLook = context.ReadValue<Vector2>();
    }

    public void OnLock(InputAction.CallbackContext context)
    {
        if (controlledActor == null || !context.performed)
            return;

        controlledActor.combater?.ToggleSoftLock();
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (controlledActor == null)
            return;

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
        if (controlledActor == null)
            return;

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
        if (controlledActor == null)
            return;

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
        if (controlledActor == null)
            return;

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

    private void SendButtonInputData(Enums.InputButton button, Enums.ButtonState state)
    {
        var buttonInput = new InputButtonData(button, state);
        AddInputHistory(buttonInput);

        if (debug)
            Debug.Log(buttonInput.inputButton + "   " + buttonInput.buttonState);
    }

    private void SendJoystickInputData(Enums.InputJoystick joystick, Enums.JoystickVigor vigor)
    {
        var joystickInput = new InputJoystickData(joystick, vigor);
        AddInputHistory(joystickInput);

        if (debug)
            Debug.Log(joystickInput.inputJoystick + "   " + joystickInput.joystickVigor);
    }

    private void SetInputState(Enums.InputButton button, bool active)
    {
        if (buttonStates.TryGetValue(button, out InputPressState state))
            state.isActive = active;
        else
            buttonStates[button] = state = new InputPressState(active);

        if (!active)
        {
            if (state.elapsedFrame == 0)
                return;

            if (state.elapsedFrame < ShortPress_Frame)
                SendButtonInputData(button, Enums.ButtonState.ShortPress);
            else
                SendButtonInputData(button, Enums.ButtonState.LongPress_Stop);
        }
    }

    private void SetInputState(Enums.InputJoystick joystick, bool active)
    {
        foreach (InputPressState state in joystickStates.Values)
            state.isActive = false;

        if (joystickStates.TryGetValue(joystick, out InputPressState current))
            current.isActive = active;
        else
            joystickStates[joystick] = new InputPressState(active);
    }

    private void UpdateInputState()
    {
        foreach (var pair in buttonStates)
        {
            InputPressState state = pair.Value;
            if (!state.isActive)
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
            }
        }

        foreach (var pair in joystickStates)
        {
            InputPressState state = pair.Value;
            if (!state.isActive)
            {
                state.elapsedFrame = 0;
                continue;
            }

            state.elapsedFrame++;
        }
    }

    private Enums.InputJoystick CastVectorToDirection(Vector2 input)
    {
        if (input.sqrMagnitude < joystick_DeadZone)
            return Enums.InputJoystick.Idle;

        Vector2 normalized = input.normalized;
        float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        return AngleToDirection(angle);
    }

    private static Enums.InputJoystick AngleToDirection(float angle)
    {
        if (angle <= 45f || angle >= 315f)
            return Enums.InputJoystick.East;

        if (angle <= 135f)
            return Enums.InputJoystick.North;

        if (angle <= 225f)
            return Enums.InputJoystick.West;

        return Enums.InputJoystick.South;
    }

    private void AddInputHistory(InputData inputData)
    {
        if (inputData == null)
            return;

        _inputHistory.Add(new BufferedInput(inputData, Time.unscaledTime));
    }

    private void MaintainInputHistory()
    {
        float now = Time.unscaledTime;
        for (int i = _inputHistory.Count - 1; i >= 0; i--)
        {
            if (now - _inputHistory[i].Timestamp > _bufferValidTime)
                _inputHistory.RemoveAt(i);
        }
    }

    private void SyncControlledActorChange()
    {
        if (_lastControlledActor == controlledActor)
            return;

        ClearPendingPlayerLocomotionIntent(_lastControlledActor);
        _lastControlledActor = controlledActor;
        _cachedCameraControl = null;
    }

    private ActorCameraControl ResolveCameraControl()
    {
        if (_cachedCameraControl != null && _cachedCameraControl.actor == controlledActor)
            return _cachedCameraControl;

        _cachedCameraControl = null;
        if (controlledActor == null)
            return null;

        _cachedCameraControl = controlledActor.GetComponent<ActorCameraControl>();
        if (_cachedCameraControl == null)
            _cachedCameraControl = controlledActor.GetComponentInChildren<ActorCameraControl>();
        if (_cachedCameraControl == null)
            _cachedCameraControl = controlledActor.GetComponentInParent<ActorCameraControl>();

        return _cachedCameraControl;
    }

    private void ClearPendingPlayerLocomotionIntent()
    {
        Actor actor = _lastControlledActor != null ? _lastControlledActor : controlledActor;
        ClearPendingPlayerLocomotionIntent(actor);
    }

    private static void ClearPendingPlayerLocomotionIntent(Actor actor)
    {
        actor?.actorMotor?.ClearPendingLocomotionIntent();
    }

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

    public sealed class BufferedInput
    {
        public BufferedInput(InputData data, float timestamp)
        {
            Data = data;
            Timestamp = timestamp;
        }

        public InputData Data { get; }
        public float Timestamp { get; }
        public bool IsConsumed { get; private set; }

        internal void Consume()
        {
            IsConsumed = true;
        }
    }
}

internal sealed class PlayerLocomotionIntentResolver
{
    public LocomotionIntent Resolve(Actor actor, ActorCameraControl cameraControl, Vector2 rawMove)
    {
        Vector2 move = Vector2.ClampMagnitude(rawMove, 1f);
        if (move.sqrMagnitude <= 0.01f)
            return LocomotionIntent.Idle;

        Vector3 worldDir = ResolveWorldMoveDirection(actor, cameraControl, move);
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
            return LocomotionIntent.Idle;

        worldDir.Normalize();
        return new LocomotionIntent
        {
            WorldMoveDirection = worldDir,
            MoveStrength = move.magnitude,
            FacingDirection = Vector3.zero,
        };
    }

    private static Vector3 ResolveWorldMoveDirection(
        Actor actor,
        ActorCameraControl cameraControl,
        Vector2 move)
    {
        if (TryResolveHardLockMoveDirection(actor, move, out Vector3 hardLockMoveDir))
            return hardLockMoveDir;

        if (cameraControl != null)
            return cameraControl.ToWorldMoveDirection(move);

        Vector3 fallback = new Vector3(move.x, 0f, move.y);
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
    }

    private static bool TryResolveHardLockMoveDirection(
        Actor actor,
        Vector2 move,
        out Vector3 worldDir)
    {
        worldDir = Vector3.zero;

        if (actor?.combater == null || actor.combater.LockMode != Enums.LockMode.HardLock)
            return false;

        Transform target = actor.combater.CombatTarget != null
            ? actor.combater.CombatTarget.transform
            : null;
        if (target == null)
            return false;

        Vector3 toTarget = target.position - actor.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return false;

        toTarget.Normalize();
        Vector3 tangentRight = Vector3.Cross(Vector3.up, toTarget);
        if (tangentRight.sqrMagnitude <= 0.0001f)
            return false;

        tangentRight.Normalize();
        worldDir = toTarget * move.y + tangentRight * move.x;
        return worldDir.sqrMagnitude > 0.0001f;
    }
}
