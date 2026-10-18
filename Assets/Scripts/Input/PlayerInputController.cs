using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single local-player device adapter. Gameplay consumes the journal at the Driver's
/// fixed boundary; camera/UI keep their presentation clock. This component owns the
/// runtime action instance and all enable/disable permissions.
/// </summary>
public class PlayerInputController : MonoBehaviour, PlayerInputControl.IPlayerActions
{
    private PlayerInputControl actions;
    public Actor controlledActor;

    [Header("Debug")]
    public bool debug;
    public float timeScale = 0.1f;

    [Header("Gameplay Input")]
    [SerializeField] private float joystick_DeadZone = 0.1f;
    [SerializeField, Tooltip("Freshness of a terminal command in real seconds, including during hit stop.")]
    private float _bufferValidTime = 0.2f;

    private Vector2 rawLook;
    private bool _lookIsMouse;
    private bool _ownsInput;
    private bool _hasFocus = true;
    private bool _gameplayEnabled = true;
    private bool _lookEnabled = true;
    private bool _uiEnabled;
    private bool _availableLast;
    private Enums.InputButton _physicallyHeld;
    private Actor _lastControlledActor;
    private ActorCameraControl _cachedCameraControl;
    private readonly PlayerInputBuffer _buffer = new PlayerInputBuffer();
    private readonly PlayerInputDirections _directions = new PlayerInputDirections();
    private readonly PlayerLocomotionIntentResolver _locomotionResolver = new PlayerLocomotionIntentResolver();

    public static PlayerInputController Instance { get; private set; }
    public Vector2 RawMove => CanUseGameplay ? _buffer.Move : Vector2.zero;
    public Vector2 RawLook => CanUseLook ? rawLook : Vector2.zero;
    public bool IsLookInputEnabled => CanUseLook;
    public InputActionAsset InputActions => actions?.asset;
    public System.Collections.Generic.IReadOnlyList<PlayerInputEvent> InputEvents => _buffer.Events;
    private bool CanUseGameplay => _ownsInput && isActiveAndEnabled && _hasFocus && _gameplayEnabled
        && controlledActor != null && controlledActor.isActiveAndEnabled;
    private bool CanUseLook => _ownsInput && isActiveAndEnabled && _hasFocus && _lookEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _ownsInput = true;
        actions = new PlayerInputControl();
        actions.Player.SetCallbacks(this);
        // Held-at-enable buttons must enter the native action state too, so their
        // eventual release is observable even though the initial press is blocked.
        actions.Player.LightAttack.wantsInitialStateCheck = true;
        actions.Player.HeavyAttack.wantsInitialStateCheck = true;
        actions.Player.Dodge.wantsInitialStateCheck = true;
        actions.Player.Jump.wantsInitialStateCheck = true;
        actions.Player.Lock.wantsInitialStateCheck = true;
    }

    private void OnEnable()
    {
        if (!_ownsInput) return;
        _hasFocus = Application.isFocused;
        RefreshActionMaps();
        _physicallyHeld = ReadPhysicallyHeldButtons();
        SyncControlledActorChange();
        ResetGameplayState();
        _availableLast = CanUseGameplay;
    }

    private void OnDisable()
    {
        // Gate and discard before Disable emits synthetic canceled callbacks.
        ResetGameplayState();
        rawLook = Vector2.zero;
        actions?.Disable();
        _availableLast = false;
    }

    private void OnDestroy()
    {
        ResetGameplayState();
        if (Instance == this) Instance = null;
        actions?.Dispose();
        actions = null;
    }

    private void OnApplicationFocus(bool focus)
    {
        _hasFocus = focus;
        ResetGameplayState();
        rawLook = Vector2.zero;
        RefreshActionMaps();
        if (focus) _physicallyHeld = ReadPhysicallyHeldButtons();
        _buffer.Reset(_physicallyHeld);
        _availableLast = CanUseGameplay;
    }

    private void Start()
    {
        if (!_ownsInput) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (debug) Time.timeScale = timeScale;
    }

    private void Update()
    {
        SyncControlledActorChange();
        SyncAvailability();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R))
            UnityEditor.EditorApplication.isPaused = !UnityEditor.EditorApplication.isPaused;
#endif
    }

    private void OnValidate()
    {
        _bufferValidTime = float.IsNaN(_bufferValidTime) || float.IsInfinity(_bufferValidTime)
            ? 0.2f : Mathf.Max(0f, _bufferValidTime);
        joystick_DeadZone = Mathf.Clamp01(joystick_DeadZone);
    }

    public void SetControlledActor(Actor actor)
    {
        controlledActor = actor;
        SyncControlledActorChange();
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        if (_gameplayEnabled == enabled) return;
        _gameplayEnabled = enabled;
        ResetGameplayState();
        RefreshActionMaps();
        _physicallyHeld = ReadPhysicallyHeldButtons();
        _buffer.Reset(_physicallyHeld);
        _availableLast = CanUseGameplay;
    }

    public void SetLookInputEnabled(bool enabled)
    {
        _lookEnabled = enabled;
        rawLook = Vector2.zero;
        RefreshActionMaps();
    }

    public void SetUIInputEnabled(bool enabled)
    {
        _uiEnabled = enabled;
        RefreshActionMaps();
    }

    /// <summary>Menu integration point, not a pause-menu or time-scale implementation.</summary>
    public void SetMenuInputActive(bool active)
    {
        SetGameplayInputEnabled(!active);
        SetLookInputEnabled(!active);
        SetUIInputEnabled(active);
    }

    private void RefreshActionMaps()
    {
        if (actions == null) return;
        bool active = _ownsInput && isActiveAndEnabled && _hasFocus;
        if (active && (_gameplayEnabled || _lookEnabled)) actions.Player.Enable();
        else actions.Player.Disable();
        if (active && _uiEnabled) actions.UI.Enable();
        else actions.UI.Disable();
    }

    public bool TryGetLookInput(out Vector2 look, out bool isMouse)
    {
        if (CanUseLook)
        {
            rawLook = actions.Player.Look.ReadValue<Vector2>();
            _lookIsMouse = actions.Player.Look.activeControl != null
                && actions.Player.Look.activeControl.device is Mouse;
        }
        look = RawLook;
        isMouse = _lookIsMouse;
        return CanUseLook;
    }

    public bool IsControlledActor(Actor actor) => CanUseGameplay && controlledActor == actor;
    public bool GetInputState(Enums.InputButton button) => CanUseGameplay && _buffer.IsHeld(button);
    public double GetHeldSeconds(Enums.InputButton button) => CanUseGameplay ? _buffer.HeldSeconds(button) : 0d;
    public void ClearInputEvents() => _buffer.ClearEvents();

    internal void SubmitLocomotionIntentForFixedTick()
    {
        SyncControlledActorChange();
        SyncAvailability();
        if (!CanUseGameplay) return;
        // Poll continuous Move after re-enable even if no new value-change callback
        // arrives. Buttons remain edge-driven and must be released to re-arm.
        double now = Time.realtimeSinceStartupAsDouble;
        QueueMove(actions.Player.Move.ReadValue<Vector2>(), now);
        _buffer.BeginFixedTick(now, _bufferValidTime);
        controlledActor.PlayerInput = _buffer;
        while (_buffer.TryConsumePress(Enums.InputButton.Lock))
            controlledActor.combater?.ToggleSoftLock();
        ActorCameraControl cameraControl = ResolveCameraControl();
        // One coordinate sample for this publication, after Lock has been applied.
        // This is a fixed-boundary approximation, not historical camera/target replay.
        _directions.Publish(_buffer, PlayerLocomotionIntentResolver.CaptureDirectionFrame(controlledActor, cameraControl));
        controlledActor.PlayerInputDirections = _directions;
        if (controlledActor.actorLocomotion != null)
        {
            LocomotionIntent intent = _locomotionResolver.Resolve(controlledActor, cameraControl, _buffer.Move);
            controlledActor.actorLocomotion.SetLocomotionIntent(intent);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        SyncControlledActorChange();
        SyncAvailability();
        if (!CanUseGameplay) return;
        QueueMove(context.ReadValue<Vector2>(), context.time);
    }

    private void QueueMove(Vector2 move, double time) =>
        _buffer.QueueMove(move.sqrMagnitude < joystick_DeadZone * joystick_DeadZone ? Vector2.zero : move, time);

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!CanUseLook) return;
        rawLook = context.ReadValue<Vector2>();
        _lookIsMouse = context.control != null && context.control.device is Mouse;
    }

    public void OnLock(InputAction.CallbackContext context) => RecordButton(Enums.InputButton.Lock, context);
    public void OnDodge(InputAction.CallbackContext context) => RecordButton(Enums.InputButton.Dodge, context);
    public void OnHeavyAttack(InputAction.CallbackContext context) => RecordButton(Enums.InputButton.HeavyAttack, context);
    public void OnJump(InputAction.CallbackContext context) => RecordButton(Enums.InputButton.Jump, context);

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (CanUseGameplay && context.performed && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        RecordButton(Enums.InputButton.LightAttack, context);
    }

    private void RecordButton(Enums.InputButton button, InputAction.CallbackContext context)
    {
        if (!context.performed && !context.canceled) return;
        SyncControlledActorChange();
        SyncAvailability();
        bool down = context.performed;
        if (down) _physicallyHeld |= button;
        else _physicallyHeld &= ~button;
        if (!CanUseGameplay) return;
        _buffer.QueueButton(button, down, context.time);
        if (debug) Debug.Log($"{button}: {(down ? "Pressed" : "Released")} @ {context.time:R}");
    }

    private Enums.InputButton ReadPhysicallyHeldButtons()
    {
        if (actions == null) return Enums.InputButton.None;
        Enums.InputButton held = Enums.InputButton.None;
        Check(actions.Player.LightAttack, Enums.InputButton.LightAttack);
        Check(actions.Player.HeavyAttack, Enums.InputButton.HeavyAttack);
        Check(actions.Player.Dodge, Enums.InputButton.Dodge);
        Check(actions.Player.Jump, Enums.InputButton.Jump);
        Check(actions.Player.Lock, Enums.InputButton.Lock);
        return held;
        void Check(InputAction action, Enums.InputButton button)
        {
            foreach (InputControl control in action.controls)
                if (control.IsPressed()) { held |= button; break; }
        }
    }

    private void SyncControlledActorChange()
    {
        if (_lastControlledActor == controlledActor) return;
        ResetGameplayState();
        _lastControlledActor = controlledActor;
        _cachedCameraControl = null;
        _availableLast = CanUseGameplay;
    }

    private void SyncAvailability()
    {
        bool available = CanUseGameplay;
        if (_availableLast == available) return;
        ResetGameplayState();
        _availableLast = available;
    }

    private void ResetGameplayState()
    {
        if (_lastControlledActor != null)
        {
            _lastControlledActor.actorLocomotion?.ClearLocomotionIntent();
            if (ReferenceEquals(_lastControlledActor.PlayerInput, _buffer))
            {
                _lastControlledActor.PlayerInput = null;
                _lastControlledActor.PlayerInputDirections = null;
            }
        }
        _buffer.Reset(_physicallyHeld);
    }

    private ActorCameraControl ResolveCameraControl()
    {
        if (_cachedCameraControl != null && _cachedCameraControl.actor == controlledActor)
            return _cachedCameraControl;
        _cachedCameraControl = controlledActor.GetComponent<ActorCameraControl>();
        if (_cachedCameraControl == null) _cachedCameraControl = controlledActor.GetComponentInChildren<ActorCameraControl>();
        if (_cachedCameraControl == null) _cachedCameraControl = controlledActor.GetComponentInParent<ActorCameraControl>();
        return _cachedCameraControl;
    }

}

internal sealed class PlayerLocomotionIntentResolver
{
    public LocomotionIntent Resolve(Actor actor, ActorCameraControl cameraControl, Vector2 rawMove)
    {
        Vector2 move = Vector2.ClampMagnitude(rawMove, 1f);
        bool hasHardLockFacing = TryResolveHardLockFacing(actor, out Vector3 hardLockFacing);
        if (move.sqrMagnitude <= 0.01f)
            return hasHardLockFacing ? new LocomotionIntent { FacingDirection = hardLockFacing } : LocomotionIntent.Idle;
        Vector3 worldDir = ResolveWorldMoveDirection(cameraControl, move, hasHardLockFacing ? hardLockFacing : Vector3.zero);
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
            return hasHardLockFacing ? new LocomotionIntent { FacingDirection = hardLockFacing } : LocomotionIntent.Idle;
        worldDir.Normalize();
        return new LocomotionIntent
        {
            WorldMoveDirection = worldDir,
            MoveStrength = Mathf.Clamp01(move.magnitude),
            FacingDirection = hasHardLockFacing ? hardLockFacing : worldDir,
        };
    }

    internal static InputDirectionFrame CaptureDirectionFrame(Actor actor, ActorCameraControl cameraControl)
    {
        if (actor == null || actor.combater == null || !actor.combater.isActiveAndEnabled || !actor.combater.IsLocked) return default;
        GameObject target = actor.combater.CombatTarget;
        if (target == null || !target.activeInHierarchy) return default;
        ActorCombater targetCombat = target.GetComponent<ActorCombater>();
        if (targetCombat == null || !targetCombat.isActiveAndEnabled || targetCombat.IsDead) return default;
        Vector3 facing = target.transform.position - actor.transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude <= 0.0001f) return default;
        facing.Normalize();
        bool hard = TryResolveHardLockFacing(actor, out Vector3 hardFacing);
        Camera camera = !hard && cameraControl != null ? Camera.main : null;
        return new InputDirectionFrame(target.GetInstanceID(), camera != null ? camera.GetInstanceID() : 0,
            (int)actor.combater.LockMode,
            ResolveWorldMoveDirection(cameraControl, Vector2.up, hard ? hardFacing : Vector3.zero),
            ResolveWorldMoveDirection(cameraControl, Vector2.right, hard ? hardFacing : Vector3.zero), facing, actor.combater.LockRevision);
    }

    private static Vector3 ResolveWorldMoveDirection(ActorCameraControl cameraControl, Vector2 move, Vector3 hardLockFacing)
    {
        if (hardLockFacing.sqrMagnitude > 0.0001f)
            return hardLockFacing * move.y + Vector3.Cross(Vector3.up, hardLockFacing).normalized * move.x;
        if (cameraControl != null) return cameraControl.ToWorldMoveDirection(move);
        Vector3 fallback = new Vector3(move.x, 0f, move.y);
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
    }

    private static bool TryResolveHardLockFacing(Actor actor, out Vector3 facing)
    {
        facing = Vector3.zero;
        if (actor?.combater == null || actor.combater.LockMode != Enums.LockMode.HardLock) return false;
        Transform target = actor.combater.CombatTarget != null ? actor.combater.CombatTarget.transform : null;
        if (target == null) return false;
        Vector3 toTarget = target.position - actor.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return false;
        facing = toTarget.normalized;
        return true;
    }
}
