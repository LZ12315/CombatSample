using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.9f, 0.3f, 0.1f)] // 橙色轨道
[TrackClipType(typeof(EffectControlClip))]
[TrackBindingType(typeof(Transform))]
public class EffectControlTrack : TrackAsset
{
    // 使用默认实现，不需要重写
    // Timeline会自动处理轨道和Clip的连接
}