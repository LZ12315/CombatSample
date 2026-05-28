using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class EffectControlClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Particles")]
    [Tooltip("Particle system prefab to drive")]
    public GameObject particlePrefab;

    [Header("Transform")]
    [Tooltip("Parent reference: HumanBone, Animator-relative Path, or Actor-relative Path.")]
    public BoneReference parentReference;
    [Tooltip("Keep updating this transform after spawn.")]
    public bool updateTransform = true;
    [Tooltip("Local position under parent")]
    public Vector3 localPosition = Vector3.zero;
    [Tooltip("Local rotation (Euler) under parent")]
    public Vector3 localRotation = Vector3.zero;
    [Tooltip("Local scale under parent")]
    public Vector3 localScale = Vector3.one;

    [Header("Playback")]
    [Tooltip("Play on activate")]
    public bool playOnActive = true;
    [Tooltip("Destroy instance when done")]
    public bool destroyOnFinish = true;
    [Tooltip("Random seed for stable playback")]
    public uint randomSeed = 1;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectControlBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // 设置配置参数
        behaviour.particlePrefab = particlePrefab;
        behaviour.parentReference = parentReference;
        behaviour.updateTransform = updateTransform;
        behaviour.localPosition = localPosition;
        behaviour.localRotation = localRotation;
        behaviour.localScale = localScale;
        behaviour.playOnActive = playOnActive;
        behaviour.destroyOnFinish = destroyOnFinish;
        behaviour.randomSeed = randomSeed;
        behaviour.owner = owner;

        return playable;
    }
}
