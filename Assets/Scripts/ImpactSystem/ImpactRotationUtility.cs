using UnityEngine;

/// <summary>
/// 命中特效旋转工具：提供绕任意轴的 roll 叠加。
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
}
