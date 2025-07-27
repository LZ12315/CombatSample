using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

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
    PlayerController controller;
    bool isPlaying = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if(isPlaying) return;
        isPlaying = true;

        controller = info.output.GetUserData() as PlayerController;
        if(controller == null ) return;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if(!isPlaying) return;
        isPlaying = false;
    }

}