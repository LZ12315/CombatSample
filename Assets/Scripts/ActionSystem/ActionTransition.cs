using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActionTransition
{
    public ActionAsset targetAction;
    public Enums.ActionTransitionMode transitionMode;
    public ConditionList condition;

    public void Enable()
    {

    }

    public bool Check()
    {
        return condition.Check();
    }

    public void Disable()
    {

    }

}

[Serializable]
public class ConditionList
{
    [SerializeReference]
    public List<TransitionCondition> conditions = new List<TransitionCondition>();

    public bool Check()
    {
        if (conditions.Count == 0) return false;

        bool result = true;
        foreach (var condition in conditions)
        {
            if (condition != null)
            {
                result = result && condition.Check();
            }
        }
        return result;
    }
}

public static partial class Enums
{
    public enum ActionTransitionMode
    {
        All, Any
    }
}
