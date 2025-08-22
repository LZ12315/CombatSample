using Animancer;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class AnimancerAsset : PlayableAsset
{
    public ClipTransition clipTransition = null;

    public override double duration
    {
        get
        {
            if (clipTransition != null && clipTransition.Clip != null)
            {
                // 使用动画实际时长
                return clipTransition.Clip.length;
            }
            return base.duration; // 默认值
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerClip>.Create(graph);
        AnimancerClip clip = playable.GetBehaviour();

        clip.clipTransition = clipTransition;

        return playable;
    }
}

public class AnimancerClip : ActionClipBase
{
    public ClipTransition clipTransition = null;

    protected override void OnClipPlay()
    {
        base.OnClipPlay();

        if (actor == null) return;

        if(clipTransition.Clip != null)
            actor.animancer.Play(clipTransition);
    }

}
