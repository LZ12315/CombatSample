using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionPhaseTrack))]
public class ActionPhaseTrackEditor : ActionTrackEditorBase{ }


[CustomTimelineEditor(typeof(ActionPhaseAsset))]
class ActionPhaseClipEditor : ActionClipEditorBase
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
        var asset = clip.asset as ActionPhaseAsset;
        var phaseType = asset.actionPhase.ToString();

        clip.displayName = $"ActionPhase : {phaseType}";
    }

}
