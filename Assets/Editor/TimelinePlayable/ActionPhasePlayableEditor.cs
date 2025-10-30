using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionPhaseTrack))]
public class ActionPhaseTrackEditor : ActionTrackEditorBase{ }


[CustomTimelineEditor(typeof(ActionPhaseAsset))]
class ActionPhaseClipEditor : ActionClipEditorBase
{
    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip.asset == null) return;

        AdjustClipStartTime(clip);
        SetClipDisplayName(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    void SetClipDisplayName(TimelineClip clip)
    {
        var asset = clip.asset as ActionPhaseAsset;
        var phaseType = asset.actionPhase_Start.ToString();

        clip.displayName = $"ActionPhase : {phaseType}";
    }

}
