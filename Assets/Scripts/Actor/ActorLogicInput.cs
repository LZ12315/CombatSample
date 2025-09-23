using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;
using System.Linq;
using System;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    protected virtual void LateUpdate()
    {
        UpdateActionCommand();
    }

    private void OnDestroy()
    {
        _transitionEventManager.ClearAllSubscriptions();
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

    #region Input处理

    private List<ActionCommand> actionCommands_Standing = new ();
    private List<ActionCommand> actionCommands_ShortDated = new();

    public void GetInputData(InputData inputData)
    {
        List<ActionCommand> actionCommands = actionCommands_Standing.Concat(actionCommands_ShortDated).ToList();
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
        RaiseTransitionEvent(commandsReady[0].actionToPlay);
    }

    public void AddStandingCommand(ActionTimelineAsset actionToPlay, InputSequence sequence, Enums.ActionPriority priority)
    {
        ActionCommand actionCommand = new ActionCommand(actionToPlay, sequence, priority);
        if (actionCommand == null) return;

        actionCommands_Standing.Add(actionCommand);
    }

    public void AddShortdatedCommand(ActionTimelineAsset actionToPlay, InputSequence sequence, Enums.ActionPriority priority)
    {
        ActionCommand actionCommand = new ActionCommand(actionToPlay, sequence, priority);
        if (actionCommand == null) return;
        actionCommands_ShortDated.Add(actionCommand);
    }

    void UpdateActionCommand()
    {
        List<ActionCommand> actionCommands = actionCommands_Standing.Concat(actionCommands_ShortDated).ToList();
        if (actionCommands.Count == 0) return;

        for (int i = 0; i < actionCommands.Count; i++)
            actionCommands[i].Update();
    }

    public void ClearShortdatedCommand()
    {
        actionCommands_ShortDated.Clear();
    }

    #endregion

    #region Action事件

    private GenericEventManager<ActionTimelineAsset> _transitionEventManager = new GenericEventManager<ActionTimelineAsset>();

    public void RegisterForTransitionEvent(object registrant, Action<ActionTimelineAsset> callback)
    {
        _transitionEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromTransitionEvent(object registrant)
    {
        _transitionEventManager.Unsubscribe(registrant);
    }

    void RaiseTransitionEvent(ActionTimelineAsset actionTimelineAsset)
    {
        _transitionEventManager.Publish(actionTimelineAsset);
    }

    #endregion

}
