using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionTagClip))]
public class ActionTagClipEditor : ActionClipEditorBase
{
    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as ActionTagClip;
        var phaseType = asset.tag.GetTag().TagName.ToString();

        clip.displayName = $"{phaseType}";
    }
}
