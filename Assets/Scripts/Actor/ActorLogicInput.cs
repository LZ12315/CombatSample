using System;
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


    //public int frameCount = 0;
    //private void Update()
    //{
    //    frameCount++;
    //    if (inputBuffers.Count == 0)
    //        Debug.Log(frameCount + "   " + inputBuffers.Count);
    //    else
    //        Debug.LogWarning(frameCount + "   " + inputBuffers.Count);
    //}

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        //Debug.LogWarning(frameCount + "Get Input");
        RaiseInputEvent(inputData);
        UpdateBuffer(new InputBuffer(inputData, Time.time));
    }

    #region Input»º³å

    void UpdateBuffer(InputBuffer buffer)
    {
        if (inputBuffers.Count == 6)
            inputBuffers.RemoveAt(0);

        inputBuffers.Add(buffer);
    }


    #endregion

    #region InputÊÂ¼þ

    private GenericEventManager<InputData> _inputEventManager = new GenericEventManager<InputData>();

    public void RegisterForInputEvent(object registrant, Action<InputData> callback)
    {
        _inputEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromInputEvent(object registrant)
    {
        _inputEventManager.Unsubscribe(registrant);
    }

    void Clear()
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
