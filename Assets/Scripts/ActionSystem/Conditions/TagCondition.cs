using System;
using UnityEngine;
using DeiveEx.TagTree; // 引入标签插件命名空间

[Serializable]
public class TagCondition : ActionCondition
{
    [Tooltip("Tag to read from the board")]
    public TagReference requiredTag; // 在 Inspector 面板上供策划下拉选择

    [Tooltip("Which runtime tag container should be queried.")]
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

    [Tooltip("Exact only matches the leaf tag itself. Fuzzy allows parent tags to match children.")]
    public ActorTagMatchMode matchMode = ActorTagMatchMode.Fuzzy;

    // 注意：这里重写的是你在基类里定义好的无状态 OnCheck(Actor actor)
    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || requiredTag == null)
        {
            return false;
        }

        Tag tagObj = requiredTag.GetTag(); 
        if (tagObj == null)
        {
            return false;
        }

        return actor.HasTag(tagObj, targetContainer, matchMode);
    }
}