using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ActionTagBehaviour : ActionBehaviourBase
{
    [Tooltip("Tags to add while this plays")]
    public TagReference tag;

    // 当 Clip 真正开始播放时候
    protected override void OnClipStart(Playable playable)
    {
        // 修复点 1：把 tag == null 改成了 tag != null
        if (actor != null && actor.tagContainer != null && tag != null)
        {
            // 修复点 2：从 TagReference 提取真正的 Tag 对象
            Tag tagObj = tag.GetTag();
            if (tagObj != null)
            {
                // 修复点 3：传入真正的 Tag 对象
                actor.tagContainer.AddTag(tagObj);
                
                // Debug.Log($"[Timeline] 发放标签: {tagObj.FullTagName}");
            }
        }
    }

    // 当 Clip 结束时（无论是正常结束，还是被强行打断）
    protected override void OnClipStop(bool isNormal)
    {
        if (actor != null && actor.tagContainer != null && tag != null)
        {
            Tag tagObj = tag.GetTag();
            if (tagObj != null)
            {
                // 回收标签
                actor.tagContainer.RemoveTag(tagObj);
                
                // Debug.Log($"[Timeline] 回收标签: {tagObj.FullTagName} | 是否正常结束: {isNormal}");
            }
        }
    }
}