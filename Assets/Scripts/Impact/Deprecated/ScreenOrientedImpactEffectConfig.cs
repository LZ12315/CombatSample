using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Screen Oriented Impact VFX", 4)]
[Obsolete("请使用 HitVfxConfig，统一配置 Screen 与 World 两种朝向")]
public class ScreenOrientedImpactEffectConfig : ImpactEffectConfig
{
    [Tooltip("Screen-space hit VFX prefab. Empty = none.")]
    public GameObject prefab;

    [Tooltip("DefaultDepth: normal depth test. EnvironmentOnly: world blocks VFX, not characters.")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("Screen tilt: Preset uses one angle. Random picks in [0, angleRandomRange].")]
    public ScreenAngleMode angleMode = ScreenAngleMode.Preset;

    [Tooltip("Angle (degrees) when AngleMode is Preset.")]
    public float anglePresetDegrees;

    [Tooltip("Max random angle (degrees) when AngleMode is Random.")]
    [Range(0f, 360f)]
    public float angleRandomRange = 360f;

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
