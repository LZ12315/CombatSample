using UnityEngine;
using System;

[Serializable]
public class ActionDataCondition : ActionCondition
{
    // 你的检查类型开关
    public Enums.ActionDataType checkType = Enums.ActionDataType.None;

    // Phase 检查参数
    public Enums.ActionPhase requiredPhase = Enums.ActionPhase.Recovery;

    // Progress 检查参数
    [Range(0f, 1f)] public float minProgress = 0f;
    [Range(0f, 1f)] public float maxProgress = 1f;

    // 1. 签名同步：加上 Actor 参数，直接使用外部传入的 actor
    protected override bool OnCheck(Actor actor)
    {
        // 增加一下安全判断
        if (actor == null || actor.actionPlayer == null) return false;

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

    // 2. 彻底删除 Clone() 方法！
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