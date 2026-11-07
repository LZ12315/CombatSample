using UnityEngine;

/// <summary>Shared VFX initialization only. Spatial context and lifetime remain with the caller.</summary>
internal static class VfxPlaybackUtility
{
    internal static bool Initialize(GameObject instance, HitVfxOcclusionMode occlusion, float simulationSpeed)
    {
        bool layerAvailable = occlusion != HitVfxOcclusionMode.EnvironmentOnly
            || HitConfirmVfxRenderUtility.TrySetHitConfirmLayer(instance);
        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = Mathf.Max(simulationSpeed, 0.01f);
        }
        return layerAvailable;
    }
}
