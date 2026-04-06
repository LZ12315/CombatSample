using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionTagBehaviour : ActionBehaviourBase
{
    [Tooltip("Tags to add while this plays")]
    public TagReference tag;
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

    // 当 Clip 真正开始播放时候
    protected override void OnClipStart(Playable playable)
    {
        if (actor != null && tag != null)
        {
            Tag tagObj = tag.GetTag();
            if (tagObj != null)
            {
                actor.AddTag(tagObj, targetContainer);
            }
        }
    }

    // 当 Clip 结束时（无论是正常结束，还是被强行打断）
    protected override void OnClipStop(bool isNormal)
    {
        if (actor != null && tag != null)
        {
            Tag tagObj = tag.GetTag();
            if (tagObj != null)
            {
                actor.RemoveTag(tagObj, targetContainer);
            }
        }
    }
}