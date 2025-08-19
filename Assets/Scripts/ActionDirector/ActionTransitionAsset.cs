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

public class ActionTransitionClip : PlayableBehaviour
{
    public bool active;
    public Enums.ActTransType actTransType;
    public Enums.InputType inputType;
    public ActionTimelineAsset next;

    Actor actor = null;
    bool isPlaying = false;
    bool eventWaitForInvoke = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if(isPlaying) return;
        isPlaying = true;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        actor = director.GetComponent<Actor>();
        if(actor == null ) return;

        if(active)
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

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if(!isPlaying) return;
        isPlaying = false;

        if(eventWaitForInvoke && actTransType == Enums.ActTransType.TransitionEnd)
        {
            actor.actionPlayerDirector.PlayAction(next);
            eventWaitForInvoke = false;
        }

        if (actor != null)
            actor.logicInput.RemoveEventListener(inputType, OnInputEventTriggered);
    }

    public override void OnGraphStop(Playable playable)
    {
        if (eventWaitForInvoke && actTransType == Enums.ActTransType.ActionEnd)
        {
            eventWaitForInvoke = false;
            actor.actionPlayerDirector.PlayAction(next);
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