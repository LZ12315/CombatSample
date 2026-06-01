using UnityEngine;

/// <summary>
/// Direction source shared by impulse and velocity Timeline clips.
/// The resolved direction is always planar: vertical motion is configured by
/// Vertical Speed / Vertical Force on the clip itself.
/// </summary>
public enum MotionDirectionMode
{
    /// <summary>Use ActionEventContext.Direction captured when the action starts.</summary>
    FromContext = 0,

    /// <summary>Use an actor-local horizontal vector. X = right, Z = forward.</summary>
    LocalHorizontal = 4,
}

public static class MotionDirectionResolver
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    public static Vector3 Resolve(
        MotionDirectionMode mode,
        Actor actor,
        ActionInstance actionInstance,
        Vector3 localHorizontalDirection)
    {
        if (actor == null)
            return Vector3.zero;

        Vector3 dir = Vector3.zero;

        switch (mode)
        {
            case MotionDirectionMode.FromContext:
                if (actionInstance != null)
                    dir = actionInstance.EventContext.Direction;
                break;

            case MotionDirectionMode.LocalHorizontal:
                Vector3 local = new Vector3(
                    localHorizontalDirection.x,
                    0f,
                    localHorizontalDirection.z);
                dir = actor.transform.TransformDirection(local);
                break;
        }

        return NormalizePlanarOrForward(dir, actor.transform.forward);
    }

    private static Vector3 NormalizePlanarOrForward(Vector3 dir, Vector3 fallbackForward)
    {
        dir.y = 0f;

        if (dir.sqrMagnitude < DirectionEpsilonSqr)
        {
            dir = fallbackForward;
            dir.y = 0f;
        }

        return dir.sqrMagnitude < DirectionEpsilonSqr ? Vector3.forward : dir.normalized;
    }
}
