using UnityEngine;
using System;

[Serializable]
public class ActionDataCondition : ActionCondition
{
    // 检查类型
    public Enums.ActionDataType checkType = Enums.ActionDataType.None;

    // Progress 检查参数
    [Range(0f, 1f)] public float minProgress = 0f;
    [Range(0f, 1f)] public float maxProgress = 1f;

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || actor.actionPlayer == null) return false;

        var currentAction = actor.actionPlayer.CurrentAction;
        if (currentAction == null) return false;

        ActionData actionData = currentAction.RuntimeData;

        // 检查 Progress（时间进度）
        if ((checkType & Enums.ActionDataType.Progress) != 0)
        {
            float time = (float)actionData.normalizedTime;
            if (time < minProgress || time > maxProgress)
                return false;
        }

        return true;
    }
}

public static partial class Enums
{
    [System.Flags]
    public enum ActionDataType
    {
        None = 0,
        Progress = 1 << 1
    }
}
