using System.Collections.Generic;
using UnityEngine;
using Animancer; // 引入 Animancer

[System.Obsolete("LocomotionModeAsset 已被 ActionAsset 条件系统取代。")]
[CreateAssetMenu(fileName = "NewLocomotionMode", menuName = "ActionSystem/LocomotionModeAsset")]
public class LocomotionModeAsset : ScriptableObject
{
    [Header("Settings")]
    [Tooltip("2D blend tree or transition for this mode")]
    public TransitionAsset Mixer;

    [SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this mode can run.")]
    private List<ActionCondition> Conditions = new List<ActionCondition>();
        
    [Header("Properties")]
    [SerializeField, Tooltip("Action priority")]
    public Enums.ActionPriority Priority = Enums.ActionPriority.Normal;

    [Tooltip("Blend time into this motion")]
    public float FadeTime = 0.2f;

    [Tooltip("Speed scale in this pose")]
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