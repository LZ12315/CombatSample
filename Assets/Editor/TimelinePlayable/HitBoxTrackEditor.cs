using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionHitBoxTrack))]
public class HitBoxTrackEditor : ActionTrackEditorBase { }


[CustomTimelineEditor(typeof(ActionHitBoxAsset))]
public class HitBoxClipEditor : ActionClipEditorBase
{
    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip.asset == null) return;

        AdjustClipStartTime(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
}
