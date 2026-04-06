using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class ActionTagCleanupPhaseConfig
{
    [Tooltip("在该时机执行标签清理")]
    public bool enabled;

    [Tooltip("要清理的 Tag 容器")]
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

    [Tooltip("All：清空该容器。SpecifiedExact：逐项 RemoveTag。SpecifiedFuzzy：逐项 RemoveTagCompletely（子树）。")]
    public ActionTagCleanupMode mode = ActionTagCleanupMode.All;

    [Tooltip("非 All 时：要处理的 Tag 列表")]
    public List<TagReference> specifiedTags = new List<TagReference>();
}

public class ActionTagCleanupClip : PlayableAsset
{
    [Header("Clip 开始时")]
    public ActionTagCleanupPhaseConfig onClipStart = new ActionTagCleanupPhaseConfig();

    [Header("Clip 正常结束")]
    public ActionTagCleanupPhaseConfig onEndFinished = new ActionTagCleanupPhaseConfig();

    [Header("Clip 被中断 / 切走")]
    public ActionTagCleanupPhaseConfig onEndCut = new ActionTagCleanupPhaseConfig();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTagCleanupBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.onClipStart = ClonePhase(onClipStart);
        behaviour.onEndFinished = ClonePhase(onEndFinished);
        behaviour.onEndCut = ClonePhase(onEndCut);
        return playable;
    }

    private static ActionTagCleanupPhaseConfig ClonePhase(ActionTagCleanupPhaseConfig src)
    {
        if (src == null)
            return new ActionTagCleanupPhaseConfig();
        var d = new ActionTagCleanupPhaseConfig
        {
            enabled = src.enabled,
            targetContainer = src.targetContainer,
            mode = src.mode
        };
        if (src.specifiedTags != null)
            d.specifiedTags = new List<TagReference>(src.specifiedTags);
        return d;
    }
}
