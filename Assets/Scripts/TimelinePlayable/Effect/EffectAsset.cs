using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class EffectConfig
{
    public Vector3 position = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
}

public class EffectAsset : PlayableAsset
{
    [Header("配置")]
    public ExposedReference<Transform> parentTransform;
    public List<GameObject> effects;

    [Header("属性")]
    public Enums.EffectPlayMode effectPlayMode;
    public bool followParent = false;
    public EffectConfig effectConfig;

    [HideInInspector]
    public EffectClip behavior;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectClip>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.effectPlayMode = effectPlayMode;
        behavior.effectPrefabs = effects;
        behavior.followParent = followParent;
        behavior.parentTransform = parentTransform.Resolve(graph.GetResolver());
        behavior.effectConfig = effectConfig;

        return playable;
    }
}

public class EffectClip : ActionClipBase
{
    public Transform parentTransform;
    public List<GameObject> effectPrefabs;

    public Enums.EffectPlayMode effectPlayMode;
    public bool followParent = false;
    public EffectConfig effectConfig;

    private GameObject effectObject;

    protected override void OnClipPlay(Playable playable)
    {
        base.OnClipPlay(playable);
        DestroyEffect();
        CreateEffect();
    }

    protected override void OnClipPause()
    {
        if (!Application.isPlaying)
            DestroyEffect();
        base.OnClipPause();
    }

    protected override void OnClipUpdate(Playable playable)
    {
        base.OnClipUpdate(playable);
        UpdateEffect();
    }

    protected override void OnClipFinish(bool isNormal)
    {
        DestroyEffect();
        base.OnClipFinish(isNormal);
    }

    private void CreateEffect()
    {
        if(effectObject != null || parentTransform == null) return;

        effectObject = new GameObject("Effects");
        effectObject.hideFlags = HideFlags.HideInHierarchy;

        effectObject.transform.position = parentTransform.TransformPoint(effectConfig.position);
        effectObject.transform.rotation = parentTransform.rotation * effectConfig.rotation;

        actor.StartCoroutine(OnCreatEffects());
    }

    private void UpdateEffect()
    {
        if (effectObject != null || parentTransform == null) return;

        if(followParent)
        {
            effectObject.transform.position = parentTransform.TransformPoint(effectConfig.position);
            effectObject.transform.rotation = parentTransform.rotation * effectConfig.rotation;
        }
    }

    private void DestroyEffect()
    {
        if (effectObject == null) return;

        // 销毁游戏对象
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(effectObject);
        else
            UnityEngine.Object.DestroyImmediate(effectObject);

        effectObject = null;
    }

    private IEnumerator OnCreatEffects()
    {
        if(effectPlayMode == Enums.EffectPlayMode.Parallel)
        {
            foreach (var effect in effectPrefabs)
            {
                GameObject newEffect = GameObject.Instantiate(effect);
                newEffect.transform.SetParent(effectObject.transform, false);
            }
        }
        else if(effectPlayMode == Enums.EffectPlayMode.Sequence)
        {
            foreach (var effect in effectPrefabs)
            {
                GameObject newEffect = GameObject.Instantiate(effect);
                newEffect.transform.SetParent(effectObject.transform, false);

                var particleSystem = newEffect.GetComponent<ParticleSystem>();
                yield return new WaitForSeconds(particleSystem.main.duration);
            }

        }
    }

}

public static partial class Enums
{
    public enum EffectPlayMode
    {
        Parallel, Sequence
    }
}