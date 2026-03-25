using System;
using System.Collections.Generic;
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
    [Tooltip("命中盒挂载到的骨骼/节点。")]
    public ExposedReference<Transform> boneTransform;

    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();

    [Tooltip("本次命中要触发的打击效果列表。只添加这招真正需要的效果。")]
    [SerializeReference, SubclassSelector]
    public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();

    [HideInInspector]
    public ActionHitBoxBehavior behavior;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxBehavior>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.hitboxConfig = hitboxConfig;
        behavior.dataConfig = dataConfig;
        behavior.effects = effects;

        return playable;
    }
}