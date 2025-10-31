using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class EffectControlBehaviour : PlayableBehaviour
{
    // 配置数据
    public GameObject particlePrefab;
    public Transform parentTransform;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale;
    public bool playOnActive;
    public bool destroyOnFinish;
    public uint randomSeed;
    public GameObject owner;

    // 运行时状态
    private GameObject particleInstance;
    private List<ParticleSystem> particleSystems;
    private bool isPlaying;

    // 时间跟踪（仿照官方代码）
    private const float kUnsetTime = float.MaxValue;
    private double m_LastPlayableTime = kUnsetTime;
    private double m_LastParticleTime = kUnsetTime;
    private double m_ClipStartTime = kUnsetTime;

    public override void OnGraphStart(Playable playable)
    {
        if (particlePrefab != null && particleInstance == null)
        {
            CreateParticleInstance();
        }
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning("EffectControlBehaviour: particlePrefab is null");
            return;
        }

        if (particleInstance == null)
        {
            CreateParticleInstance();
        }

        if (particleInstance != null && particleSystems != null)
        {
            // 重置时间跟踪（仿照官方代码）
            m_LastPlayableTime = kUnsetTime;
            m_LastParticleTime = kUnsetTime;
            m_ClipStartTime = playable.GetTime();

            // 激活粒子系统
            particleInstance.SetActive(true);

            // 重置粒子系统（使用官方代码的方法）
            ResetParticleSystems();

            isPlaying = true;

            Debug.Log($"开始播放粒子系统: {particleInstance.name}");
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (info.effectivePlayState == PlayState.Paused)
        {
            if (particleInstance != null)
            {
                particleInstance.SetActive(false);
                StopParticleSystems();
                isPlaying = false;
                Debug.Log("暂停粒子系统");
            }
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UpdateParticleTransform();
        UpdateParticlePlayback(playable);
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        CleanupParticleInstance();
    }

    private void CreateParticleInstance()
    {
        if (particlePrefab == null) return;

        try
        {
            particleInstance = UnityEngine.Object.Instantiate(particlePrefab);
            particleInstance.name = $"{particlePrefab.name}_EffectControl";

            particleSystems = new List<ParticleSystem>(
                particleInstance.GetComponentsInChildren<ParticleSystem>());

            // 设置随机种子（使用官方代码的方法）
            if (randomSeed == 0) randomSeed = (uint)UnityEngine.Random.Range(1, 10000);
            SetRandomSeed(particleSystems, randomSeed);

            particleInstance.SetActive(false);

            Debug.Log($"创建粒子实例: {particleInstance.name}, 找到 {particleSystems.Count} 个粒子系统");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建粒子实例失败: {e.Message}");
        }
    }

    // 使用官方代码的随机种子设置方法
    private void SetRandomSeed(List<ParticleSystem> systems, uint seed)
    {
        foreach (var ps in systems)
        {
            if (ps == null) continue;

            // 停止并清除现有粒子
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 设置随机种子
            if (ps.useAutoRandomSeed)
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed;
            }

            Debug.Log($"设置 {ps.gameObject.name} 随机种子: {seed}");

            // 递归设置子发射器
            var subEmitters = new List<ParticleSystem>();
            for (int i = 0; i < ps.subEmitters.subEmittersCount; i++)
            {
                var subEmitter = ps.subEmitters.GetSubEmitterSystem(i);
                if (subEmitter != null) subEmitters.Add(subEmitter);
            }

            if (subEmitters.Count > 0)
            {
                SetRandomSeed(subEmitters, ++seed);
            }
        }
    }

    private void ResetParticleSystems()
    {
        if (particleSystems == null) return;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                // 完全停止并清除（使用官方代码的方法）
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // 开始播放
                ps.Play(false);
            }
        }
    }

    private void StopParticleSystems()
    {
        if (particleSystems == null) return;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void UpdateParticleTransform()
    {
        if (particleInstance == null || parentTransform == null) return;

        particleInstance.transform.SetParent(parentTransform, false);
        particleInstance.transform.localPosition = localPosition;
        particleInstance.transform.localEulerAngles = localRotation;
        particleInstance.transform.localScale = localScale;
    }

    // 核心修改：使用官方代码的智能播放控制逻辑
    private void UpdateParticlePlayback(Playable playable)
    {
        if (!isPlaying || particleInstance == null || particleSystems == null)
            return;

        // 检查粒子系统是否激活（仿照官方代码）
        if (!particleInstance.gameObject.activeInHierarchy)
        {
            m_LastPlayableTime = kUnsetTime;
            return;
        }

        double currentTime = playable.GetTime();

        // 获取第一个粒子系统的时间作为参考（仿照官方代码）
        float particleTime = particleSystems[0] != null ? particleSystems[0].time : 0f;

        // 官方代码的智能决策逻辑：
        // 1. 时间倒流 或 粒子时间被外部修改 → 重新模拟
        // 2. 时间正常前进 → 增量模拟

        if (m_LastPlayableTime > currentTime || !Mathf.Approximately((float)particleTime, (float)m_LastParticleTime))
        {
            // 情况1：重新模拟（从0开始）
            SimulateAllParticles((float)currentTime, true);
            Debug.Log($"重新模拟 - 时间: {currentTime:F2}s, 粒子时间: {particleTime:F2}s");
        }
        else if (m_LastPlayableTime < currentTime)
        {
            // 情况2：增量模拟（从上一次开始）
            float deltaTime = (float)(currentTime - m_LastPlayableTime);
            SimulateAllParticles(deltaTime, false);
            Debug.Log($"增量模拟 - Delta: {deltaTime:F2}s");
        }

        // 更新跟踪变量（仿照官方代码）
        m_LastPlayableTime = currentTime;
        m_LastParticleTime = particleTime;
    }

    // 使用官方代码的分步模拟算法
    private void SimulateAllParticles(float time, bool restart)
    {
        const bool withChildren = true;    // 包含子发射器
        const bool fixedTimeStep = false;  // 使用可变时间步长
        float maxTime = Time.maximumDeltaTime;

        if (particleSystems == null) return;

        if (restart)
        {
            // 重新模拟：先模拟到0位置
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(0, withChildren, true, fixedTimeStep);
            }
        }

        // 官方代码的关键：分步模拟避免大时间步长问题
        while (time > maxTime)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(maxTime, withChildren, false, fixedTimeStep);
            }
            time -= maxTime;
        }

        if (time > 0)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(time, withChildren, false, fixedTimeStep);
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
                    UnityEngine.Object.DestroyImmediate(particleInstance);
                }
                Debug.Log("销毁粒子实例");
            }
            else
            {
                StopParticleSystems();
                particleInstance.SetActive(false);
                Debug.Log("停止粒子但不销毁实例");
            }
            particleInstance = null;
        }

        if (particleSystems != null)
        {
            particleSystems.Clear();
        }

        isPlaying = false;
        m_LastPlayableTime = kUnsetTime;
        m_LastParticleTime = kUnsetTime;
        m_ClipStartTime = kUnsetTime;
    }

    // 编辑器专用功能
#if UNITY_EDITOR
    [ContextMenu("调试粒子状态")]
    public void DebugParticleState()
    {
        if (particleSystems == null || particleSystems.Count == 0) return;

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                Debug.Log($"{ps.gameObject.name}: 时间={ps.time:F2}, 粒子数={ps.particleCount}, 是否播放={ps.isPlaying}");
            }
        }
    }
#endif
}