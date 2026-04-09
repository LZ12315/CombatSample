using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionInputBufferCleanupClip : PlayableAsset
{
[Header("On Clip Start")]
    public bool clearOnClipStart;

[Header("On Clip End (Finished)")]
    public bool clearOnEndFinished;

[Header("On Clip End (Interrupted)")]
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
