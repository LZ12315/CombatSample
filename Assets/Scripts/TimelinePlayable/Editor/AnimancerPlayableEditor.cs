using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Timeline;
using Animancer;

[CustomTimelineEditor(typeof(AnimancerTrack))]
public class AnimancerTrackEditor : TrackEditor
{
    public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
    {
        var options = base.GetTrackOptions(track, binding);

        Texture2D trackIcon = Resources.Load<Texture2D>("Textures/AnimancerIcon");
        if (trackIcon != null)
            options.icon = trackIcon;
        else
            Debug.LogWarning("Can Not Find Texture!");

        return options;
    }

    public override void OnCreate(TrackAsset track, TrackAsset copiedFrom)
    {
        track.name = "Animancer Track";
    }
}

[CustomTimelineEditor(typeof(AnimancerClip))]
class AnimancerClipEditor : ActionClipEditorBase
{

    protected override void OnClipChange(TimelineClip clip)
    {
        //更新Clip名称为动画名称
        var asset = clip.asset as AnimancerClip;
        if (asset == null || asset.transitionAsset == null) return;

        ClipTransition clipTransition = asset.transitionAsset.Transition as ClipTransition;
        if (clipTransition.Clip != null)
            clip.displayName = clipTransition.Clip.name;
    }

    protected override void SetClipDurationOnStart(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        // 不进行设置 Animancer会自动匹配动画时长
    }

}
