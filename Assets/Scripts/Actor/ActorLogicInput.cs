using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;
using System.Linq;
using System;
using NodeCanvas.Tasks.Conditions;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;
    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;

    private void Start()
    {
        //AddStandingHandler();
    }

    protected virtual void LateUpdate()
    {
        UpdateActionCommands();
    }

    private void OnDestroy()
    {
        _transitionEventManager.ClearAllSubscriptions();
    }

    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void InputLook(Vector2 LookInput)
    {
    }

    #region Input处理

    private List<CommandStateHandler> handlers_Standing = new ();
    private List<CommandStateHandler> handlers_ShortDated = new();

    public void GetInputData(InputData inputData)
    {
        List<CommandStateHandler> actionCommands = handlers_Standing.Concat(handlers_ShortDated).ToList();
        if (actionCommands.Count == 0) return;

        List<CommandStateHandler> commandsReady = new List<CommandStateHandler>();
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

    void UpdateActionCommands()
    {
        List<CommandStateHandler> actionCommands = new();
        if (actionCommands.Count == 0) return;

        for (int i = 0; i < actionCommands.Count; i++)
            actionCommands[i].Update();
    }

    #endregion

    #region Action事件

    private GenericEventManager<ActionAsset> _transitionEventManager = new GenericEventManager<ActionAsset>();

    public void RegisterForTransitionEvent(object registrant, Action<ActionAsset> callback)
    {
        _transitionEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromTransitionEvent(object registrant)
    {
        _transitionEventManager.Unsubscribe(registrant);
    }

    void RaiseTransitionEvent(ActionAsset actionTimelineAsset)
    {
        _transitionEventManager.Publish(actionTimelineAsset);
    }

    #endregion

}
