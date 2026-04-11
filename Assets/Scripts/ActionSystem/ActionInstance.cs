using System;
using UnityEngine;
using DeiveEx.TagTree;

public class ActionInstance
{
    public ActionAsset Config { get; }

    public ActionData RuntimeData { get; private set; }

    /// <summary>本次 Action 开始时的上下文快照；无快照需求时为 default。</summary>
    public ActionEventContext EventContext { get; private set; }

    /// <summary>当前持有此 ActionInstance 的 Actor，OnEnter 时赋值，OnExit 时清空。</summary>
    public Actor Actor { get; private set; }

    public ActionInstance(ActionAsset config)
    {
        Config = config;
        ResetRuntimeData();
    }

    public void OnEnter(Actor actor, ActionEventContext context = default)
    {
        Actor = actor;
        EventContext = context;

        var selfTags = Config.SelfTags;
        if (Actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var binding = selfTags[i];
                if (binding?.tag == null) continue;
                Tag tagObj = binding.tag.GetTag();
                if (tagObj != null)
                    Actor.AddTag(tagObj, binding.targetContainer);
            }
        }
    }

    public void OnExit()
    {
        var selfTags = Config.SelfTags;
        if (Actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var binding = selfTags[i];
                if (binding?.tag == null) continue;
                Tag tagObj = binding.tag.GetTag();
                if (tagObj != null)
                    Actor.RemoveTag(tagObj, binding.targetContainer);
            }
        }

        Actor = null;
        EventContext = default;
    }

    public void UpdateNormalizedTime(double normalizedTime)
    {
        var currentData = RuntimeData;
        currentData.normalizedTime = normalizedTime;
        RuntimeData = currentData;
    }

    public void ResetRuntimeData()
    {
        RuntimeData = new ActionData
        {
            normalizedTime = 0
        };
    }
}
