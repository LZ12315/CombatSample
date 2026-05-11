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

    private ICameraFrameProvider _cameraFrameProvider;

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

    public void SetCameraFrameProvider(ICameraFrameProvider provider)
    {
        _cameraFrameProvider = provider;
    }

    public void ClearCameraFrameProvider(ICameraFrameProvider provider)
    {
        if (_cameraFrameProvider == provider)
            _cameraFrameProvider = null;
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

        // 是否已被某个 Condition 命中并消费（例如 InputSequenceCondition 命中胜选后标记）。
        // 扫描方应跳过 IsConsumed == true 的条目，避免同一条输入被后续 Action 重复吃掉
        // （典型场景：一段跳命中后，二段跳不应再吃同一条 Jump ShortPress）。
        public bool IsConsumed;
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

    #region === Locomotion：推送意图 → ActorMotor ===

    private void PushLocomotionIntent()
    {
        if (actor == null || actor.actorMotor == null)
            return;

        Vector2 move = Vector2.ClampMagnitude(_moveInput, 1f);

        if (move.sqrMagnitude <= 0.01f)
        {
            actor.actorMotor.SetLocomotionIntent(LocomotionIntent.Idle);
            return;
        }

        Vector3 worldDir;
        if (_cameraFrameProvider != null)
        {
            worldDir = _cameraFrameProvider.ToWorldMoveDirection(move);
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
            actor.actorMotor.SetLocomotionIntent(LocomotionIntent.Idle);
            return;
        }

        worldDir.y = 0f;
        worldDir.Normalize();

        var intent = new LocomotionIntent
        {
            WorldMoveDirection = worldDir,
            MoveStrength = move.magnitude,
            FacingDirection = Vector3.zero
        };

        actor.actorMotor.SetLocomotionIntent(intent);
    }

    #endregion
}
