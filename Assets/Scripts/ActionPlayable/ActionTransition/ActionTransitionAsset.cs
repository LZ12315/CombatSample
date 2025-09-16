using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionTransitionAsset : PlayableAsset
{
    public bool active = true;
    public Enums.ActTransType transitionType;
    public ActionTimelineAsset nextAction;
    public InputSequence inputSequence = new ();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.active = active;
        clip.transitionType = transitionType;
        clip.nextAction = nextAction;
        clip.inputSequence = inputSequence;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public bool active = true;
    public Enums.ActTransType transitionType;
    public ActionTimelineAsset nextAction;
    public InputSequence inputSequence;

    bool actionWaitToPlay = false;
    ActionTimelineAsset actionToPlay;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);
        if(!active || actor == null) return;

        actor.logicInput.AddTransitionEvent(o => PlayNextAction(o));
    }


    protected override void OnClipPause()
    {
        base.OnClipPause();
        if (!active || actor == null) return;
    }

    protected override void OnClipFinish()
    {
        base.OnClipFinish();
        if (!active || actor == null) return;

        if (actionWaitToPlay && transitionType == Enums.ActTransType.TransitionEnd)
        {
            actionWaitToPlay = false;

            if(actionToPlay != null)
                actor.actionPlayerDirector.PlayAction(actionToPlay);
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        if (actionWaitToPlay && transitionType == Enums.ActTransType.ActionEnd)
        {
            actionWaitToPlay = false;

            if (actionToPlay != null)
                actor.actionPlayerDirector.PlayAction(actionToPlay);
        }
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

}

public static partial class Enums
{
    public enum ActTransType
    {
        Immediate,
        TransitionEnd,
        ActionEnd
    }
}