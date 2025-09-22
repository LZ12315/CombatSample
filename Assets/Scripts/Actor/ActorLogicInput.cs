using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    [SerializeField] private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    protected virtual void LateUpdate()
    {
        UpdateActionCommand();
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

    #region Input¥¶¿Ì

    public List<ActionCommand> actionCommands = new ();

    public void GetInputData(InputData inputData)
    {
        if (actionCommands.Count == 0) return;

        List<ActionCommand> commandsReady = new List<ActionCommand>();
        for (int i = 0; i < actionCommands.Count; i++)
        {
            if (!actionCommands[i].Matches(inputData)) continue;

            if (actionCommands[i].IsLast)
            {
                if (!commandsReady.Contains(actionCommands[i]))
                    commandsReady.Add(actionCommands[i]);
                actionCommands[i].checkIndex = 0;
                actionCommands[i].waitCounter = 0;
            }
            else
            {
                actionCommands[i].checkIndex++;
                actionCommands[i].waitCounter = actionCommands[i].waitTime;
            }
        }

        if (commandsReady.Count == 0) return;

        commandsReady.Sort((a, b) => b.priority.CompareTo(a.priority));
        InvokeTransitionEvent(commandsReady[0].actionToPlay);
    }

    public void AddActionCommand(ActionCommand actionCommand)
    {
        actionCommands.Add(actionCommand);
    }

    void UpdateActionCommand()
    {
        if (actionCommands.Count == 0) return;

        for (int i = 0; i < actionCommands.Count; i++)
            actionCommands[i].Update();
    }

    public void RemoveActionCommand(ActionCommand command)
    {
        if (actionCommands.Contains(command))
            actionCommands.Remove(command);
    }

    #endregion

    #region Action¥´µ›

    private EventInfo<ActionTimelineAsset> actionTransitionEvent = new();

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
