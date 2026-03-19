using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using JetBrains.Annotations;

[Serializable]
public class ActionHitBoxConfig
{
    public Vector3 center = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public float height = 0.5f;
    public float radius = 0.1f;
}

public class ActionHitBoxClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> boneTransform;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public ImpactConfig impactConfig = new ImpactConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();

    [HideInInspector]
    public ActionHitBoxBehavior behavior;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxBehavior>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.hitboxConfig = hitboxConfig;
        behavior.impactConfig = impactConfig;
        behavior.dataConfig = dataConfig;

        return playable;
    }
}


public static partial class Enums
{
    public enum HitImpactType
    {
        None, HitStop, HitStick
    }
}