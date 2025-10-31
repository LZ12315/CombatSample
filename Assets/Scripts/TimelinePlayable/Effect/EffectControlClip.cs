using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class EffectControlClip : PlayableAsset, ITimelineClipAsset
{
    [Header("粒子设置")]
    public GameObject particlePrefab;

    [Header("位置偏移")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Header("播放控制")]
    public bool playOnActive = true;
    public bool destroyOnFinish = true;
    public uint randomSeed = 1;

    [Header("时长匹配")]
    [Tooltip("启用后，粒子系统播放时长将与Clip时长完全同步")]
    public bool matchClipDuration = true;
    [Tooltip("粒子系统自然播放时长（秒），如果为0则自动计算")]
    public float particleNaturalDuration = 0f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectControlBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.particlePrefab = particlePrefab;
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