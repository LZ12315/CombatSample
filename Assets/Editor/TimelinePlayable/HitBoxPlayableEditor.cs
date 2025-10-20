using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionHitBoxTrack))]
public class HitBoxTrackEditor : ActionTrackEditorBase { }


[CustomTimelineEditor(typeof(ActionHitBoxAsset))]
public class HitBoxPlayableEditor : ActionClipEditorBase
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        var options = base.GetClipOptions(clip);
        options.highlightColor = new Color(1f, 0.5f, 0f);  // …Ë÷√Clip—’…´

        return options;
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip.asset == null) return;

        AdjustClipStartTime(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
}
