using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionTransitionAsset : PlayableAsset
{
    public bool active = true;
    public Enums.InputType inputType;
    public ActionTimelineAsset next;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.active = active;
        clip.inputType = inputType;
        clip.next = next;

        return playable;
    }

}

public class ActionTransitionClip : PlayableBehaviour
{
    public bool active;
    public Enums.InputType inputType;
    public ActionTimelineAsset next;
    Actor actor = null;
    bool isPlaying = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if(isPlaying) return;
        isPlaying = true;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        actor = director.GetComponent<Actor>();
        if(actor == null ) return;

        if(active)
            actor.logicInput.AddEventListener(inputType, OnEventTriggered);
    }

    protected virtual void OnEventTriggered()
    {
        if(!active) return;
        actor.actionPlayerDirector.PlayAction(next);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if(!isPlaying) return;
        isPlaying = false;

        if(actor != null)
            actor.logicInput.RemoveEventListener(inputType, OnEventTriggered);
    }

}