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

    private Vector2 _moveInput = Vector2.zero;
    public Vector2 MoveInput { get => _moveInput; }

    private Vector2 _lookInput = Vector2.zero;
    public Vector2 LookInput { get => _lookInput;}

    private void Awake()
    {
        actor = GetComponent<Actor>();
    }

    private void Update()
    {
        Vector3 faceDir = actor.cameraControl.CalculateWorldDirection(_moveInput);
        actor.movement.UpdateRotation(faceDir);
    }

    public void InputMove(Vector2 moveInput)
    {
        _moveInput = moveInput;
    }

    public void InputLook(Vector2 lookInput)
    {
        _lookInput = lookInput;
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
