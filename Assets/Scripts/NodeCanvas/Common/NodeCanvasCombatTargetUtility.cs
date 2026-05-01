using NodeCanvas.Framework;
using UnityEngine;

/// <summary>
/// NodeCanvas 战斗目标解析：黑板覆盖优先，否则 <see cref="ActorCombater.CombatTarget"/>。
/// </summary>
public static class NodeCanvasCombatTargetUtility
{
    public static bool TryResolveTarget(Actor actor, BBParameter<Transform> targetOverride, out Transform target)
    {
        target = targetOverride != null ? targetOverride.value : null;
        if (target != null)
            return true;

        if (actor != null && actor.combater != null && actor.combater.CombatTarget != null)
        {
            target = actor.combater.CombatTarget.transform;
            return true;
        }

        target = null;
        return false;
    }
}
