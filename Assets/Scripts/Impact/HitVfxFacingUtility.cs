using UnityEngine;

/// <summary>
/// 解析「面向出手者」时使用的世界空间参考点。
/// 默认：本次命中的 <see cref="ActorCombater"/> 上 CharacterController 的几何中心（与谁在播 Timeline 一致）；
/// 无 CC 则用攻击者根位置；攻击者为空时回退 MainCamera。
/// </summary>
public static class HitVfxFacingUtility
{
    /// <summary>
    /// override 非空用其 position；否则用攻击者的 CC 中心（见 <see cref="GetAttackerFacingReferenceWorldPosition"/>）。
    /// </summary>
    public static Vector3 ResolveFacingWorldPosition(Transform overrideTransform, ActorCombater attacker)
    {
        if (overrideTransform != null)
            return overrideTransform.position;
        return GetAttackerFacingReferenceWorldPosition(attacker);
    }

    public static Vector3 GetAttackerFacingReferenceWorldPosition(ActorCombater attacker)
    {
        if (attacker == null)
        {
            if (Camera.main != null)
                return Camera.main.transform.position;
            return Vector3.zero;
        }

        if (TryCharacterControllerCenter(attacker.transform, out Vector3 p))
            return p;

        return attacker.transform.position;
    }

    static bool TryCharacterControllerCenter(Transform root, out Vector3 world)
    {
        var cap = root.GetComponent<CapsuleCollider>()
                  ?? root.GetComponentInChildren<CapsuleCollider>();
        if (cap != null)
        {
            world = cap.transform.TransformPoint(cap.center);
            return true;
        }

        var cc = root.GetComponent<CharacterController>()
                 ?? root.GetComponentInChildren<CharacterController>();
        if (cc != null)
        {
            world = cc.transform.TransformPoint(cc.center);
            return true;
        }

        world = default;
        return false;
    }
}
