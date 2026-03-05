using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionTagClip : PlayableAsset
{
    [Tooltip("想要在这个时间段内激活的标签")]
    public TagReference tag;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTagBehaviour>.Create(graph);
        ActionTagBehaviour behaviour = playable.GetBehaviour();
        behaviour.tag = this.tag;
        return playable;
    }
}