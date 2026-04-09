using System;
using UnityEngine;
using DeiveEx.TagTree;

public class ActionInstance
{
    public ActionAsset Config { get; }

    public ActionData RuntimeData { get; private set; }

    /// <summary>事件触发时携带的上下文参数，轮询触发时为 default。</summary>
    public ActionEventContext EventContext { get; private set; }

    private Actor _actor;

    public ActionInstance(ActionAsset config)
    {
        Config = config;
        ResetRuntimeData();
    }

    public void OnEnter(Actor actor, ActionEventContext context = default)
    {
        _actor = actor;
        EventContext = context;

        var selfTags = Config.SelfTags;
        if (_actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var binding = selfTags[i];
                if (binding?.tag == null) continue;
                Tag tagObj = binding.tag.GetTag();
                if (tagObj != null)
                    _actor.AddTag(tagObj, binding.targetContainer);
            }
        }
    }

    public void OnExit()
    {
        var selfTags = Config.SelfTags;
        if (_actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var binding = selfTags[i];
                if (binding?.tag == null) continue;
                Tag tagObj = binding.tag.GetTag();
                if (tagObj != null)
                    _actor.RemoveTag(tagObj, binding.targetContainer);
            }
        }

        _actor = null;
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
