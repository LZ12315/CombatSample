using System;
using UnityEngine;
using DeiveEx.TagTree; // 引入标签插件命名空间

[Serializable]
public class TagCondition : ActionCondition
{
    [Tooltip("需要在黑板上检测的标签")]
    public TagReference requiredTag; // 在 Inspector 面板上供策划下拉选择

    // 注意：这里重写的是你在基类里定义好的无状态 OnCheck(Actor actor)
    protected override bool OnCheck(Actor actor)
    {
        // 防呆保护：如果角色没配置黑板，或者策划忘了选标签，直接不通过
        if (actor == null || actor.tagContainer == null || requiredTag == null)
        {
            return false;
        }

        // 1. 从 TagReference 中提取出真正的、底层的 Tag 对象
        Tag tagObj = requiredTag.GetTag(); 
        
        // 如果提取出来的为空（可能标签在设置里被删了），不通过
        if (tagObj == null)
        {
            return false;
        }

        // 2. 将真正的 Tag 对象传给 TagContainer 的 HasTag 方法进行极速模糊匹配
        return actor.tagContainer.HasTag(tagObj); 
    }
}