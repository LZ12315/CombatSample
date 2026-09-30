#if UNITY_EDITOR
using UnityEngine;

public static class RootMotionDependencyHash
{
    public static bool TryCompute(AnimationClip clip, RootMotionBakeSettings settings,
        out string dependencyHash, out string diagnostic) =>
        RootMotionBakeSource.TryCompute(clip, settings, out dependencyHash, out diagnostic);
}
#endif
