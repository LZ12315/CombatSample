using Animancer;
using UnityEngine;
using UnityEngine.Playables;

internal static class LocomotionAnimationUtility
{
    internal const float WeightEpsilon = 0.0001f;

    internal static bool IsValidPlanarDirection(Vector3 direction) =>
        LocomotionDataValidation.IsFinite(direction.x) && LocomotionDataValidation.IsFinite(direction.z)
        && direction.x * direction.x + direction.z * direction.z > 0.0001f;

    internal static bool IsUsableClip(AnimationClip clip) => clip != null
        && LocomotionDataValidation.IsFinite(clip.length) && clip.length > 0f;

    // Manual graph evaluation can happen repeatedly within one Unity frame. Read the playable
    // clock instead of Animancer's per-frame TimeD cache when observing the evaluated pose.
    internal static double EvaluatedTime(AnimancerState state) =>
        state.Playable.IsValid() ? state.RawTime : state.TimeD;

    internal static bool WasGraphDestroyed(AnimancerState state) =>
        state != null && state.Graph != null && !state.Playable.IsValid();

    internal static void Destroy(AnimancerState state)
    {
        // Graph destruction already removed its native nodes; do not touch stale layer connections.
        if (state != null && !WasGraphDestroyed(state))
            state.Destroy();
    }
}
