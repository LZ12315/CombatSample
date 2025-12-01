using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;


[CustomTimelineEditor(typeof(ActionPhaseAsset))]
class ActionPhaseClipEditor : ActionClipEditorBase
{

    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as ActionPhaseAsset;
        var phaseType = asset.actionPhase.ToString();

        clip.displayName = $"ActionPhase : {phaseType}";
    }

}
