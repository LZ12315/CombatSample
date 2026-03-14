using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Actor))]
public class ActorLogicInput : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private Actor actor;

    [Header("输入缓冲配置")]
    [SerializeField, Tooltip("指令在缓冲池中存活的时间（秒）")]
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
    /// 接收外部（如 InputSystem）传来的按键事件
    /// </summary>
    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(inputData);
        
        // 压入带时间戳的缓冲池 (? 删除了冗余的 IsConsumed)
        _inputBuffer.Add(new BufferedInput 
        { 
            Data = inputData, 
            Timestamp = Time.time
        });
    }

    #region === 输入缓冲 ===

    // === 内部类：带时间戳的输入指令 ===
    public class BufferedInput
    {
        public InputData Data;
        public float Timestamp;
    }

    private List<BufferedInput> _inputBuffer = new List<BufferedInput>();

    // ? 新增：对外暴露只读的缓冲池，供所有 Condition 公平、无副作用地查阅
    public IReadOnlyList<BufferedInput> InputBuffer => _inputBuffer;

    private void MaintainBuffer()
    {
        // 倒序遍历安全删除：仅剔除过期时间 (> _bufferValidTime) 的指令
        for (int i = _inputBuffer.Count - 1; i >= 0; i--)
        {
            if (Time.time - _inputBuffer[i].Timestamp > _bufferValidTime)
            {
                _inputBuffer.RemoveAt(i);
            }
        }
    }

    // ? 新增：最核心的清空接口。由 ActionStateManager 在成功切动作时调用！
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
}