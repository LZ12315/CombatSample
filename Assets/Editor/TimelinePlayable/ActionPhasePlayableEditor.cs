using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;


[CustomTimelineEditor(typeof(ActionDataClip))]
class ActionDataClipEditor : ActionClipEditorBase
{

    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as ActionDataClip;
        var phaseType = asset.actionPhase.ToString();

        clip.displayName = $"ActionPhase : {phaseType}";
    }

}
