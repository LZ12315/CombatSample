using UnityEngine;

/// <summary>
/// A local rigid transform used by baked root-motion trajectories.
/// Position and rotation are composed together; they are not independent channels.
/// </summary>
public readonly struct RootMotionTransform
{
    private const float MinimumQuaternionSqrMagnitude = 1e-12f;

    public static RootMotionTransform Identity => new RootMotionTransform(Vector3.zero, Quaternion.identity);

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public RootMotionTransform(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = NormalizeSafe(rotation);
    }

    public static RootMotionTransform Compose(RootMotionTransform first, RootMotionTransform second)
    {
        return new RootMotionTransform(
            first.Position + first.Rotation * second.Position,
            first.Rotation * second.Rotation);
    }

    public static RootMotionTransform Inverse(RootMotionTransform value)
    {
        Quaternion inverseRotation = Quaternion.Inverse(value.Rotation);
        return new RootMotionTransform(
            inverseRotation * -value.Position,
            inverseRotation);
    }

    public static RootMotionTransform Delta(RootMotionTransform from, RootMotionTransform to)
    {
        return Compose(Inverse(from), to);
    }

    internal static Quaternion NormalizeSafe(Quaternion value)
    {
        float sqrMagnitude = value.x * value.x
            + value.y * value.y
            + value.z * value.z
            + value.w * value.w;

        if (!IsFinite(sqrMagnitude) || sqrMagnitude < MinimumQuaternionSqrMagnitude)
            return Quaternion.identity;

        float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(
            value.x * inverseMagnitude,
            value.y * inverseMagnitude,
            value.z * inverseMagnitude,
            value.w * inverseMagnitude);
    }

    internal static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
