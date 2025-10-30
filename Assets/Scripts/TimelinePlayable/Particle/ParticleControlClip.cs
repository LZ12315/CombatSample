using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class ParticleControlClip : PlayableAsset, ITimelineClipAsset
{
    [Header("粒子设置")]
    public GameObject particlePrefab;

    [Header("变换设置")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Header("播放设置")]
    public uint randomSeed = 1;
    public bool destroyOnFinish = true;
    public bool previewInEditMode = true;

    public ClipCaps clipCaps => ClipCaps.Extrapolation | ClipCaps.Looping;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ParticleControlBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.particlePrefab = particlePrefab;
        behaviour.localPosition = localPosition;
        behaviour.localRotation = localRotation;
        behaviour.localScale = localScale;
        behaviour.randomSeed = randomSeed;
        behaviour.destroyOnFinish = destroyOnFinish;
        behaviour.previewInEditMode = previewInEditMode;
        behaviour.owner = owner;

        return playable;
    }
}

public class ParticleControlBehaviour : PlayableBehaviour
{
    public GameObject particlePrefab;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale;
    public uint randomSeed;
    public bool destroyOnFinish;
    public bool previewInEditMode;
    public GameObject owner;

    [NonSerialized] public GameObject particleInstance;
    private ParticleSystem[] particleSystems;
    private bool isPlaying;
    private double lastTime = -1;
    private bool isInitialized;

    public override void OnGraphStart(Playable playable)
    {
        if (particlePrefab == null) return;

        // 确保只初始化一次
        if (isInitialized) return;
        isInitialized = true;

        // 实例化粒子系统
        particleInstance = UnityEngine.Object.Instantiate(particlePrefab);
        particleInstance.name = $"{particlePrefab.name} [Timeline]";

        // 设置隐藏标志，避免出现在Inspector中
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            particleInstance.hideFlags = HideFlags.HideAndDontSave;
        }
#endif

        particleInstance.SetActive(false);

        // 获取所有粒子系统组件
        particleSystems = particleInstance.GetComponentsInChildren<ParticleSystem>();

        // 设置随机种子
        if (randomSeed == 0) randomSeed = (uint)UnityEngine.Random.Range(1, 10000);
        SetRandomSeed(particleSystems, randomSeed);
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (particleInstance == null || particleSystems == null) return;

        particleInstance.SetActive(true);
        isPlaying = true;
        lastTime = playable.GetTime();

        // 重置所有粒子系统
        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(false);
            }
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (particleInstance == null) return;

        // 只有在有效暂停时（不是由于超出范围）才停止
        if (info.effectivePlayState == PlayState.Paused)
        {
            particleInstance.SetActive(false);
            isPlaying = false;

            // 停止所有粒子系统
            foreach (var ps in particleSystems)
            {
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    public override void PrepareFrame(Playable playable, FrameData info)
    {
        if (!isPlaying || particleInstance == null || particleSystems == null) return;

        double currentTime = playable.GetTime();

        // 检测时间反转（Timeline拖拽）
        if (lastTime > currentTime + 0.1f)
        {
            // 时间倒流，重置粒子系统
            ResetParticleSystems();
            lastTime = currentTime;
            return;
        }

        float deltaTime = (float)(currentTime - lastTime);
        lastTime = currentTime;

        // 只在编辑模式下使用Simulate，运行模式下让粒子系统自然播放
#if UNITY_EDITOR
        if (!Application.isPlaying && previewInEditMode)
        {
            // 编辑模式下精确控制粒子系统时间
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    // 使用Simulate来精确控制时间
                    ps.Simulate(deltaTime, true, false);
                }
            }
        }
#endif
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        CleanupParticleInstance();
    }

    private void ResetParticleSystems()
    {
        if (particleSystems == null) return;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(false);
            }
        }
    }

    private void CleanupParticleInstance()
    {
        if (particleInstance != null)
        {
            if (destroyOnFinish)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(particleInstance);
                }
                else
                {
                    // 编辑模式下使用DestroyImmediate
                    UnityEngine.Object.DestroyImmediate(particleInstance);
                }
            }
            else
            {
                // 如果不销毁，确保粒子系统停止
                if (particleSystems != null)
                {
                    foreach (var ps in particleSystems)
                    {
                        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
                particleInstance.SetActive(false);
            }
            particleInstance = null;
        }

        isInitialized = false;
        isPlaying = false;
    }

    private void SetRandomSeed(ParticleSystem[] systems, uint seed)
    {
        foreach (var ps in systems)
        {
            if (ps == null) continue;

            if (ps.useAutoRandomSeed)
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed;
            }

            // 递归设置子发射器的随机种子
            var subEmitters = new List<ParticleSystem>();
            for (int i = 0; i < ps.subEmitters.subEmittersCount; i++)
            {
                var subEmitter = ps.subEmitters.GetSubEmitterSystem(i);
                if (subEmitter != null) subEmitters.Add(subEmitter);
            }

            if (subEmitters.Count > 0)
            {
                SetRandomSeed(subEmitters.ToArray(), ++seed);
            }
        }
    }
}

public class ParticleControlMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var trackBinding = playerData as Transform;
        if (trackBinding == null) return;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight > 0)
            {
                var inputPlayable = (ScriptPlayable<ParticleControlBehaviour>)playable.GetInput(i);
                var behaviour = inputPlayable.GetBehaviour();

                if (behaviour != null && behaviour.particleInstance != null)
                {
                    // 更新粒子系统实例的位置
                    behaviour.particleInstance.transform.SetParent(trackBinding, false);
                    behaviour.particleInstance.transform.localPosition = behaviour.localPosition;
                    behaviour.particleInstance.transform.localEulerAngles = behaviour.localRotation;
                    behaviour.particleInstance.transform.localScale = behaviour.localScale;
                }
            }
        }
    }
}