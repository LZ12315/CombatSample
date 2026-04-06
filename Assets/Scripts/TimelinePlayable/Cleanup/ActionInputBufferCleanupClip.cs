using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionInputBufferCleanupClip : PlayableAsset
{
    [Header("Clip 开始时")]
    public bool clearOnClipStart;

    [Header("Clip 正常结束")]
    public bool clearOnEndFinished;

    [Header("Clip 被中断 / 切走")]
    public bool clearOnEndCut;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionInputBufferCleanupBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.clearOnClipStart = clearOnClipStart;
        behaviour.clearOnEndFinished = clearOnEndFinished;
        behaviour.clearOnEndCut = clearOnEndCut;
        return playable;
    }
}
