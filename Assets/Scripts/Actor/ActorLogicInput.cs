using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private InputData lastInput;
    public InputData LastInput => lastInput;

    private List<InputBuffer> inputBuffers = new();
    public List<InputBuffer> InputBuffers => inputBuffers;
    int a = 0;

    private void Update()
    {
        a++;
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();
        handlersReady.AddRange(BufferCheck());
        handlersReady.AddRange(InputCheck(lastInput));

        if (handlersReady.Count > 0)
        {
            SendBackInputResult(handlersReady);
            inputBuffers.Clear();
            lastInput = null;
            ClearAll();
        }
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        lastInput = inputData;
        UpdateBuffer(new InputBuffer(inputData, Time.time));
    }

    #region Input检查

    private List<InputCheckHandler> inputCheckHandlers = new List<InputCheckHandler>();
    private List<InputCheckHandler> bufferCheckHandlers = new List<InputCheckHandler>();

    List<InputCheckHandler> BufferCheck()
    {
        // 防止在遍历过程中集合被修改 创建一份副本进行遍历
        var handlersCopy = new List<InputCheckHandler>(bufferCheckHandlers);
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();

        if (handlersCopy.Count == 0 || inputBuffers.Count == 0) return handlersReady;

        foreach (var handler in bufferCheckHandlers)
        {
            var buffersCopy = new List<InputBuffer>(InputBuffers);
            float primaryTime = 0;
            foreach (var buffer in buffersCopy)
            {
                if (handler.IsLast)
                    break;

                if (handler.Matches(buffer.inputData))
                {
                    float intervalTime = buffer.time - primaryTime;
                    if(primaryTime == 0 || intervalTime <= handler.waitTime)
                    {
                        handler.Advance();
                        primaryTime = buffer.time;
                    }
                }
            }

            if (handler.IsLast)
                handlersReady.Add(handler);
            else
                handler.Reset();
        }

        return handlersReady;
    }

    List<InputCheckHandler> InputCheck(InputData inputData)
    {
        var handlersCopy = new List<InputCheckHandler>(inputCheckHandlers);
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();

        if (handlersCopy.Count == 0 || inputData == null) return handlersReady;
        Debug.Log(lastInput);
        foreach (var handler in handlersCopy)
        {
            if (handler.Matches(inputData))
            {
                if (handler.IsLast)
                {
                    if (!handlersReady.Contains(handler))
                        handlersReady.Add(handler);
                }
                else
                    handler.Advance();
            }
            else
                handler.Update(Time.deltaTime);
        }

        return handlersReady;
    }

    void SendBackInputResult(List<InputCheckHandler> handlersReady)
    {
        if (handlersReady.Count == 0) return;

        var handlerToUse = handlersReady.OrderByDescending(h => (int)h.priority).FirstOrDefault();
        RaiseInputEventSingle(handlerToUse, true);

        foreach (var handler in inputCheckHandlers)
        {
            if (handler != handlerToUse)
                RaiseInputEventSingle(handler, false);
        }
    }

    void UpdateBuffer(InputBuffer buffer)
    {
        if (inputBuffers.Count == 6)
            inputBuffers.RemoveAt(0);

        inputBuffers.Add(buffer);
    }

    void AddHandler(InputCheckHandler handler)
    {
        if (handler.useBuffer)
        {
            if (!bufferCheckHandlers.Contains(handler))
                bufferCheckHandlers.Add(handler);
        }
        else
        {
            if (!inputCheckHandlers.Contains(handler))
                inputCheckHandlers.Add(handler);
        }
    }

    void RemoveHandler(InputCheckHandler handler)
    {
        if (inputCheckHandlers.Contains(handler))
            inputCheckHandlers.Remove(handler);

        if (bufferCheckHandlers.Contains(handler))
            bufferCheckHandlers.Remove(handler);
    }

    void ClearAllHandler()
    {
        inputCheckHandlers.Clear();
        bufferCheckHandlers.Clear();
    }

    #endregion

    #region Input事件

    private GenericEventManager<bool> _inputEventManager = new GenericEventManager<bool>();

    public void RegisterForInputEvent(InputCheckHandler registrant, Action<bool> callback)
    {
        _inputEventManager.Subscribe(registrant, callback);
        AddHandler(registrant);
    }

    public void UnregisterFromInputEvent(InputCheckHandler registrant)
    {
        _inputEventManager.Unsubscribe(registrant);
        RemoveHandler(registrant);
    }

    void ClearAll()
    {
        _inputEventManager.ClearAllSubscriptions();
        ClearAllHandler();
    }

    void RaiseInputEvent(bool eventData)
    {
        _inputEventManager.Publish(eventData);
    }

    void RaiseInputEventSingle(InputCheckHandler registrant, bool eventData)
    {
        _inputEventManager.PublishSingle(registrant, eventData);
    }

    #endregion

}


public class InputBuffer
{
    public InputData inputData;
    public float time;

    public InputBuffer(InputData data, float time)
    {
        inputData = data;
        this.time = time;
    }
}
