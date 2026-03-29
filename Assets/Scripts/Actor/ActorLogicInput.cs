using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Actor))]
public class ActorLogicInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Actor actor;

    [Header("Input Buffer")]
    [SerializeField, Tooltip("Max age (seconds) for buffered input events; older entries are removed.")]
    private float _bufferValidTime = 0.2f;

    private Vector2 _moveInput = Vector2.zero;
    public Vector2 MoveInput => _moveInput;

    private Vector2 _lookInput = Vector2.zero;
    public Vector2 LookInput => _lookInput;

    private void Awake()
    {
        actor = GetComponent<Actor>();
    }

    private void Update()
    {
        MaintainBuffer();
        PushLocomotionIntent();
    }

    public void InputMove(Vector2 moveInput)
    {
        _moveInput = moveInput;
    }

    public void InputLook(Vector2 lookInput)
    {
        _lookInput = lookInput;
    }

    /// <summary>
    /// 由外部（例如 <see cref="PlayerInputController"/>）调用，汇总来自 Input System 的输入。
    /// </summary>
    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(inputData);
        
        // 写入缓冲；是否视为「已消费」由条件系统判断（例如 IsConsumed）。
        _inputBuffer.Add(new BufferedInput 
        { 
            Data = inputData, 
            Timestamp = Time.time
        });
    }

    #region === 输入缓冲 ===

    // BufferedInput：单次缓冲项（数据 + 时间戳）。
    public class BufferedInput
    {
        public InputData Data;
        public float Timestamp;
    }

    private List<BufferedInput> _inputBuffer = new List<BufferedInput>();

    // 对外只读；具体消费策略由条件（Condition）等处理。
    public IReadOnlyList<BufferedInput> InputBuffer => _inputBuffer;

    private void MaintainBuffer()
    {
        // 从后往前移除超时项（超过 _bufferValidTime 则删除）。
        for (int i = _inputBuffer.Count - 1; i >= 0; i--)
        {
            if (Time.time - _inputBuffer[i].Timestamp > _bufferValidTime)
            {
                _inputBuffer.RemoveAt(i);
            }
        }
    }

    // 供动作状态切换等时机清空缓冲（例如 ActionStateManager）。
    public void ClearBuffer()
    {
        _inputBuffer.Clear();
    }

    #endregion

    #region === 输入事件 ===

    private GenericEventManager<InputData> _inputEventManager = new GenericEventManager<InputData>();

    public void RegisterForInputEvent(object registrant, Action<InputData> callback)
    {
        _inputEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromInputEvent(object registrant)
    {
        _inputEventManager.Unsubscribe(registrant);
    }

    private void RaiseInputEvent(InputData inputData)
    {
        _inputEventManager.Publish(inputData);
    }
    #endregion

    #region === Locomotion：推送意图（锁定/自由相机、Mixer2D）→ ActorLocomotion ===

    private void PushLocomotionIntent()
    {
        if (actor == null || actor.locomotion == null)
            return;

        Vector2 move = Vector2.ClampMagnitude(_moveInput, 1f);

        if (move.sqrMagnitude <= 0.01f)
        {
            actor.locomotion.SetIntent(LocomotionIntent.Idle);
            return;
        }

        Vector3 worldDir;
        if (actor.cameraControl != null)
        {
            worldDir = actor.cameraControl.CalculateWorldDirection(move);
        }
        else
        {
            worldDir = new Vector3(move.x, 0f, move.y);
            if (worldDir.sqrMagnitude > 0.0001f)
                worldDir.Normalize();
            else
                worldDir = Vector3.zero;
        }

        if (worldDir.sqrMagnitude < 0.0001f)
        {
            actor.locomotion.SetIntent(LocomotionIntent.Idle);
            return;
        }

        worldDir.y = 0f;
        worldDir.Normalize();

        bool lockOn = actor.cameraControl != null
            && actor.cameraControl.CinemachineState != Enums.PlayerCameraState.Free;

        var intent = new LocomotionIntent
        {
            WorldMoveDirection = worldDir,
            MoveStrength = move.magnitude,
            Mixer2D = lockOn ? move : new Vector2(0f, move.magnitude),
            FacingDirection = Vector3.zero
        };

        if (lockOn)
        {
            if (TryGetLockFacingDirection(out Vector3 face))
                intent.FacingDirection = face;
        }

        actor.locomotion.SetIntent(intent);
    }

    private bool TryGetLockFacingDirection(out Vector3 faceDir)
    {
        faceDir = Vector3.zero;
        Transform target = actor.combater != null ? actor.combater.CombatTarget?.transform : null;
        if (target != null)
        {
            Vector3 to = target.position - actor.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
            {
                faceDir = to.normalized;
                return true;
            }
        }

        if (Camera.main != null)
        {
            Vector3 cf = Camera.main.transform.forward;
            cf.y = 0f;
            if (cf.sqrMagnitude > 0.001f)
            {
                faceDir = cf.normalized;
                return true;
            }
        }

        return false;
    }

    #endregion
}
