using System.Collections.Generic;
using UnityEngine;
using Animancer; // 引入 Animancer

[CreateAssetMenu(fileName = "NewLocomotionMode", menuName = "ActionSystem/LocomotionModeAsset")]
public class LocomotionModeAsset : ScriptableObject
{
    [Header("配置")]
    [Tooltip("该移动模式对应的 2D 混合树或动画过渡")]
    public TransitionAsset Mixer;

    [SerializeReference, SubclassSelector, Tooltip("必须满足列表里【所有】条件，模式才会被系统选中")]
    private List<ActionCondition> Conditions = new List<ActionCondition>();
        
    [Header("属性")]
    [SerializeField, Tooltip("动作优先级")]
    public Enums.ActionPriority Priority = Enums.ActionPriority.Normal;

    [Tooltip("切入该动画的平滑融合时间")]
    public float FadeTime = 0.2f;

    [Tooltip("该姿态下的速度倍率")]
    public float SpeedMultiplier = 1.0f;

    // 检查条件是否全部满足
    public bool CheckConditions(Actor actor)
    {
        if (Conditions == null || Conditions.Count == 0) return false; // 如果没配条件，默认不满足（默认移动不需要条件）
        
        foreach (var condition in Conditions)
        {
            if (!condition.Check(actor)) return false;
        }
        return true;
    }
}