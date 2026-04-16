using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Animancer;

public enum AnimancerParameterMode
{
    None = 0,
    ContextDirection2D = 1,
    ContextMagnitude = 2,
    SerializedFallback = 3,
}

[System.Serializable]
public class AnimancerClip : PlayableAsset, ITimelineClipAsset
{
    public TransitionAsset transitionAsset;
    public AnimancerParameterMode parameterMode = AnimancerParameterMode.None;
    public Vector2 fallbackVector2 = Vector2.zero;
    public float fallbackFloat = 0f;

    public ClipCaps clipCaps => ClipCaps.SpeedMultiplier;

    public override double duration
    {
        get
        {
            double resolvedDuration = AnimancerTransitionUtility.GetDuration(transitionAsset);
            return resolvedDuration > 0d ? resolvedDuration : base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.transitionAsset = this.transitionAsset;
        behaviour.parameterMode = parameterMode;
        behaviour.fallbackVector2 = fallbackVector2;
        behaviour.fallbackFloat = fallbackFloat;

        return playable;
    }
}