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

    private List<InputBuffer> inputBuffers = new ();
    public List<InputBuffer> InputBuffers => inputBuffers;


    private void LateUpdate()
    {
        handlersReady.AddRange(BufferCheck());
        if (handlersReady.Count > 0) 
        {
            SendBackInputResult(handlersReady);
            Clear();
        }

        UpdateInput();
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        InputCheck(inputData);
        UpdateBuffer(new InputBuffer(inputData, Time.time));
    }

    #region Input检查

    private List<InputCheckHandler> inputCheckHandlers = new List<InputCheckHandler>();
    private List<InputCheckHandler> bufferCheckHandlers = new List<InputCheckHandler>();
    private List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();

    List<InputCheckHandler> BufferCheck()
    {
        // 防止在遍历过程中集合被修改 创建一份副本进行遍历
        var handlersCopy = new List<InputCheckHandler>(bufferCheckHandlers);
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();

        if(handlersCopy.Count == 0 || inputBuffers.Count == 0) return handlersReady;

        foreach (var handler in bufferCheckHandlers)
        {
            var buffersCopy = new List<InputBuffer>(InputBuffers);
            float primaryTime = buffersCopy[0].time;
            foreach (var buffer in buffersCopy)
            {
                if (handler.IsLast)
                    break;

                float intervalTime = buffer.time - primaryTime;
                if (handler.Matches(buffer.inputData) && intervalTime <= handler.waitTime)
                    handler.Advance();
                primaryTime = buffer.time;
            }

            if (handler.IsLast)
                handlersReady.Add(handler);
        }

        return handlersReady;
    }

    void InputCheck(InputData inputData)
    {
        var handlersCopy = new List<InputCheckHandler>(inputCheckHandlers);

        if(handlersCopy.Count == 0) return;

        foreach (var handler in handlersCopy)
        {
            if (!handler.Matches(inputData)) continue;
            if (handler.IsLast)
            {
                if (!handlersReady.Contains(handler))
                    handlersReady.Add(handler);
            }
            else
                handler.Advance();
        }
    }

    void SendBackInputResult(List<InputCheckHandler> handlersReady)
    {
        var handlerToUse = handlersReady.OrderByDescending(h => (int)h.priority).FirstOrDefault();
        RaiseInputEventSingle(handlerToUse, true);

        foreach (var handler in inputCheckHandlers)
        {
            if (handler != handlerToUse)
                RaiseInputEventSingle(handler, false);
        }

        handlersReady.Clear();
    }

    void UpdateInput()
    {
        foreach (var handler in inputCheckHandlers)
            handler.Update(Time.deltaTime);
    }

    void UpdateBuffer(InputBuffer buffer)
    {
        if(inputBuffers.Count >= 6)
        {
            float currentTime = Time.time;
            for (int i = 0; i < inputBuffers.Count; i++)
            {
                if (currentTime - inputBuffers[i].time >= 0.6f)
                    inputBuffers.RemoveAt(i);
            }
        }
        
        if(inputBuffers.Count < 6) 
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

    void ClearHandler()
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

    public void Clear()
    {
        _inputEventManager.ClearAllSubscriptions();
        ClearHandler();
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

public struct InputBuffer
{
    public InputData inputData;
    public float time;

    public InputBuffer(InputData data, float time)
    {
        inputData = data;
        this.time = time;
    }
}
