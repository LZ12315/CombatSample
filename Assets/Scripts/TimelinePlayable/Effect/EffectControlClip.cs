using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class EffectControlClip : PlayableAsset, ITimelineClipAsset
{
    [Header("粒子设置")]
    [Tooltip("要控制的粒子系统预制体")]
    public GameObject particlePrefab;

    [Header("变换设置")]
    [Tooltip("父级变换引用（使用ExposedReference以便在Timeline中绑定）")]
    public ExposedReference<Transform> parentTransform;
    [Tooltip("相对于父级的本地位置")]
    public Vector3 localPosition = Vector3.zero;
    [Tooltip("相对于父级的本地旋转（欧拉角）")]
    public Vector3 localRotation = Vector3.zero;
    [Tooltip("相对于父级的本地缩放")]
    public Vector3 localScale = Vector3.one;

    [Header("播放控制")]
    [Tooltip("激活时自动播放")]
    public bool playOnActive = true;
    [Tooltip("播放完成后销毁实例")]
    public bool destroyOnFinish = true;
    [Tooltip("随机种子，确保播放一致性")]
    public uint randomSeed = 1;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectControlBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        // 设置配置参数
        behaviour.particlePrefab = particlePrefab;
        behaviour.parentTransform = parentTransform.Resolve(graph.GetResolver());
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