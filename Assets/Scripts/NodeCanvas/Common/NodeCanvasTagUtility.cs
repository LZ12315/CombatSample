using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// ActionAsset SelfTags 与 <see cref="TagReference"/> 的匹配工具。
/// </summary>
public static class NodeCanvasTagUtility
{
    public static bool ActionHasSelfTag(ActionAsset action, TagReference requiredTagRef, ActorTagMatchMode matchMode)
    {
        if (action == null || requiredTagRef == null)
            return false;

        Tag required = requiredTagRef.GetTag();
        if (required == null)
            return false;

        var selfTags = action.SelfTags;
        if (selfTags == null) return false;

        for (int i = 0; i < selfTags.Count; i++)
        {
            var refItem = selfTags[i];
            if (refItem == null) continue;
            Tag self = refItem.GetTag();
            if (self == null) continue;

            if (matchMode == ActorTagMatchMode.Exact)
            {
                if (self.Id == required.Id)
                    return true;
            }
            else
            {
                if (self.Matches(required))
                    return true;
            }
        }

        return false;
    }
}
