using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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
    [Tooltip("骨骼或挂点（HumanBone / 相对 Animator 的路径）")]
    public BoneReference boneReference;

    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();

    [Tooltip("Impact effects for this hit. Only add what this move really needs.")]
    [SerializeReference, SubclassSelector]
    public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();

    [HideInInspector]
    public ActionHitBoxBehavior behavior;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxBehavior>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.boneReference = boneReference;
        behavior.hitboxConfig = hitboxConfig;
        behavior.dataConfig = dataConfig;
        behavior.effects = effects;

        return playable;
    }
}