using UnityEngine;
using System;

[Serializable]
public class ActionDataCondition : TransitionCondition
{
    // 你的检查类型开关
    public Enums.ActionDataType checkType = Enums.ActionDataType.None;

    // Phase 检查参数
    public Enums.ActionPhase requiredPhase = Enums.ActionPhase.Recovery;

    // Progress 检查参数
    [Range(0f, 1f)] public float minProgress = 0f;
    [Range(0f, 1f)] public float maxProgress = 1f;

    protected override bool OnCheck()
    {
        var currentAction = actor.actionPlayer.CurrentAction;
        if (currentAction == null) return false;

        ActionData actionData = currentAction.RuntimeData;

        // 1. 检查 Phase (位运算极速判断)
        if ((checkType & Enums.ActionDataType.Phase) != 0)
        {
            if ((actionData.phase & requiredPhase) == 0)
                return false;
        }

        // 2. 检查 Progress (区间判断)
        if ((checkType & Enums.ActionDataType.Progress) != 0)
        {
            float time = (float)actionData.normalizedTime;
            if (time < minProgress || time > maxProgress)
                return false;
        }

        return true;
    }

    public override TransitionCondition Clone()
    {
        // MemberwiseClone 是浅拷贝，对于值类型字段完全够用，比手写 new 更不容易出错
        return (ActionDataCondition)this.MemberwiseClone();
    }
}

public static partial class Enums
{
    [System.Flags]
    public enum ActionDataType
    {
        None = 0,
        Phase = 1 << 0, // 1
        Progress = 1 << 1  // 2
    }
}