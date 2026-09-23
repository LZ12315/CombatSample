using UnityEngine;

/// <summary>Resolved world-space representation shared by hit queries and editor previews.</summary>
public readonly struct ActionHitBoxWorldShape
{
    internal ActionHitBoxWorldShape(
        ActionHitBoxShape shape,
        Vector3 center,
        Quaternion rotation,
        Vector3 halfExtents,
        Vector3 pointA,
        Vector3 pointB,
        float radius)
    {
        Shape = shape;
        Center = center;
        Rotation = rotation;
        HalfExtents = halfExtents;
        PointA = pointA;
        PointB = pointB;
        Radius = radius;
    }

    public ActionHitBoxShape Shape { get; }
    public Vector3 Center { get; }
    public Quaternion Rotation { get; }
    public Vector3 HalfExtents { get; }
    public Vector3 PointA { get; }
    public Vector3 PointB { get; }
    public float Radius { get; }
}

public static class ActionHitBoxGeometry
{
    public const float MinimumQueryRadius = 0.001f;

    public static bool TryBuild(
        Transform binding,
        ActionHitBoxConfig config,
        out ActionHitBoxWorldShape worldShape,
        out string failureReason)
    {
        worldShape = default;
        failureReason = string.Empty;
        if (binding == null)
        {
            failureReason = "The binding transform could not be resolved.";
            return false;
        }
        if (config == null)
        {
            failureReason = "The shape configuration is missing.";
            return false;
        }
        if (!IsFinite(config.center) || !IsFinite(config.rotation) || QuaternionMagnitudeSquared(config.rotation) <= 1e-10f)
        {
            failureReason = "Center or rotation is invalid.";
            return false;
        }

        Quaternion localRotation = Normalize(config.rotation);
        Vector3 center = binding.TransformPoint(config.center);
        Quaternion rotation = binding.rotation * localRotation;
        switch (config.shape)
        {
            case ActionHitBoxShape.Box:
                if (!IsFinite(config.size) || config.size.x <= 0f || config.size.y <= 0f || config.size.z <= 0f)
                {
                    failureReason = "Box size must be finite and positive on every axis.";
                    return false;
                }
                worldShape = new ActionHitBoxWorldShape(
                    config.shape, center, rotation, config.size * 0.5f, center, center, 0f);
                return true;

            case ActionHitBoxShape.Sphere:
                if (!IsFinite(config.radius) || config.radius <= 0f)
                {
                    failureReason = "Sphere radius must be finite and positive.";
                    return false;
                }
                worldShape = new ActionHitBoxWorldShape(
                    config.shape, center, Quaternion.identity, Vector3.zero, center, center,
                    Mathf.Max(MinimumQueryRadius, config.radius));
                return true;

            case ActionHitBoxShape.Capsule:
                if (!IsFinite(config.radius) || config.radius <= 0f || !IsFinite(config.height) || config.height < 0f)
                {
                    failureReason = "Capsule radius must be positive and total height must be non-negative.";
                    return false;
                }
                float radius = Mathf.Max(MinimumQueryRadius, config.radius);
                float halfSegment = Mathf.Max(0f, config.height * 0.5f - radius);
                Vector3 axis = rotation * Vector3.up * halfSegment;
                worldShape = new ActionHitBoxWorldShape(
                    config.shape, center, rotation, Vector3.zero, center + axis, center - axis, radius);
                return true;

            default:
                failureReason = $"Unknown HitBox shape value {(int)config.shape}.";
                return false;
        }
    }

    internal static int QueryNonAlloc(
        ActionHitBoxWorldShape shape,
        Collider[] results,
        LayerMask layers,
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide)
    {
        if (results == null || results.Length == 0)
            return 0;
        switch (shape.Shape)
        {
            case ActionHitBoxShape.Box:
                return Physics.OverlapBoxNonAlloc(
                    shape.Center, shape.HalfExtents, results, shape.Rotation, layers, triggerInteraction);
            case ActionHitBoxShape.Sphere:
                return Physics.OverlapSphereNonAlloc(
                    shape.Center, shape.Radius, results, layers, triggerInteraction);
            case ActionHitBoxShape.Capsule:
                return Physics.OverlapCapsuleNonAlloc(
                    shape.PointA, shape.PointB, shape.Radius, results, layers, triggerInteraction);
            default:
                return 0;
        }
    }

    private static Quaternion Normalize(Quaternion value)
    {
        float magnitude = Mathf.Sqrt(QuaternionMagnitudeSquared(value));
        return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
    }

    private static float QuaternionMagnitudeSquared(Quaternion value) =>
        value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(Quaternion value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
}
