using System;
using UnityEngine;

[Serializable]
public abstract class ActionCondition
{
    [Tooltip("If on, flip the result")]
    public bool invertResult = false;
    
    [Tooltip("If on and this is True, skip all other checks and pass.")]
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

    /// <summary>
    /// 胜选后回调：用于带有"消费型"语义的条件（例如 InputSequenceCondition 要把命中的输入标记为已消费），
    /// 避免同一条输入被后续 Action 重复判定通过（例如一段跳→二段跳自动连触）。
    /// 由 <see cref="ActionAsset.ClaimEntry"/> 在 ActionStateManager 真正选中此 Action 并决定进入时调用。
    /// 默认空实现；绝大多数纯只读条件无需重写。
    /// </summary>
    public virtual void OnClaim(Actor actor) { }
}