using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionTransitionAsset : PlayableAsset
{
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();
        return playable;
    }

}

public class ActionTransitionClip : PlayableBehaviour
{
    public string displayName = string.Empty;
    public TimelineAsset action;

    Actor actor;
    bool isPlaying = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if(isPlaying) return;
        isPlaying = true;

        actor = info.output.GetUserData() as Actor;
        if(actor == null ) return;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if(!isPlaying) return;
        isPlaying = false;
    }

}