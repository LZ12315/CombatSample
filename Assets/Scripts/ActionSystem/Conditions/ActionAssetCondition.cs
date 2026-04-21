using System;
using UnityEngine;

/// <summary>
/// 判断当前正在播放的 Action 是否匹配指定的 ActionAsset。
/// - 若 <see cref="requiredAction"/> 已指定：CurrentAction.Config == requiredAction 时通过。
/// - 若 <see cref="requiredAction"/> 为 null：等价于"当前没有任何 Action 正在播放"（CurrentAction == null）时通过。
/// 可配合 invertResult 表达"正在播放某 Action"或"不在播放某 Action"的反向语义。
/// </summary>
[Serializable]
public class ActionAssetCondition : ActionCondition
{
    [Tooltip("要匹配的 ActionAsset。留空表示检查 CurrentAction 是否为 null。")]
    public ActionAsset requiredAction;

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || actor.actionPlayer == null)
            return false;

        var currentInstance = actor.actionPlayer.CurrentAction;

        // 未指定目标 Asset —— 检查是否没有任何 Action 在播
        if (requiredAction == null)
            return currentInstance == null;

        // 指定了目标 Asset —— 当前必须在播且 Config 匹配
        if (currentInstance == null)
            return false;

        return currentInstance.Config == requiredAction;
    }
}
