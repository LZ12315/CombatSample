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
        UpdateInputCommand(Time.deltaTime);
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
        if (commands.Count == 0) return;

        for (int i = 0; i < commands.Count; i++)
        {
            commands[i].GetInputData(inputData);

            if (commands[i].IsCommandComplished())
                InvokeTransitionEvent(commands[i].);
        }
    }

    #region InputÊÂ¼þ

    private EventInfo<ActionTimelineAsset> actionTransitionEvent = new();
    private List<InputCommand> commands = new ();

    public void AddInputCommand(InputCommand command)
    {
        if(commands.Contains(command))
            commands.Add(command);
    }

    void UpdateInputCommand(double deltaTime)
    {
        if(commands.Count == 0) return;

        for (int i = 0; i < commands.Count; i++)
            commands[i].CommandUpdate(deltaTime);
    }

    public void RemoveInputCommand(InputCommand command)
    {
        if (commands.Contains(command))
            commands.Remove(command);
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
