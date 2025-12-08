using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Actor))]
public class ActorLogicInput : MonoBehaviour
{
    [Header("组件引用")]
    private Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private void Awake()
    {
        actor = GetComponent<Actor>();
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;

        Vector3 moveDir = Vector3.zero;
        if(actor.cameraControl != null)
            moveDir = actor.cameraControl.CalculateDirection(moveInput);

        if (actor.movement != null)
            actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {
        RaiseInputEvent(inputData);
        UpdateBuffer(inputData);
    }

    #region Input处理

    private List<InputData> inputBuffers = new List<InputData>();
    private GenericEventManager<InputData> inputEventManager = new GenericEventManager<InputData>();

    public void RegisterForInputEvent(object registrant, Action<InputData> callback)
    {
        inputEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromInputEvent(object registrant)
    {
        inputEventManager.Unsubscribe(registrant);
    }

    void RaiseInputEvent(InputData inputData)
    {
        inputEventManager.Publish(inputData);
    }

    void UpdateBuffer(InputData buffer)
    {
        if (inputBuffers.Count == 6)
            inputBuffers.RemoveAt(0);

        inputBuffers.Add(buffer);
    }

    #endregion

}
