using System;
using UnityEngine;
using DeiveEx.TagTree;

public class ActionInstance
{
    public ActionAsset Config { get; }

    public ActionData RuntimeData { get; private set; }

    /// <summary>本次 Action 开始时的上下文快照；无快照需求时为 default。</summary>
    public ActionContext Context { get; private set; }

    /// <summary>当前持有此 ActionInstance 的 Actor，OnEnter 时赋值，OnExit 时清空。</summary>
    public Actor Actor { get; private set; }

    public ActionInstance(ActionAsset config)
    {
        Config = config;
        ResetRuntimeData();
    }

    public void OnEnter(Actor actor, ActionContext context)
    {
        Actor = actor;
        Context = context;

        var selfTags = Config.SelfTags;
        if (Actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var tagRef = selfTags[i];
                if (tagRef == null) continue;
                Tag tagObj = tagRef.GetTag();
                if (tagObj != null)
                    Actor.AddTag(tagObj, ActorTagContainerType.Transient);
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
                var tagRef = selfTags[i];
                if (tagRef == null) continue;
                Tag tagObj = tagRef.GetTag();
                if (tagObj != null)
                    Actor.RemoveTag(tagObj, ActorTagContainerType.Transient);
            }
        }

        Actor = null;
        Context = default;
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
