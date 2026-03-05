using System;
using UnityEngine;
using DeiveEx.TagTree; // ? 引入标签树插件命名空间

public class ActionInstance
{
    // 原始配置引用（只读）
    public ActionAsset Config { get; }

    // 运行时独立数据（进度、阶段等）
    public ActionData RuntimeData { get; private set; }

    // 缓存当前正在执行此动作的 Actor
    private Actor _actor;

    public ActionInstance(ActionAsset config)
    {
        Config = config;

        // 初始化运行时数据
        ResetRuntimeData();
    }

    /// <summary>
    /// 当此动作实例变为激活状态时调用
    /// </summary>
    public void OnEnter(Actor actor)
    {
        _actor = actor;

        if (_actor != null && _actor.tagContainer != null)
        {
            // ==========================================
            // 1. 强制入场净空 (消耗事件大类标签)
            // ==========================================
            if (Config.ConsumeTagsOnEnter != null)
            {
                for (int i = 0; i < Config.ConsumeTagsOnEnter.Count; i++)
                {
                    if (Config.ConsumeTagsOnEnter[i] != null)
                    {
                        Tag tagToConsume = Config.ConsumeTagsOnEnter[i].GetTag();
                        if (tagToConsume != null)
                        {
                            // ? 核心绝杀：彻底连根拔起该标签及其所有子标签！
                            // 比如 Config 里填了 Event.Hit，那么 Event.Hit.Physical 和 Event.Hit.Magic 都会被一波带走
                            _actor.tagContainer.RemoveTagCompletely(tagToConsume);
                        }
                    }
                }
            }

            // ==========================================
            // 2. 宣告身份 (挂载运行时身份标签)
            // ==========================================
            if (Config.SelfTag != null)
            {
                Tag selfTagObj = Config.SelfTag.GetTag();
                if (selfTagObj != null)
                {
                    _actor.tagContainer.AddTag(selfTagObj);
                }
            }
        }
    }

    /// <summary>
    /// 当此动作实例变为非激活状态时调用
    /// </summary>
    public void OnExit()
    {
        // ==========================================
        // 3. 安全退场：回收专属身份标签
        // ==========================================
        if (_actor != null && _actor.tagContainer != null && Config.SelfTag != null)
        {
            Tag selfTagObj = Config.SelfTag.GetTag();
            if (selfTagObj != null)
            {
                // 动作结束，安全回收，不留垃圾
                _actor.tagContainer.RemoveTag(selfTagObj);
            }
        }

        _actor = null;
    }

    #region 数据更新方法 (保留原样，用于记录动作播放的物理进度)

    /// <summary>
    /// 更新动作的播放进度
    /// </summary>
    public void UpdateNormalizedTime(double normalizedTime)
    {
        var currentData = RuntimeData;
        currentData.normalizedTime = normalizedTime;
        RuntimeData = currentData;
    }

    /// <summary>
    /// 更新动作的当前阶段
    /// </summary>
    public void UpdatePhase(Enums.ActionPhase phase)
    {
        var currentData = RuntimeData;
        currentData.phase = phase;
        RuntimeData = currentData;
    }

    /// <summary>
    /// 将所有运行时数据重置为初始状态
    /// </summary>
    public void ResetRuntimeData()
    {
        RuntimeData = new ActionData
        {
            normalizedTime = 0,
            phase = Enums.ActionPhase.Neutral
        };
    }
    #endregion
}