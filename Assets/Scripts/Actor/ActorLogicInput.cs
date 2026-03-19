using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Actor))]
public class ActorLogicInput : MonoBehaviour
{
    [Header("???????")]
    [SerializeField] private Actor actor;

    [Header("??????????")]
    [SerializeField, Tooltip("??????????ùù????????")]
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
    /// ?????????? InputSystem??????????????
    /// </summary>
    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(inputData);
        
        // ?????????????? (? ?????????? IsConsumed)
        _inputBuffer.Add(new BufferedInput 
        { 
            Data = inputData, 
            Timestamp = Time.time
        });
    }

    #region === ?????? ===

    // === ???????????????????? ===
    public class BufferedInput
    {
        public InputData Data;
        public float Timestamp;
    }

    private List<BufferedInput> _inputBuffer = new List<BufferedInput>();

    // ? ??????????????????????????? Condition ????????????????
    public IReadOnlyList<BufferedInput> InputBuffer => _inputBuffer;

    private void MaintainBuffer()
    {
        // ??????????????????????????? (> _bufferValidTime) ?????
        for (int i = _inputBuffer.Count - 1; i >= 0; i--)
        {
            if (Time.time - _inputBuffer[i].Timestamp > _bufferValidTime)
            {
                _inputBuffer.RemoveAt(i);
            }
        }
    }

    // ? ???????????????????? ActionStateManager ?????ùù?????????
    public void ClearBuffer()
    {
        _inputBuffer.Clear();
    }

    #endregion

    #region === ??????? ===

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