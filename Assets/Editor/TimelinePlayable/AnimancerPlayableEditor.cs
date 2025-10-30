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

        Texture2D trackIcon = Resources.Load<Texture2D>("ArtAssets/Textures/AnimancerIcon");
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
    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        //创建时设置Clip时长为动画时长
        var asset = clip.asset as AnimancerClip;
        if (asset == null || asset.transitionAsset == null) return;

        ClipTransition clipTransition = asset.transitionAsset.Transition as ClipTransition;
        if (clipTransition.Clip != null)
        {
            clip.displayName = clipTransition.Clip.name;
            clip.duration = clipTransition.Clip.length;
        }
    }

    protected override void OnClipChange(TimelineClip clip)
    {
        //更新Clip名称为动画名称
        var asset = clip.asset as AnimancerClip;
        if (asset == null || asset.transitionAsset == null) return;

        ClipTransition clipTransition = asset.transitionAsset.Transition as ClipTransition;
        if (clipTransition.Clip != null)
            clip.displayName = clipTransition.Clip.name;
    }

}
