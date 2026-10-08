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

    /// <summary>
    /// 计算 Actor 到解析目标的距离（可选仅水平面）。
    /// </summary>
    public static bool TryComputeDistanceToTarget(
        Actor actor,
        BBParameter<Transform> targetOverride,
        bool horizontalOnly,
        out float distance)
    {
        distance = 0f;
        if (actor == null)
            return false;

        if (!TryResolveTarget(actor, targetOverride, out Transform target))
            return false;

        Vector3 delta = target.position - actor.transform.position;
        if (horizontalOnly)
            delta.y = 0f;

        distance = delta.magnitude;
        return true;
    }
}
