using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Timeline;
using Animancer;

[CustomTimelineEditor(typeof(AnimancerTrack))]
public class AnimancerPlayableEditor : ActionTrackEditorBase
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

[CustomTimelineEditor(typeof(AnimancerAsset))]
class AnimancerClipEditor : ActionClipEditorBase
{
    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        UpdateAnimancerInfo(clip);
    }

    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        var options = base.GetClipOptions(clip);
        options.highlightColor = new Color(0.2f, 0.8f, 0.4f); // 设置Clip颜色

        return options;
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        AdjustClipStartTime(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    //自动更新Clip时长和名称
    void UpdateAnimancerInfo(TimelineClip clip)
    {
        var asset = clip.asset as AnimancerAsset;
        if (asset == null || asset.transitionAsset == null) return;

        ClipTransition clipTransition = asset.transitionAsset.Transition as ClipTransition;
        if (clipTransition.Clip != null)
        {
            clip.displayName = clipTransition.Clip.name;
            clip.duration = clipTransition.Clip.length;
        }
    }

}
