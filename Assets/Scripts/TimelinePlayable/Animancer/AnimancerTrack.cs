using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.8f, 0.4f)]
[TrackClipType(typeof(AnimancerClip))]
[TrackClipType(typeof(ContinuousAnimancerClip))]
public class AnimancerTrack : TrackAsset
{
}