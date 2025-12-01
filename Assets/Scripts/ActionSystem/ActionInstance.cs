using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public class ActionInstance
{
    // 原始配置引用（只读）
    public ActionAsset Config { get; }

    // 运行时独立数据
    public ActionData RuntimeData { get; private set; }

    // 运行时Transition副本
    public List<ActionTransition> RuntimeTransitions { get; private set; }

    public ActionInstance(ActionAsset config)
    {
        Config = config;

        // 初始化运行时数据（独立副本）
        RuntimeData = new ActionData
        {
            normalizedTime = 0,
            phase = Enums.ActionPhase.Neutral
        };

        // 创建Transition的运行时副本
        RuntimeTransitions = CreateRuntimeTransitions(config.Transitions);
    }

    public void EnableTransitions(Actor actor)
    {
        for (int i = 0; i < RuntimeTransitions.Count; i++)
            RuntimeTransitions[i].Enable(actor);
    }

    public ActionAsset CheckTransitions()
    {
        for (int i = 0; i < RuntimeTransitions.Count; i++)
        {
            var transition = RuntimeTransitions[i];
            if (transition.Check())
                return transition.TargetAction;
        }

        return null;
    }

    public void DisableTransitions()
    {
        for (int i = 0; i < RuntimeTransitions.Count; i++)
            RuntimeTransitions[i].Disable();
    }

    #region 数据更新方法

    public void UpdateRuntimeData(double normalizedTime, Enums.ActionPhase phase)
    {
        RuntimeData = new ActionData
        {
            normalizedTime = normalizedTime,
            phase = phase
        };
    }

    public void UpdateRuntimeData(ActionData newData)
    {
        UpdateRuntimeData(newData.normalizedTime, newData.phase);
    }

    public void ResetRuntimeData()
    {
        UpdateRuntimeData(0, Enums.ActionPhase.Neutral);
    }
    #endregion

    #region 辅助方法

    private List<ActionTransition> CreateRuntimeTransitions(IReadOnlyList<ActionTransition> sourceTransitions)
    {
        var runtimeTransitions = new List<ActionTransition>();

        foreach (var sourceTransition in sourceTransitions)
        {
            var runtimeTransition = sourceTransition.Clone();
            runtimeTransitions.Add(runtimeTransition);
        }

        return runtimeTransitions;
    }
    #endregion

}
