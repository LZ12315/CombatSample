using Animancer;
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public struct ActionHitBoxConfig
{
    public Vector3 center;
    public Quaternion rotation;
    public float height;
    public float radius;
}

[Serializable]
public class ActionHitBoxAsset : PlayableAsset, ITimelineClipAsset
{
    // Ê¹ÓÃ ExposedReference Ìæ´ú Transform
    public ExposedReference<Transform> boneTransform;
    public ActionHitBoxConfig config;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxClip>.Create(graph);
        var behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.config = config;

        return playable;
    }
}

public class ActionHitBoxClip : ActionClipBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig config;

}
