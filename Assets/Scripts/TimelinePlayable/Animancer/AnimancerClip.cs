using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Animancer;

[System.Serializable]
public class AnimancerClip : PlayableAsset, ITimelineClipAsset
{
    public TransitionAsset transitionAsset;

    public ClipCaps clipCaps => ClipCaps.SpeedMultiplier;

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
                // 【修正】使用 AnimationSet 属性
                if (directionalTransition.AnimationSet != null &&
                    directionalTransition.AnimationSet.GetClip(0) != null)
                {
                    return directionalTransition.AnimationSet.GetClip(0).length;
                }
            }

            return base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.transitionAsset = this.transitionAsset;

        return playable;
    }
}