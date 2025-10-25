using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;
using System.Linq;
using System;
using UnityEngine.Playables;
using NodeCanvas.Framework;


public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private List<InputBuffer> inputBuffers = new ();
    public List<InputBuffer> InputBuffers => inputBuffers;

    private void Update()
    {
        for (int i = 0; i < inputBuffers.Count; i++)
        {
            if (Time.time - inputBuffers[i].time >= 0.6f)
                inputBuffers.RemoveAt(i);
        }
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
        inputBuffers.Add(new InputBuffer(inputData, Time.time));
    }

    public void CleanInputBuffers()
    {
        inputBuffers.Clear();
    }

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

    void RaiseInputEvent(InputData inputData)
    {
        _inputEventManager.Publish(inputData);
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
