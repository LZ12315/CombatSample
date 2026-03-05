using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputSequenceCondition : ActionCondition
{
    [Header("配置")]
    [Tooltip("要求完成整个序列的最大时间窗口")]
    [SerializeField]
    private float maxTimeWindow = 0.4f;
    
    [SerializeReference, SubclassSelector]
    private List<InputCheckBase> inputSequence = new List<InputCheckBase>();

    // 删除了所有的状态变量：waitCounter, checkIndex
    // 删除了所有的生命周期：OnEnable, OnDisable, GetInput

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || inputSequence == null || inputSequence.Count == 0) 
            return false;

        // 【核心改变】：条件本身不再记录按键，而是去查 Actor 身上的“输入历史记录”
        // 注意：这需要你在 Actor 身上实现一个 InputBuffer 或类似历史记录的组件
        //if (actor.inputBuffer != null)
        //{
            // 向缓存器发起查询：在规定的时间内，是否完成了这套连招？
        //    return actor.inputBuffer.HasCompletedSequence(inputSequence, maxTimeWindow);
        //}

        return false;
    }
}