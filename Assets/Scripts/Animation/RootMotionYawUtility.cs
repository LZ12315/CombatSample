using UnityEngine;

public static class RootMotionYawUtility
{
    private const float MinimumQuaternionSqrMagnitude = 1e-12f;

    public static bool TryExtractLocalYaw(Quaternion rotation, out Quaternion localYawDelta)
    {
        localYawDelta = Quaternion.identity;

        if (!TryNormalize(rotation, out Quaternion normalized))
            return false;

        Vector3 axis = Vector3.up;
        Vector3 vector = new Vector3(normalized.x, normalized.y, normalized.z);
        Vector3 projected = axis * Vector3.Dot(vector, axis);
        var twist = new Quaternion(projected.x, projected.y, projected.z, normalized.w);

        float sqrMagnitude = twist.x * twist.x
            + twist.y * twist.y
            + twist.z * twist.z
            + twist.w * twist.w;

        if (!IsFinite(sqrMagnitude))
            return false;

        if (sqrMagnitude < MinimumQuaternionSqrMagnitude)
        {
            localYawDelta = Quaternion.identity;
            return true;
        }

        float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
        twist = new Quaternion(
            twist.x * inverseMagnitude,
            twist.y * inverseMagnitude,
            twist.z * inverseMagnitude,
            twist.w * inverseMagnitude);

        if (twist.w < 0f)
            twist = new Quaternion(-twist.x, -twist.y, -twist.z, -twist.w);

        localYawDelta = twist;
        return true;
    }

    private static bool TryNormalize(Quaternion value, out Quaternion normalized)
    {
        normalized = Quaternion.identity;
        if (!IsFinite(value.x)
            || !IsFinite(value.y)
            || !IsFinite(value.z)
            || !IsFinite(value.w))
        {
            return false;
        }

        float sqrMagnitude = value.x * value.x
            + value.y * value.y
            + value.z * value.z
            + value.w * value.w;

        if (!IsFinite(sqrMagnitude) || sqrMagnitude < MinimumQuaternionSqrMagnitude)
            return false;

        float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
        normalized = new Quaternion(
            value.x * inverseMagnitude,
            value.y * inverseMagnitude,
            value.z * inverseMagnitude,
            value.w * inverseMagnitude);
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
