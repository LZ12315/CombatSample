using System;
using UnityEngine;

[Serializable]
public abstract class ActionCondition
{
    [Tooltip("如果勾选，则结果取反")]
    public bool invertResult = false;
    
    [Tooltip("如果勾选，只要本条件为 True，直接无视其他所有条件强行通过")]
    public bool overrideAll = false;

    /// <summary>
    /// 外部调用的统一检票入口 (直接由大脑传入 actor 进行瞬时判断)
    /// </summary>
    public bool Check(Actor actor)
    {
        if (actor == null) return false;

        // 1. 传入 actor，获取子类真实的检测结果
        bool rawResult = OnCheck(actor);

        // 2. 根据反转配置返回最终结果
        return invertResult ? !rawResult : rawResult;
    }

    /// <summary>
    /// 子类必须实现的具体检测逻辑
    /// </summary>
    protected abstract bool OnCheck(Actor actor);
}