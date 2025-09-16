using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private void Update()
    {
        UpdateCommand(Time.deltaTime);
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateFaceDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void InputLook(Vector2 LookInput)
    {
        actor.cameraControl.HandleCameraRotation(LookInput);
    }

    public void GetInputData(InputData inputData)
    {
        if (skillCommands.Count == 0) return;

        for (int i = 0; i < skillCommands.Count; i++)
        {

        }
    }

    #region InputÊÂ¼þ

    private EventInfo<ActionTimelineAsset> actionTransitionEvent = new();
    private List<ActionCommand> skillCommands = new ();

    public void AddCommand(InputSequence command)
    {

    }

    void UpdateCommand(double deltaTime)
    {

    }

    public void RemoveCommand(InputSequence command)
    {

    }

    public void AddTransitionEvent(UnityAction<ActionTimelineAsset> action)
    {
        actionTransitionEvent.AddAction(action);
    }

    void InvokeTransitionEvent(ActionTimelineAsset parameter)
    {
        actionTransitionEvent.Invoke(parameter);
    }

    public void RemoveTransitionEvent(UnityAction<ActionTimelineAsset> action)
    {
        actionTransitionEvent.RemoveAction(action);
    }

    #endregion

}
