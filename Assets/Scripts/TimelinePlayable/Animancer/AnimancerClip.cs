using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Animancer;

public class AnimancerClip : PlayableAsset,ITimelineClipAsset
{
    public TransitionAsset transitionAsset = null;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override double duration
    {
        get
        {
            if (transitionAsset == null) return base.duration;

            if (transitionAsset.Transition is ClipTransition clipTransition)
            {
                if (clipTransition.Clip != null)
                    return clipTransition.Clip.length;
            }
            else if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
            {
                if (directionalTransition.AnimationSet != null &&
                    directionalTransition.AnimationSet.GetClip(0) != null)
                {
                    // 使用第一个方向的长度作为基准
                    return directionalTransition.AnimationSet.GetClip(0).length;
                }
            }

            return base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerBehaviour>.Create(graph);
        AnimancerBehaviour clip = playable.GetBehaviour();

        clip.transitionAsset = transitionAsset;

        return playable;
    }
}
