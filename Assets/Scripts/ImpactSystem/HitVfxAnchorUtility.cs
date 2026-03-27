using System;
using UnityEngine;

/// <summary>
/// VFX 生成点：从 rayOrigin 指向目标 Collider 包围盒中心做射线，取与目标 Collider 的第一个有效交点。
/// 失败时依次 fallback：目标 Collider 上 ClosestPoint(攻击者根位置)、物理 HitPoint。
/// </summary>
public static class HitVfxAnchorUtility
{
    const float RayOriginPush = 0.08f;
    const float RayExtraLength = 0.75f;

    /// <summary>
    /// 默认射线起点：CharacterController 或 CapsuleCollider 的 world bounds 竖直方向约 3/4 高处
    /// （水平用 bounds 中心）。无碰撞体则根节点上移约 1.5m。
    /// </summary>
    public static Vector3 GetDefaultAttackerRayOrigin(ActorCombater attacker)
    {
        if (attacker == null) return Vector3.zero;

        Transform t = attacker.transform;

        var cc = attacker.GetComponent<CharacterController>()
                 ?? attacker.GetComponentInParent<CharacterController>();
        if (cc != null)
            return PointAtHeightFraction(cc.bounds, 0.75f);

        var cap = attacker.GetComponent<CapsuleCollider>()
                  ?? attacker.GetComponentInParent<CapsuleCollider>();
        if (cap != null)
            return PointAtHeightFraction(cap.bounds, 0.75f);

        return t.position + Vector3.up * 1.5f;
    }

    static Vector3 PointAtHeightFraction(Bounds b, float fraction)
    {
        float y = b.min.y + b.size.y * Mathf.Clamp01(fraction);
        return new Vector3(b.center.x, y, b.center.z);
    }

    /// <summary>目标参考点：首个子 Collider 包围盒中心，否则根位置。</summary>
    public static Vector3 GetTargetReferenceWorldPosition(GameObject targetObject)
    {
        if (targetObject == null) return Vector3.zero;

        var col = targetObject.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return targetObject.transform.position;
    }

    public static Vector3 ComputeVfxSpawnPoint(
        Vector3 rayOriginWorld,
        ActorCombater attacker,
        GameObject targetObject,
        Vector3 hitPointFallback)
    {
        if (attacker == null || targetObject == null)
            return hitPointFallback;

        var col = targetObject.GetComponentInChildren<Collider>();
        if (col == null)
            return hitPointFallback;

        Vector3 targetCenter = col.bounds.center;
        Vector3 toCenter = targetCenter - rayOriginWorld;
        if (toCenter.sqrMagnitude < 1e-8f)
            return FallbackClosestToAttacker(col, attacker, hitPointFallback);

        Vector3 dir = toCenter.normalized;
        float maxDist = toCenter.magnitude + RayExtraLength;
        Vector3 rayOrigin = rayOriginWorld + dir * RayOriginPush;

        var hits = Physics.RaycastAll(
            rayOrigin,
            dir,
            maxDist,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsUnderAttacker(h.collider, attacker))
                    continue;
                if (IsUnderTarget(h.collider, targetObject))
                    return h.point;
            }
        }

        return FallbackClosestToAttacker(col, attacker, hitPointFallback);
    }

    /// <summary>
    /// 屏幕语义点：从主摄像机朝目标参考点做射线，取目标表面命中点。
    /// 失败时 fallback 到 hitPointFallback。
    /// </summary>
    public static Vector3 ComputeScreenPointFromCamera(GameObject targetObject, Vector3 hitPointFallback)
    {
        if (targetObject == null)
            return hitPointFallback;

        var col = targetObject.GetComponentInChildren<Collider>();
        if (col == null)
            return hitPointFallback;

        if (!TryGetPrimaryCameraWorldPosition(out Vector3 camWorld))
            return hitPointFallback;

        Vector3 targetRef = GetTargetReferenceWorldPosition(targetObject);
        Vector3 toTarget = targetRef - camWorld;
        if (toTarget.sqrMagnitude < 1e-8f)
            return FallbackClosestToWorldPosition(col, camWorld, hitPointFallback);

        Vector3 dir = toTarget.normalized;
        float maxDist = toTarget.magnitude + RayExtraLength;
        Vector3 rayOrigin = camWorld + dir * RayOriginPush;

        var hits = Physics.RaycastAll(
            rayOrigin,
            dir,
            maxDist,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsUnderTarget(h.collider, targetObject))
                    return h.point;
            }
        }

        return FallbackClosestToWorldPosition(col, camWorld, hitPointFallback);
    }

    static Vector3 FallbackClosestToAttacker(Collider col, ActorCombater attacker, Vector3 hitPointFallback)
    {
        Vector3 p = col.ClosestPoint(attacker.transform.position);
        if ((p - attacker.transform.position).sqrMagnitude > 1e-10f)
            return p;
        return hitPointFallback;
    }

    static Vector3 FallbackClosestToWorldPosition(Collider col, Vector3 worldPosition, Vector3 hitPointFallback)
    {
        Vector3 p = col.ClosestPoint(worldPosition);
        if ((p - worldPosition).sqrMagnitude > 1e-10f)
            return p;
        return hitPointFallback;
    }

    static bool IsUnderTarget(Collider c, GameObject targetRoot)
    {
        Transform t = c.transform;
        return t == targetRoot.transform || t.IsChildOf(targetRoot.transform);
    }

    static bool IsUnderAttacker(Collider c, ActorCombater attacker)
    {
        Transform t = c.transform;
        Transform root = attacker.transform;
        return t == root || t.IsChildOf(root);
    }

    static bool TryGetPrimaryCameraWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        if (Camera.main != null && Camera.main.enabled)
        {
            worldPosition = Camera.main.transform.position;
            return true;
        }

        var tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged != null)
        {
            var cam = tagged.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                worldPosition = cam.transform.position;
                return true;
            }
        }

        var any = UnityEngine.Object.FindObjectOfType<Camera>();
        if (any != null && any.enabled)
        {
            worldPosition = any.transform.position;
            return true;
        }

        return false;
    }
}
