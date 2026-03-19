using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionCleanupClip : PlayableAsset
{
    [Header("开始时清理")]
    [Tooltip("Clip 开始时清理输入缓冲")]
    public bool clearInputOnStart = false;

    [Tooltip("Clip 开始时清理标签")]
    public bool clearTagsOnStart = false;

    [Header("结束时清理 - 正常播完")]
    [Tooltip("Clip 正常播完时清理输入缓冲")]
    public bool clearInputOnEndNormal = false;

    [Tooltip("Clip 正常播完时清理标签")]
    public bool clearTagsOnEndNormal = false;

    [Header("结束时清理 - 被中断")]
    [Tooltip("Clip 被强制中断时清理输入缓冲")]
    public bool clearInputOnEndInterrupt = false;

    [Tooltip("Clip 被强制中断时清理标签")]
    public bool clearTagsOnEndInterrupt = false;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionCleanupBehavior>.Create(graph);
        ActionCleanupBehavior behaviour = playable.GetBehaviour();

        // 开始时
        behaviour.cleanupOnStart = GetCleanupTarget(clearInputOnStart, clearTagsOnStart);

        // 结束时 - 正常
        behaviour.cleanupOnEndNormal = GetCleanupTarget(clearInputOnEndNormal, clearTagsOnEndNormal);

        // 结束时 - 中断
        behaviour.cleanupOnEndInterrupt = GetCleanupTarget(clearInputOnEndInterrupt, clearTagsOnEndInterrupt);

        return playable;
    }

    private Enums.CleanupTarget GetCleanupTarget(bool clearInput, bool clearTags)
    {
        Enums.CleanupTarget result = Enums.CleanupTarget.None;
        if (clearInput) result |= Enums.CleanupTarget.Input;
        if (clearTags) result |= Enums.CleanupTarget.Tags;
        return result;
    }
}
