using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CombatSample.Consts;

public class ActionTransitionAsset : PlayableAsset
{
    [Header("Transition…Ë÷√")]
    public bool active = true;
    public Enums.ActTransType transitionType;

    [Header(" ‰»ÎºÏ≤È")]
    public List<ActionCommand> actionCommands = new ();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.active = active;
        clip.transitionType = transitionType;
        clip.actionCommands = actionCommands;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public bool active = true;
    public Enums.ActTransType transitionType;

    public List<ActionCommand> actionCommands;

    bool actionWaitToPlay = false;
    ActionTimelineAsset actionToPlay = null;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);
        if(!active || actor == null) return;

        foreach (var command in actionCommands)
            actor.logicInput.AddActionCommand(command);
        actor.logicInput.AddTransitionEvent(o => PlayNextAction(o));
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();
        CleanUp();
    }

    protected override void OnClipFinish(bool isNormal)
    {
        base.OnClipFinish(isNormal);
        if (!active || actor == null) return;
        Debug.Log(isNormal);
        if (isNormal)
        {
            if(actionWaitToPlay && transitionType == Enums.ActTransType.TransitionEnd)
            {
                actionWaitToPlay = false;

                if (actionToPlay != null)
                    actor.actionPlayerDirector.PlayAction(actionToPlay);
            }
        }
        CleanUp();
    }

    private void PlayNextAction(ActionTimelineAsset actionToPlay)
    {
        if(!active) return;
        actionWaitToPlay = true;
        this.actionToPlay = actionToPlay;

        if (actionWaitToPlay && transitionType == Enums.ActTransType.Immediate)
        {
            actionWaitToPlay = false;

            if (actionToPlay != null)
                actor.actionPlayerDirector.PlayAction(actionToPlay);
        }
    }

    void CleanUp()
    {
        foreach (var command in actionCommands)
            actor.logicInput.RemoveActionCommand(command);

        actor.logicInput.RemoveTransitionEvent(o => PlayNextAction(o));
        actionWaitToPlay = false;
        actionToPlay = null;
    }

}

public static partial class Enums
{
    public enum ActTransType
    {
        Immediate,
        TransitionEnd,
    }
}