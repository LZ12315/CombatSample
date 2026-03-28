using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionCleanupClip : PlayableAsset
{
    [Header("On Start")]
    [Tooltip("Clear input buffer when clip starts")]
    public bool clearInputOnStart = false;

    [Tooltip("Clear tags when clip starts")]
    public bool clearTagsOnStart = false;

    [Header("On End (Finished)")]
    [Tooltip("Clear input buffer when clip ends normally")]
    public bool clearInputOnEndNormal = false;

    [Tooltip("Clear tags when clip ends normally")]
    public bool clearTagsOnEndNormal = false;

    [Header("On End (Cut)")]
    [Tooltip("Clear input buffer when clip is cut")]
    public bool clearInputOnEndInterrupt = false;

    [Tooltip("Clear tags when clip is cut")]
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
