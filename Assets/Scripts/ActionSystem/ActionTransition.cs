using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActionTransition
{
    [Header("配置")]
    [SerializeField, Tooltip("下一个动作")]
    private ActionAsset targetAction;

    [SerializeField, Tooltip("动作检查模式")]
    private Enums.ActionTransitionMode transitionMode;

    [SerializeReference, SubclassSelector, Tooltip("Transition列表")]
    private List<TransitionCondition> conditions;

    #region 公有属性
    public ActionAsset TargetAction { get => targetAction;}
    public IReadOnlyList<TransitionCondition> Conditions => conditions?.AsReadOnly();
    #endregion

    public void Enable(Actor actor)
    {
        if (conditions.Count == 0) return;

        foreach (var condition in conditions)
            condition.Enable(actor);
    }

    public bool Check()
    {
        if (conditions.Count == 0) return false;

        bool result = true;
        foreach (var condition in conditions)
        {
            if (condition != null)
            {
                result = result && condition.Check();
                if (result && transitionMode == Enums.ActionTransitionMode.AnyTrue)
                    return true;
            }
        }
        return result;
    }

    public void Disable()
    {
        if (conditions.Count == 0) return;

        foreach (var condition in conditions)
            condition.Disable();
    }

    public ActionTransition Clone()
    {
        var clonedConditions = new List<TransitionCondition>();

        foreach (var condition in Conditions)
        {
            if (condition != null)
            {
                var clonedCondition = condition.Clone();
                clonedConditions.Add(clonedCondition);
            }
        }

        return new ActionTransition
        {
            targetAction = this.targetAction,
            transitionMode = this.transitionMode,
            conditions = clonedConditions
        };
    }

}

public static partial class Enums
{
    public enum ActionTransitionMode
    {
        AllTrue, AnyTrue
    }
}
