using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionTransitionTrack))]
public class ActionTransitionTrackEditor : ActionTrackEditorBase{ }


[CustomTimelineEditor(typeof(ActionTransitionAsset))]
class ActionTransitionClipEditor : ActionClipEditorBase
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        var options = base.GetClipOptions(clip);
        options.highlightColor = new Color(0.4f, 0.4f, 0.6f);  // …Ë÷√Clip—’…´

        return options;
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip.asset == null) return;

        AdjustClipStartTime(clip);
        SetClipDisplayName(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    void SetClipDisplayName(TimelineClip clip)
    {
        var asset = clip.asset as ActionTransitionAsset;
        var transitionType = asset.actionPhase.ToString();

        clip.displayName = $"Translate when : {transitionType}";
    }

}
