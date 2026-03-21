using System;
using UnityEngine;
using DeiveEx.TagTree;

public class ActionInstance
{
    public ActionAsset Config { get; }

    public ActionData RuntimeData { get; private set; }

    private Actor _actor;

    public ActionInstance(ActionAsset config)
    {
        Config = config;
        ResetRuntimeData();
    }

    public void OnEnter(Actor actor)
    {
        _actor = actor;

        // ���� SelfTag
        if (_actor != null && _actor.tagContainer != null && Config.SelfTag != null)
        {
            Tag selfTagObj = Config.SelfTag.GetTag();
            if (selfTagObj != null)
            {
                _actor.tagContainer.AddTag(selfTagObj);
            }
        }
        
        // Enter ����
        ExecuteCleanup(Config.cleanupOnEnter);
    }

    public void OnExit()
    {
        // �Ƴ� SelfTag
        if (_actor != null && _actor.tagContainer != null && Config.SelfTag != null)
        {
            Tag selfTagObj = Config.SelfTag.GetTag();
            if (selfTagObj != null)
            {
                _actor.tagContainer.RemoveTag(selfTagObj);
            }
        }
        
        // Exit ����
        ExecuteCleanup(Config.cleanupOnExit);

        _actor = null;
    }
    
    private void ExecuteCleanup(Enums.CleanupTarget target)
    {
        if (target == Enums.CleanupTarget.None || _actor == null) return;
        
        if ((target & Enums.CleanupTarget.Input) != 0)
            _actor.logicInput?.ClearBuffer();
        
        if ((target & Enums.CleanupTarget.Tags) != 0)
            _actor.tagContainer?.ClearTags();
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
