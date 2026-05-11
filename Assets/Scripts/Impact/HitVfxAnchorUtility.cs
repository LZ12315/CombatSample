using UnityEngine;

/// <summary>
/// VFX 锚点工具：提供攻击者参考点、相机偏移等辅助方法。
/// 注意：旧版 "ComputeVfxSpawnPoint" 射线法已移除，现推荐直接用命中接触点或碰撞体中心。
/// </summary>
public static class HitVfxAnchorUtility
{
    const float CameraLateralBiasScale = 0.35f;
    const float CameraLateralBiasMin = 0.05f;
    const float CameraLateralBiasMax = 0.22f;

    /// <summary>
    /// 默认射线起点：CharacterController 或 CapsuleCollider 的 world bounds 竖直方向约 3/4 高处
    /// （水平用 bounds 中心）。无碰撞体则根节点上移约 1.5m。
    /// </summary>
    public static Vector3 GetDefaultAttackerRayOrigin(ActorCombater attacker)
    {
        if (attacker == null) return Vector3.zero;

        Transform t = attacker.transform;

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

    /// <summary>
    /// 在 attacker->target 的表面交点基础上，沿相机所在的左右侧方向做水平偏移。
    /// 只修正横向读感，不引入相机俯仰导致的高度漂移。
    /// </summary>
    public static Vector3 ApplyCameraLateralBias(
        Vector3 basePointWorld,
        Vector3 attackerReferenceWorld,
        GameObject targetObject)
    {
        if (targetObject == null)
            return basePointWorld;

        if (!TryGetPrimaryCameraTransform(out Transform camTransform))
            return basePointWorld;

        Vector3 targetReferenceWorld = GetTargetReferenceWorldPosition(targetObject);
        Vector3 attackDirHorizontal = Vector3.ProjectOnPlane(targetReferenceWorld - attackerReferenceWorld, Vector3.up);
        if (attackDirHorizontal.sqrMagnitude < 1e-8f)
            return basePointWorld;
        attackDirHorizontal.Normalize();

        Vector3 toCameraHorizontal = Vector3.ProjectOnPlane(camTransform.position - basePointWorld, Vector3.up);
        if (toCameraHorizontal.sqrMagnitude < 1e-8f)
            return basePointWorld;

        Vector3 lateralDirection = Vector3.ProjectOnPlane(toCameraHorizontal, attackDirHorizontal);
        if (lateralDirection.sqrMagnitude < 1e-8f)
        {
            lateralDirection = Vector3.ProjectOnPlane(camTransform.right, Vector3.up);
            if (lateralDirection.sqrMagnitude < 1e-8f)
                return basePointWorld;
        }

        lateralDirection.Normalize();
        float offsetDistance = ComputeCameraLateralBiasDistance(targetObject);
        return basePointWorld + lateralDirection * offsetDistance;
    }

    static float ComputeCameraLateralBiasDistance(GameObject targetObject)
    {
        var col = targetObject != null ? targetObject.GetComponentInChildren<Collider>() : null;
        if (col == null)
            return CameraLateralBiasMin;

        float targetRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
        return Mathf.Clamp(targetRadius * CameraLateralBiasScale, CameraLateralBiasMin, CameraLateralBiasMax);
    }

    static bool TryGetPrimaryCameraTransform(out Transform cameraTransform)
    {
        cameraTransform = null;
        if (Camera.main != null && Camera.main.enabled)
        {
            cameraTransform = Camera.main.transform;
            return true;
        }

        var tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged != null)
        {
            var cam = tagged.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                cameraTransform = cam.transform;
                return true;
            }
        }

        var any = UnityEngine.Object.FindObjectOfType<Camera>();
        if (any != null && any.enabled)
        {
            cameraTransform = any.transform;
            return true;
        }

        return false;
    }
}
