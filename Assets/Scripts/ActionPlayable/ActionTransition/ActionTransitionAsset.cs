using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionTransitionAsset : PlayableAsset
{
    public bool active = true;
    public Enums.ActTransType actTransType;
    public Enums.InputType inputType;
    public ActionTimelineAsset next;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.actTransType = actTransType;
        clip.active = active;
        clip.inputType = inputType;
        clip.next = next;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public bool active;
    public Enums.ActTransType actTransType;
    public Enums.InputType inputType;
    public ActionTimelineAsset next;

    bool eventWaitForInvoke = false;

    protected override void OnClipPlay()
    {
        base.OnClipPlay();
        if(!active || actor == null) return;

        actor.logicInput.AddEventListener(inputType, OnInputEventTriggered);
    }

    protected virtual void OnInputEventTriggered()
    {
        if(!active) return;

        if(actTransType == Enums.ActTransType.Immediate)
            actor.actionPlayerDirector.PlayAction(next);
        else
            eventWaitForInvoke = true;
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();
        if (!active || actor == null) return;

        if (eventWaitForInvoke && actTransType == Enums.ActTransType.TransitionEnd)
        {
            actor.actionPlayerDirector.PlayAction(next);
            eventWaitForInvoke = false;
        }

        actor.logicInput.RemoveEventListener(inputType, OnInputEventTriggered);
    }

    protected override void OnClipFinish()
    {
        base.OnClipFinish();
        if (!active || actor == null) return;

        if (eventWaitForInvoke && actTransType == Enums.ActTransType.ActionEnd)
        {
            eventWaitForInvoke = false;
            actor.actionPlayerDirector.PlayAction(next);
        }

        actor.logicInput.RemoveEventListener(inputType, OnInputEventTriggered);
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