using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;
using System.Linq;
using System;
using UnityEngine.Playables;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private InputData lastInput;
    public InputData LastInput => lastInput; 

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        lastInput = inputData;
        RaiseInputEvent(inputData);
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
