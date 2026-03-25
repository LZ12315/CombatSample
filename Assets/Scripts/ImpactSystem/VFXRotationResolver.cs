using UnityEngine;

/// <summary>
/// 根据朝向模式（面向攻击者参考点 / 朝上）与翻滚模式（随机 / 预设角度）生成世界旋转。
/// </summary>
public static class VFXRotationResolver
{
    public static Quaternion Resolve(
        VFXOrientationMode orientation,
        VFXRollMode rollMode,
        Vector3 spawnWorld,
        Vector3 facingReferenceWorld,
        float rollPresetDegrees,
        float rollRandomRangeDegrees)
    {
        switch (orientation)
        {
            case VFXOrientationMode.FaceAttacker:
            {
                Vector3 dir = facingReferenceWorld - spawnWorld;
                if (dir.sqrMagnitude < 1e-8f)
                    return Quaternion.identity;
                dir.Normalize();
                Quaternion q = Quaternion.LookRotation(dir);
                return ApplyRoll(q, dir, rollMode, rollPresetDegrees, rollRandomRangeDegrees);
            }
            case VFXOrientationMode.WorldUp:
            {
                Quaternion q = Quaternion.LookRotation(Vector3.up);
                return ApplyRoll(q, Vector3.up, rollMode, rollPresetDegrees, rollRandomRangeDegrees);
            }
            default:
                return Quaternion.identity;
        }
    }

    static Quaternion ApplyRoll(
        Quaternion baseRot,
        Vector3 axis,
        VFXRollMode rollMode,
        float presetDegrees,
        float randomRangeDegrees)
    {
        if (rollMode == VFXRollMode.Preset)
            return baseRot * Quaternion.AngleAxis(presetDegrees, axis);
        if (rollMode == VFXRollMode.Random && randomRangeDegrees > 0.001f)
            return baseRot * Quaternion.AngleAxis(Random.Range(0f, randomRangeDegrees), axis);
        return baseRot;
    }
}
