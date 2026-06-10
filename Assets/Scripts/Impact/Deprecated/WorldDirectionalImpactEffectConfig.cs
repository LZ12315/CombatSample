using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("World Directional Impact VFX", 5)]
[Obsolete("请使用 HitVfxConfig，统一配置 Screen 与 World 两种朝向")]
public class WorldDirectionalImpactEffectConfig : ImpactEffectConfig
{
    [Tooltip("World hit VFX prefab. Empty = none.")]
    public GameObject prefab;

    [Tooltip("DefaultDepth: normal depth test. EnvironmentOnly: world blocks VFX, not characters.")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("Main spray axis in world space.")]
    public WorldDirectionMode directionMode = WorldDirectionMode.FromAttackerToTarget;

    [Tooltip("Roll around spray axis: random or fixed.")]
    public VFXRollMode rollMode = VFXRollMode.Random;

    [Tooltip("Fixed roll (degrees) when RollMode is Preset.")]
    public float rollPresetDegrees;

    [Tooltip("Max random roll (degrees) when RollMode is Random.")]
    [Range(0f, 360f)]
    public float rollRandomRange = 360f;

    [Tooltip("Overall VFX scale.")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("VFX lifetime (seconds).")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("Particle sim speed scale.")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;
}
