using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private List<InputBuffer> inputBuffers = new();
    public List<InputBuffer> InputBuffers => inputBuffers;


    public int frameCount = 0;
    private void Update()
    {
        frameCount++;
        UpdateInputCheck(Time.deltaTime);
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(InputCheck(inputData));
        UpdateBuffer(new InputBuffer(inputData, Time.time));
    }

    #region Buffer事件

    void UpdateBuffer(InputBuffer buffer)
    {
        if (inputBuffers.Count == 6)
            inputBuffers.RemoveAt(0);

        inputBuffers.Add(buffer);
    }

    private GenericEventManager<bool> bufferEventManager = new GenericEventManager<bool>();
    private List<InputCheckHandler> bufferCheckHandlers = new List<InputCheckHandler>();

    public void RegisterForBufferEvent(InputCheckHandler registrant, Action<bool> callback)
    {
        bufferEventManager.Subscribe(registrant, callback);
        bufferCheckHandlers.Add(registrant);
    }

    public void UnregisterFromBufferEvent(InputCheckHandler registrant)
    {
        bufferEventManager.Unsubscribe(registrant);
        bufferCheckHandlers.Remove(registrant);
    }

    List<InputCheckHandler> BufferCheck(List<InputBuffer> inputBuffers)
    {
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();
        List<InputCheckHandler> handlers = new List<InputCheckHandler>(bufferCheckHandlers);
        if (handlers.Count == 0) return handlersReady;

        for (int i = 0; i < handlers.Count; i++)
        {
            float lastTime = 0;
            foreach (var buffer in inputBuffers)
            {
                if (handlers[i].Matches(buffer.inputData))
                {
                    float intervalTime = buffer.time - lastTime;
                    if (lastTime == 0 || intervalTime <= handlers[i].waitTime)
                    {
                        handlers[i].checkIndex++;
                        lastTime = buffer.time;
                    }
                }

                if (handlers[i].IsLast)
                {
                    handlersReady.Add(handlers[i]);
                    break;
                }
            }
        }

        return handlersReady;
    }

    void RaiseBufferEvent(List<InputCheckHandler> handlersReady)
    {
        if (handlersReady.Count == 0) return;

        var orderedHandlers = handlersReady.OrderByDescending(h => h.priority).ToList();

        for (int i = 0; i < orderedHandlers.Count; i++)
            bufferEventManager.PublishSingle(orderedHandlers[i], i == 0);

        inputBuffers.Clear();
    }

    public void BufferEventTrigger()
    {
        RaiseBufferEvent(BufferCheck(inputBuffers));
    }

    #endregion

    #region Input事件

    private GenericEventManager<bool> inputEventManager = new GenericEventManager<bool>();
    private List<InputCheckHandler> inputCheckHandlers = new List<InputCheckHandler>();

    public void RegisterForInputEvent(InputCheckHandler registrant, Action<bool> callback)
    {
        inputEventManager.Subscribe(registrant, callback);
        inputCheckHandlers.Add(registrant);
    }

    public void UnregisterFromInputEvent(InputCheckHandler registrant)
    {
        inputEventManager.Unsubscribe(registrant);
        inputCheckHandlers.Remove(registrant);
    }

    List<InputCheckHandler> InputCheck(InputData inputData)
    {
        List<InputCheckHandler> handlersReady = new List<InputCheckHandler>();
        if (inputCheckHandlers.Count == 0) return handlersReady;

        for (int i = 0; i < inputCheckHandlers.Count; i++)
        {
            if (!inputCheckHandlers[i].Matches(inputData)) continue;

            inputCheckHandlers[i].checkIndex++;
            if (inputCheckHandlers[i].IsLast)
            {
                if (!handlersReady.Contains(inputCheckHandlers[i]))
                    handlersReady.Add(inputCheckHandlers[i]);

                continue;
            }
            else
                inputCheckHandlers[i].waitCounter = inputCheckHandlers[i].waitTime;
        }

        return handlersReady;
    }

    void UpdateInputCheck(float deltaTime)
    {
        if (inputCheckHandlers.Count == 0) return;

        for (int i = 0; i < inputCheckHandlers.Count; i++)
        {
            if (inputCheckHandlers[i].waitCounter > 0)
            {
                inputCheckHandlers[i].waitCounter -= deltaTime;
                if(inputCheckHandlers[i].waitCounter <= 0)
                {
                    inputCheckHandlers[i].checkIndex = 0;
                    inputCheckHandlers[i].waitCounter = 0;
                }
            }
        }
    }


    void RaiseInputEvent(List<InputCheckHandler> handlersReady)
    {
        if(handlersReady.Count == 0) return;
        var orderedHandlers = handlersReady.OrderByDescending(h => h.priority).ToList();

        for (int i = 0;i < orderedHandlers.Count;i++)
            inputEventManager.PublishSingle(orderedHandlers[i], i == 0);
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
