using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(EffectControlClip))]
public class EffectControlClipEditor : ActionClipEditorBase
{
    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as EffectControlClip;
        var effectName = asset.particlePrefab.name.ToString();

        clip.displayName = $"Effect : {effectName}";
    }
}