using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.8f, 0.4f)]
[TrackClipType(typeof(AnimancerClip))]
[TrackBindingType(typeof(Actor))] // 确保这里绑定的是 Actor
public class AnimancerTrack : TrackAsset
{
}