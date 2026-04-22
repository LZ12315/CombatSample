using UnityEngine;

/// <summary>
/// 世界方向命中特效：按世界空间主方向生成旋转，并绕该轴 roll。
/// </summary>
public static class WorldDirectionalImpactRotationResolver
{
    static bool _hasWarnedMissingCamera;

    public static Quaternion Resolve(
        WorldDirectionMode directionMode,
        VFXRollMode rollMode,
        float rollPresetDegrees,
        float rollRandomRangeDegrees,
        ImpactData impact)
    {
        Vector3 forward = ResolveForward(directionMode, impact);
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            up = Vector3.right;

        Quaternion baseRot = Quaternion.LookRotation(forward, up);
        return ImpactRotationUtility.ApplyRollAroundAxis(baseRot, forward, rollMode, rollPresetDegrees, rollRandomRangeDegrees);
    }

    static Vector3 ResolveForward(WorldDirectionMode mode, ImpactData impact)
    {
        Vector3 impactDir = GetImpactDirection(impact);

        switch (mode)
        {
            case WorldDirectionMode.FromAttackerToTarget:
                return impactDir;
            case WorldDirectionMode.FromTargetToAttacker:
                return -impactDir;
            case WorldDirectionMode.WorldUp:
                return Vector3.up;
            case WorldDirectionMode.CameraForward:
                if (TryGetPrimaryCamera(out Camera cam))
                    return cam.transform.forward;
                if (!_hasWarnedMissingCamera)
                {
                    _hasWarnedMissingCamera = true;
                    Debug.LogWarning("WorldDirectionalImpactRotationResolver.CameraForward: no enabled camera. Using world +Z.");
                }

                return Vector3.forward;
            default:
                return Vector3.forward;
        }
    }

    static Vector3 GetImpactDirection(ImpactData impact)
    {
        if (impact == null)
            return Vector3.forward;

        if (impact.ImpactDirectionWorld.sqrMagnitude > 1e-8f)
            return impact.ImpactDirectionWorld;

        Vector3 fromRefs = impact.TargetWorldPosition - impact.AttackerWorldPosition;
        if (fromRefs.sqrMagnitude > 1e-8f)
            return fromRefs.normalized;

        return Vector3.forward;
    }

    static bool TryGetPrimaryCamera(out Camera camera)
    {
        camera = null;
        if (Camera.main != null && Camera.main.enabled)
        {
            camera = Camera.main;
            return true;
        }

        var tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged != null)
        {
            var cam = tagged.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                camera = cam;
                return true;
            }
        }

        var any = Object.FindObjectOfType<Camera>();
        if (any != null && any.enabled)
        {
            camera = any;
            return true;
        }

        return false;
    }
}
