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

    // Native nodes can remain valid until the end of the frame after their graph is destroyed.
    // Check the owning graph as well before sampling, disconnecting or reusing a node.
    internal static bool IsLive(AnimancerNode node) =>
        node != null && node.Graph != null && node.Graph.PlayableGraph.IsValid() && node.Playable.IsValid();

    internal static bool WasGraphDestroyed(AnimancerState state) =>
        state != null && state.Graph != null && !IsLive(state);

    internal static void Destroy(AnimancerState state)
    {
        // Graph destruction already removed its native nodes; do not touch stale layer connections.
        if (state == null || WasGraphDestroyed(state))
            return;
        MarkDisposedTree(state);
        state.Destroy();
    }

    private static void MarkDisposedTree(AnimancerState state)
    {
        if (state == null)
            return; // Mixers may have unassigned child slots.
        // Cached alternatives may be disposed before ever joining a graph. This is intentional
        // ownership cleanup, not an abandoned node for Animancer's finalizer warning to report.
        AnimancerNodeBase.MarkAsUsed(state);
        for (int i = 0; i < state.ChildCount; i++)
            MarkDisposedTree(state.GetChild(i));
    }
}
