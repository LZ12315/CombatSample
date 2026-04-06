using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionTagClip : PlayableAsset
{
    [Tooltip("Tags to add during this clip")]
    public TagReference tag;

    [Tooltip("Which runtime tag container receives this tag.")]
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTagBehaviour>.Create(graph);
        ActionTagBehaviour behaviour = playable.GetBehaviour();
        behaviour.tag = tag;
        behaviour.targetContainer = targetContainer;
        return playable;
    }
}