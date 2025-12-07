using System;
using System.Collections.Generic;
using UnityEngine;

public class ActionInstance
{
    // 原始配置引用（只读）
    public ActionAsset Config { get; }

    // 运行时独立数据
    public ActionData RuntimeData { get; private set; }

    // 【优化】封装为只读属性，防止外部直接修改列表
    public IReadOnlyList<ActionTransition> RuntimeTransitions { get; }
    private readonly List<ActionTransition> _runtimeTransitionsInternal;

    public ActionInstance(ActionAsset config)
    {
        Config = config;

        // 初始化运行时数据（独立副本）
        ResetRuntimeData();

        // 创建Transition的运行时副本
        _runtimeTransitionsInternal = CreateRuntimeTransitions(config.Transitions);
        RuntimeTransitions = _runtimeTransitionsInternal.AsReadOnly();
    }

    /// <summary>
    /// 当此动作实例变为激活状态时调用
    /// </summary>
    public void OnEnter(Actor actor)
    {
        EnableTransitions(actor);
    }

    /// <summary>
    /// 当此动作实例变为非激活状态时调用
    /// </summary>
    public void OnExit()
    {
        DisableTransitions();
    }
    /// <summary>
    /// 检查所有转换条件，并将满足条件的目标动作添加到传入的列表中。
    /// </summary>
    /// <param name="outResults">用于接收结果的列表，避免GC Alloc。</param>
    public void CheckTransitions(List<ActionAsset> outResults)
    {
        // 注意：这里不再创建新列表，而是使用外部传入的列表
        for (int i = 0; i < _runtimeTransitionsInternal.Count; i++)
        {
            var transition = _runtimeTransitionsInternal[i];
            if (transition.Check())
            {
                outResults.Add(transition.TargetAction);
            }
        }
    }
    // --- 【优化结束】 ---


    #region 内部状态管理

    private void EnableTransitions(Actor actor)
    {
        for (int i = 0; i < _runtimeTransitionsInternal.Count; i++)
            _runtimeTransitionsInternal[i].Enable(actor);
    }

    private void DisableTransitions()
    {
        for (int i = 0; i < _runtimeTransitionsInternal.Count; i++)
            _runtimeTransitionsInternal[i].Disable();
    }
    #endregion


    #region 数据更新方法

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


    #region 辅助方法
    private List<ActionTransition> CreateRuntimeTransitions(IReadOnlyList<ActionTransition> sourceTransitions)
    {
        var runtimeTransitions = new List<ActionTransition>();

        if (sourceTransitions == null) return runtimeTransitions;

        foreach (var sourceTransition in sourceTransitions)
        {
            if (sourceTransition != null)
            {
                runtimeTransitions.Add(sourceTransition.Clone());
            }
        }

        return runtimeTransitions;
    }
    #endregion
}