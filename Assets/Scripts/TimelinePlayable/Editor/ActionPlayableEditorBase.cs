using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using Animancer;
using UnityEngine.Timeline;


[CustomTimelineEditor(typeof(ActionBehaviourBase))]
public class ActionClipEditorBase : ClipEditor
{

    protected virtual void OnClipCreate(TimelineClip clip, TrackAsset track) { }

    protected virtual void OnClipChange(TimelineClip clip){ }

    protected virtual void AdjustClipStartTime(TimelineClip clip)
    {
        //防止Clip起始时间过于靠近0
        //否则会导致逻辑错误: OnBehavior的方法被错误触发
        if (clip.start < 0.0001f)
            clip.start = 0.0001f;
    }

    protected virtual void SetClipDurationOnStart(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        // 如果是Copy其他Clip 则不改变长度
        if(clonedFrom != null) return;

        // 获取Timeline资源
        var timelineAsset = track.timelineAsset;
        if (timelineAsset == null) return;

        // 获取Timeline总时长
        double timelineDuration = timelineAsset.duration;

        // 设置Clip时长为Timeline总时长
        if(timelineDuration < 0.01f)
            clip.duration = 0.01f;
        else
            clip.duration = timelineDuration;
    }

    #region 方法继承

    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        OnClipCreate(clip, track);
        SetClipDurationOnStart(clip, track, clonedFrom);
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip == null ||clip.asset == null) return;

        OnClipChange(clip);
        AdjustClipStartTime(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    #endregion

}
