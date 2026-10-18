using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class ActionCondition
{
    [Tooltip("Flip a state check. Input event/state conditions use explicit selections instead.")]
    public bool invertResult;

    public virtual bool IsConfigurationValid => true;
    public virtual bool SupportsInversion => true;
    public virtual bool RequiresPlayerInput => false;
    public virtual bool ConsumesInput => false;

    public bool Check(Actor actor) => Check(actor, ActionContext.ForSelf(actor));

    public bool Check(Actor actor, ActionContext context) => TryEvaluate(actor, context, out _);

    internal virtual bool TryEvaluate(Actor actor, ActionContext context, out PlayerInputMatch input)
    {
        input = default;
        if (actor == null || !IsConfigurationValid || (invertResult && !SupportsInversion)
            || !CanEvaluate(actor)) return false;
        bool result = OnCheck(actor, context);
        return invertResult ? !result : result;
    }

    internal static bool TryValidateAll(IReadOnlyList<ActionCondition> conditions,
        bool skipInput, out int invalidIndex, bool exit = false)
    {
        invalidIndex = -1;
        for (int i = 0; conditions != null && i < conditions.Count; i++)
        {
            ActionCondition condition = conditions[i];
            if (condition != null && skipInput && condition.RequiresPlayerInput) continue;
            if (condition != null && condition.IsConfigurationValid
                && (!condition.invertResult || condition.SupportsInversion)
                && (!exit || !condition.ConsumesInput)) continue;
            invalidIndex = i;
            return false;
        }
        return true;
    }

    protected virtual bool CanEvaluate(Actor actor) => true;
    protected abstract bool OnCheck(Actor actor);
    protected virtual bool OnCheck(Actor actor, ActionContext context) => OnCheck(actor);
}
