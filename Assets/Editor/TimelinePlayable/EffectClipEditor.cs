using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ParticleControlClip))]
public class EffectClipEditor : ActionClipEditorBase
{
    protected override void OnClipChange(TimelineClip clip)
    {
        var asset = clip.asset as ParticleControlClip;
        if (asset == null || asset.particlePrefab == null) return;

        var particlePrefab = asset.particlePrefab;
    }

}
