using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    // 为了获取InputState而专门设置
    private PlayerInputController inputController;
    public PlayerInputController InputController { get => inputController; set => inputController = value; }
    // 之后需要删除

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

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
