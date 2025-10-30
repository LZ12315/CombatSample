using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using Animancer;
using UnityEngine.Timeline;


[CustomTimelineEditor(typeof(ActionBehaviourBase))]
public class ActionClipEditorBase : ClipEditor
{
    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip.asset == null) return;

        OnClipChange(clip);
        AdjustClipStartTime(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    protected virtual void OnClipChange(TimelineClip clip){ }

    //防止Clip起始时间过于靠近0
    //否则会导致逻辑错误: OnBehavior的方法被错误触发
    protected virtual void AdjustClipStartTime(TimelineClip clip)
    {
        if (clip.start < 0.0001f)
            clip.start = 0.0001f;
    }

}
