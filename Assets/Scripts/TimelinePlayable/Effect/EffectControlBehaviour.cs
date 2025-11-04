using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline Playable Behaviour 用于控制粒子系统的播放
/// 简化版本：移除时长匹配功能，只保留基本播放控制和变换设置
/// </summary>
public class EffectControlBehaviour : PlayableBehaviour
{
    #region 配置数据 - 在Inspector中设置

    [Header("粒子系统设置")]
    [Tooltip("要控制的粒子系统预制体")]
    public GameObject particlePrefab;

    [Header("变换设置")]
    [Tooltip("父级变换，粒子将相对于此变换放置")]
    public Transform parentTransform;
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
    [Tooltip("拥有此Playable的GameObject")]
    public GameObject owner;

    #endregion

    #region 运行时状态 - 内部使用

    private GameObject particleInstance;           // 实例化的粒子系统对象
    private List<ParticleSystem> particleSystems;  // 所有粒子系统组件
    private bool isPlaying;                         // 是否正在播放

    // 时间跟踪（仿照Unity官方代码设计）
    private const float kUnsetTime = float.MaxValue;    // 未设置时间的标志值
    private double m_LastPlayableTime = kUnsetTime;     // 上一次Playable时间
    private double m_LastParticleTime = kUnsetTime;     // 上一次粒子系统时间

    #endregion

    #region Timeline Playable 生命周期方法

    /// <summary>
    /// 当PlayableGraph开始时调用
    /// </summary>
    public override void OnGraphStart(Playable playable)
    {
        if (particlePrefab != null && particleInstance == null)
        {
            CreateParticleInstance();
        }
    }

    /// <summary>
    /// 当Behaviour开始播放时调用
    /// </summary>
    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (particlePrefab == null) return;

        // 确保粒子实例存在
        if (particleInstance == null)
        {
            CreateParticleInstance();
        }

        if (particleInstance != null && particleSystems != null)
        {
            // 重置时间跟踪状态
            ResetTimeTracking();

            // 激活并开始播放
            ActivateAndPlayParticleSystem();
        }
    }

    /// <summary>
    /// 当Behaviour暂停时调用
    /// </summary>
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (info.effectivePlayState == PlayState.Paused)
        {
            if (particleInstance != null)
            {
                DeactivateParticleSystem();
            }
        }
    }

    /// <summary>
    /// 每帧处理，更新粒子系统状态
    /// </summary>
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UpdateParticleTransform();
        UpdateParticlePlayback(playable);
    }

    /// <summary>
    /// 当Playable销毁时调用，进行资源清理
    /// </summary>
    public override void OnPlayableDestroy(Playable playable)
    {
        CleanupParticleInstance();
    }

    #endregion

    #region 粒子系统管理方法

    /// <summary>
    /// 创建粒子系统实例
    /// </summary>
    private void CreateParticleInstance()
    {
        if (particlePrefab == null) return;

        try
        {
            // 实例化粒子系统预制体
            particleInstance = UnityEngine.Object.Instantiate(particlePrefab);
            particleInstance.name = $"{particlePrefab.name}_EffectControl";

            // 获取所有粒子系统组件（包括子对象）
            particleSystems = new List<ParticleSystem>(
                particleInstance.GetComponentsInChildren<ParticleSystem>());

            // 设置随机种子确保一致性
            if (randomSeed == 0) randomSeed = (uint)UnityEngine.Random.Range(1, 10000);
            SetRandomSeed(particleSystems, randomSeed);

            // 初始状态为不激活
            particleInstance.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建粒子实例失败: {e.Message}");
        }
    }

    /// <summary>
    /// 设置随机种子到所有粒子系统
    /// </summary>
    private void SetRandomSeed(List<ParticleSystem> systems, uint seed)
    {
        foreach (var ps in systems)
        {
            if (ps == null) continue;

            // 停止并清除现有粒子
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 设置固定随机种子（禁用自动随机）
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
                SetRandomSeed(subEmitters, ++seed);
            }
        }
    }

    /// <summary>
    /// 重置粒子系统到初始状态
    /// </summary>
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

    /// <summary>
    /// 停止所有粒子系统
    /// </summary>
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

    /// <summary>
    /// 清理粒子实例资源
    /// </summary>
    private void CleanupParticleInstance()
    {
        if (particleInstance != null)
        {
            if (destroyOnFinish)
            {
                // 销毁粒子实例
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(particleInstance);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(particleInstance);
                }
            }
            else
            {
                // 停止粒子但不销毁实例
                StopParticleSystems();
                particleInstance.SetActive(false);
            }
            particleInstance = null;
        }

        // 清理粒子系统引用
        if (particleSystems != null)
        {
            particleSystems.Clear();
        }

        // 重置所有状态
        ResetAllStates();
    }

    #endregion

    #region 更新方法

    /// <summary>
    /// 更新粒子系统的变换（位置、旋转、缩放）
    /// </summary>
    private void UpdateParticleTransform()
    {
        if (particleInstance == null)
        {
            Debug.LogWarning("粒子实例为空，无法更新变换");
            return;
        }

        if (parentTransform == null)
        {
            Debug.LogWarning("父级变换为空，使用世界坐标");
            // 如果没有父级，使用世界坐标
            particleInstance.transform.position = localPosition;
            particleInstance.transform.eulerAngles = localRotation;
            particleInstance.transform.localScale = localScale;
            return;
        }

        // 设置相对于父级的变换
        particleInstance.transform.SetParent(parentTransform, false);
        particleInstance.transform.localPosition = localPosition;
        particleInstance.transform.localEulerAngles = localRotation;
        particleInstance.transform.localScale = localScale;
    }

    /// <summary>
    /// 更新粒子系统的播放状态（核心播放逻辑）
    /// </summary>
    private void UpdateParticlePlayback(Playable playable)
    {
        if (!isPlaying || particleInstance == null || particleSystems == null)
            return;

        // 检查粒子系统是否激活
        if (!particleInstance.gameObject.activeInHierarchy)
        {
            m_LastPlayableTime = kUnsetTime;
            return;
        }

        double currentTime = playable.GetTime();
        float particleTime = particleSystems[0] != null ? particleSystems[0].time : 0f;

        if (m_LastPlayableTime > currentTime || !Mathf.Approximately(particleTime, (float)m_LastParticleTime))
        {
            // 情况1：时间倒流或外部修改，需要重新模拟
            SimulateAllParticles((float)currentTime, true);
        }
        else if (m_LastPlayableTime < currentTime)
        {
            // 情况2：时间正常前进，使用增量模拟
            float deltaTime = (float)(currentTime - m_LastPlayableTime);
            SimulateAllParticles(deltaTime, false);
        }

        // 更新跟踪变量
        m_LastPlayableTime = currentTime;
        m_LastParticleTime = particleTime;
    }

    /// <summary>
    /// 模拟所有粒子系统的播放（使用分步模拟避免大时间步长问题）
    /// </summary>
    private void SimulateAllParticles(float time, bool restart)
    {
        const bool withChildren = true;    // 包含子发射器
        const bool fixedTimeStep = false;  // 使用可变时间步长
        float maxTime = Time.maximumDeltaTime; // 最大时间步长

        if (particleSystems == null) return;

        // 重新模拟时需要先重置到0
        if (restart)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(0, withChildren, true, fixedTimeStep);
            }
        }

        // 分步模拟：避免大时间步长导致的模拟不准确
        while (time > maxTime)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(maxTime, withChildren, false, fixedTimeStep);
            }
            time -= maxTime;
        }

        // 模拟剩余时间
        if (time > 0)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                    ps.Simulate(time, withChildren, false, fixedTimeStep);
            }
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 重置时间跟踪状态
    /// </summary>
    private void ResetTimeTracking()
    {
        m_LastPlayableTime = kUnsetTime;
        m_LastParticleTime = kUnsetTime;
    }

    /// <summary>
    /// 激活并开始播放粒子系统
    /// </summary>
    private void ActivateAndPlayParticleSystem()
    {
        particleInstance.SetActive(true);
        ResetParticleSystems();
        isPlaying = true;
    }

    /// <summary>
    /// 停用粒子系统
    /// </summary>
    private void DeactivateParticleSystem()
    {
        particleInstance.SetActive(false);
        StopParticleSystems();
        isPlaying = false;
    }

    /// <summary>
    /// 重置所有运行时状态
    /// </summary>
    private void ResetAllStates()
    {
        isPlaying = false;
        ResetTimeTracking();
    }

    #endregion

    #region 编辑器调试功能

#if UNITY_EDITOR

    /// <summary>
    /// 调试粒子系统状态（在Inspector中右键调用）
    /// </summary>
    [ContextMenu("调试粒子状态")]
    public void DebugParticleState()
    {
        if (particleSystems == null || particleSystems.Count == 0) return;

        Debug.Log($"=== 粒子系统状态调试 ===");
        Debug.Log($"粒子实例: {particleInstance?.name ?? "null"}");
        Debug.Log($"父级变换: {parentTransform?.name ?? "null"}");
        Debug.Log($"播放状态: {isPlaying}");

        foreach (var ps in particleSystems)
        {
            if (ps != null)
            {
                var main = ps.main;
                Debug.Log($"{ps.gameObject.name}: 时间={ps.time:F2}, 粒子数={ps.particleCount}, 是否播放={ps.isPlaying}");
            }
        }
    }

    /// <summary>
    /// 调试变换状态（在Inspector中右键调用）
    /// </summary>
    [ContextMenu("调试变换状态")]
    public void DebugTransformState()
    {
        if (particleInstance == null)
        {
            Debug.LogWarning("粒子实例为空");
            return;
        }

        Debug.Log($"=== 变换状态调试 ===");
        Debug.Log($"粒子实例: {particleInstance.name}");
        Debug.Log($"父级变换: {(parentTransform != null ? parentTransform.name : "null")}");
        Debug.Log($"世界位置: {particleInstance.transform.position}");
        Debug.Log($"本地位置: {particleInstance.transform.localPosition}");
        Debug.Log($"父级: {(particleInstance.transform.parent != null ? particleInstance.transform.parent.name : "null")}");
        Debug.Log($"设置的位置: {localPosition}");
        Debug.Log($"设置的旋转: {localRotation}");
        Debug.Log($"设置的缩放: {localScale}");
    }

#endif

    #endregion
}