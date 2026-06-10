using UnityEngine;

/// <summary>
/// 命中特效旋转工具：提供绕任意轴的 roll 叠加，以及相机查询共享方法。
/// </summary>
public static class ImpactRotationUtility
{
    public static Quaternion ApplyRollAroundAxis(
        Quaternion baseRotation,
        Vector3 axis,
        VFXRollMode rollMode,
        float presetDegrees,
        float randomRangeDegrees)
    {
        if (axis.sqrMagnitude < 1e-8f)
            return baseRotation;
        axis.Normalize();

        if (rollMode == VFXRollMode.Preset)
            return baseRotation * Quaternion.AngleAxis(presetDegrees, axis);
        if (rollMode == VFXRollMode.Random && randomRangeDegrees > 0.001f)
            return baseRotation * Quaternion.AngleAxis(Random.Range(0f, randomRangeDegrees), axis);
        return baseRotation;
    }

    public static bool TryGetPrimaryCamera(out Camera camera)
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
