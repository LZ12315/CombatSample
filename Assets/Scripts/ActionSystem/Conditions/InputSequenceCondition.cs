using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputSequenceCondition : ActionCondition
{
    [Header("Settings")]
    [Tooltip("Max time gap between inputs")]
    [SerializeField]
    private float maxInputInterval = 0.25f;
    
    // 你的多态配置列表
    [SerializeReference, SubclassSelector]
    private List<InputCheckBase> inputSequence = new List<InputCheckBase>();

    protected override bool OnCheck(Actor actor)
    {
        // 防呆检查
        if (actor == null || inputSequence == null || inputSequence.Count == 0) 
            return false;

        // 获取底层的只读录像带
        var buffer = actor.logicInput.InputBuffer;
        if (buffer.Count == 0) return false;

        // 从最新的需要的指令开始往前匹配（例如：↓ ↘ → [轻击]，我们先找 [轻击]）
        int seqIndex = inputSequence.Count - 1; 
        
        // 记录上一个成功匹配的按键的时间戳。初始化为 -1 表示还没找到第一个匹配项
        float lastMatchedTime = -1f;

        // 时光倒流：从录像带的最后一帧（最新输入）往前倒推
        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            var bufferItem = buffer[i];

            // 【核心规则】：检查输入间隔
            // 如果我们已经找到了后面的按键（lastMatchedTime >= 0），
            // 就需要确保当前遍历的这个历史按键，离它不能太远！
            // (注意：lastMatchedTime 在时间轴上比 bufferItem.Timestamp 更晚/更大，所以是相减)
            if (lastMatchedTime >= 0f && (lastMatchedTime - bufferItem.Timestamp > maxInputInterval))
            {
                // 间隔太大了！因为录像带是按时间排序的，再往前找只会间隔更大，直接判定搓招失败。
                return false; 
            }

            // 【判定规则】：调用你配置的多态检查器
            if (inputSequence[seqIndex].CheckInput(bufferItem.Data))
            {
                // 匹配成功！更新时间锚点，用来约束下一个要找的按键
                lastMatchedTime = bufferItem.Timestamp;
                
                // 指针前移，去寻找序列中的上一个指令
                seqIndex--;

                // 如果整个序列都找齐了！
                if (seqIndex < 0) 
                {
                    // 🌟 纯粹的只读判定，绝不在这里吃指令！返回 true 进入候选名单！
                    return true;
                }
            }
            // (如果没匹配上，继续往前找容错的“脏输入”，只要不超时就行)
        }

        // 录像带翻完了，但需要的指令序列没找齐
        return false;
    }
}