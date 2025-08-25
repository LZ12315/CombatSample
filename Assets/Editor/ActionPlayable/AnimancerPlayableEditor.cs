using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Timeline;
using Animancer;

[CustomTimelineEditor(typeof(AnimancerTrack))]
public class AnimancerPlayableEditor : TrackEditor
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
        base.OnCreate(track, copiedFrom);
        track.name = "Animancer Track";
    }
}

[CustomTimelineEditor(typeof(AnimancerAsset))]
class AnimancerClipEditor : ClipEditor
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        var options = base.GetClipOptions(clip);
        // 设置Clip颜色
        options.highlightColor = new Color(0.2f, 0.8f, 0.4f);

        return options;
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        AdjustClipStartTime(clip);
        UpdateAnimancerInfo(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    //防止Clip起始时间正好为0
    //否则会导致逻辑错误: OnBehavior等方法被错误触发
    //因此自动向右偏移0.0001f
    void AdjustClipStartTime(TimelineClip clip)
    {
        if (clip.start == 0f)
            clip.start = 0.0001f;
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
