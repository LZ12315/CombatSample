using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionTagClip))]
public class ActionTagClipEditor : ActionClipEditorBase
{
    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as ActionTagClip;

        var tagName = "None";
        if(asset.tag.GetTag() != null)
        {
            tagName = asset.tag.GetTag().FullTagName.ToString();
        }
        
        clip.displayName = $"{tagName}";
    }
}
