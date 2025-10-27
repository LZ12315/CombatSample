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
    }

    public void InputMove(Vector2 moveInput)
    {
        //lastMoveInput = moveInput;
        //Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        //actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(inputData);
        UpdateBuffer(new InputBuffer(inputData, Time.time));

        StartCoroutine(AFuc());
    }

    #region Buffer事件

    void UpdateBuffer(InputBuffer buffer)
    {
        if (inputBuffers.Count == 6)
            inputBuffers.RemoveAt(0);

        inputBuffers.Add(buffer);
    }

    public void ClearBuffer()
    {
        Debug.Log("Clear");
        inputBuffers.Clear();
    }

    private GenericEventManager<bool> _actionEventManager = new GenericEventManager<bool>();
    private List<object> registrants = new List<object>();

    public void RegisterForActionEvent(object registrant, Action<bool> callback)
    {
        _actionEventManager.Subscribe(registrant, callback);
        registrants.Add(registrant);
    }

    public void UnregisterFromActionEvent(object registrant)
    {
        _actionEventManager.Unsubscribe(registrant);
        registrants.Remove(registrant);
    }

    void RaiseActionEventSingle(object registrant, bool eventData)
    {
        //_actionEventManager.PublishSingle(registrant, eventData);
        for (int i = 0; i < registrants.Count; i++)
        {
            if(i == 0)
                _actionEventManager.PublishSingle(registrant, true);
            else
                _actionEventManager.PublishSingle(registrant, false);
        }
    }

    private IEnumerator AFuc()
    {
        yield return null;


        for (int i = 0; i < registrants.Count; i++)
        {
            if(i == 0)
                RaiseActionEventSingle(registrants[i], true);
            else
                RaiseActionEventSingle(registrants[i], false);
        }
    }

    void ClearActionEvent()
    {
        _actionEventManager.ClearAllSubscriptions();
    }

    #endregion

    #region Input事件

    private GenericEventManager<InputData> _inputEventManager = new GenericEventManager<InputData>();
    private List<InputCheckHandler> _inputCheckHandlers = new List<InputCheckHandler>();

    public void RegisterForInputEvent(object registrant, Action<InputData> callback)
    {
        _inputEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromInputEvent(object registrant)
    {
        _inputEventManager.Unsubscribe(registrant);
    }

    void ClearInputEvent()
    {
        _inputEventManager.ClearAllSubscriptions();
    }

    void RaiseInputEvent(InputData eventData)
    {
        _inputEventManager.Publish(eventData);
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
