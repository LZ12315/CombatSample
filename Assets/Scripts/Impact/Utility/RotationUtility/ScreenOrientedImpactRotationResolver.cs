using UnityEngine;

/// <summary>
/// 屏幕语义命中特效：在屏幕平面内选定主方向，不受绕背影响。
/// </summary>
public static class ScreenOrientedImpactRotationResolver
{
    static bool _hasWarnedMissingCamera;

    public static Quaternion Resolve(
        ScreenAngleMode angleMode,
        float anglePresetDegrees,
        float angleRandomRangeDegrees,
        Vector3 spawnWorld)
    {
        if (!ImpactRotationUtility.TryGetPrimaryCamera(out Camera cam))
        {
            if (!_hasWarnedMissingCamera)
            {
                _hasWarnedMissingCamera = true;
                Debug.LogWarning("ScreenOrientedImpactRotationResolver: no enabled camera found. Using identity rotation.");
            }

            return Quaternion.identity;
        }

        return Resolve(angleMode, anglePresetDegrees, angleRandomRangeDegrees, cam.transform.rotation);
    }

    /// <summary>Explicit camera orientation for isolated previews and context-owned placement.</summary>
    public static Quaternion Resolve(ScreenAngleMode angleMode, float anglePresetDegrees,
        float angleRandomRangeDegrees, Quaternion cameraRotation)
    {
        Vector3 cameraForward = cameraRotation * Vector3.forward;
        Vector3 cameraRight = cameraRotation * Vector3.right;
        float angle = angleMode == ScreenAngleMode.Preset
            ? anglePresetDegrees
            : (angleRandomRangeDegrees > 0.001f ? Random.Range(0f, angleRandomRangeDegrees) : 0f);

        // This slash prefab is stretched-billboard based, so its visible angle follows
        // the emitter/velocity axis in screen space more than the root normal.
        Vector3 screenDirection = Quaternion.AngleAxis(angle, cameraForward) * cameraRight;
        return Quaternion.LookRotation(screenDirection, -cameraForward);
    }
}
