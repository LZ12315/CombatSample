using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class ActionTagCleanupPhaseConfig
{
[Tooltip("Enable tag cleanup at this phase")]
    public bool enabled;

[Tooltip("Which tag container to clean")]
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

[Tooltip("All: clear entire container. SpecifiedExact: remove each tag. SpecifiedFuzzy: remove each tag and its subtree.")]
    public ActionTagCleanupMode mode = ActionTagCleanupMode.All;

[Tooltip("Tags to process (ignored when mode is All)")]
    public List<TagReference> specifiedTags = new List<TagReference>();
}

public class ActionTagCleanupClip : PlayableAsset
{
[Header("On Clip Start")]
    public ActionTagCleanupPhaseConfig onClipStart = new ActionTagCleanupPhaseConfig();

[Header("On Clip End (Finished)")]
    public ActionTagCleanupPhaseConfig onEndFinished = new ActionTagCleanupPhaseConfig();

[Header("On Clip End (Interrupted)")]
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
