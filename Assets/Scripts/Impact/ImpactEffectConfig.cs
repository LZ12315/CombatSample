using System;
using UnityEngine;

[Serializable]
public enum HitVfxOcclusionMode
{
    DefaultDepth = 0,
    EnvironmentOnly = 1,
}

[Serializable]
public abstract class ImpactEffectConfig
{
    [Tooltip("Off = skip this effect for this hit only.")]
    public bool enabled = true;
}

[Serializable]
[AddTypeMenu("Hit Stop", 0)]
public class HitStopEffectConfig : ImpactEffectConfig
{
    [Tooltip("Hit stop time (seconds).")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("Attacker Timeline speed during hit stop. 0 = freeze.")]
    [Range(0f, 1f)]
    public float timeScale = 0.05f;
}

[Serializable]
[AddTypeMenu("Hit Stick", 1)]
public class HitStickEffectConfig : ImpactEffectConfig
{
    [Tooltip("Attacker Timeline speed during stick.")]
    [Range(0.05f, 1f)]
    public float speedScale = 0.3f;

    [Tooltip("Stick time (seconds).")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.15f;
}

[Serializable]
[AddTypeMenu("Screen Shake", 2)]
public class ScreenShakeEffectConfig : ImpactEffectConfig
{
    [Tooltip("Impulse strength for Cinemachine.")]
    [Range(0f, 2f)]
    public float intensity = 0.3f;
}

[Serializable]
[AddTypeMenu("Hit Sound", 3)]
public class HitSoundEffectConfig : ImpactEffectConfig
{
    [Tooltip("Pick one clip at random. Empty = no sound.")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Random pitch around 1 ± this value.")]
    [Range(0f, 0.3f)]
    public float pitchVariation = 0.1f;
}

[Serializable]
[AddTypeMenu("Screen Oriented Impact VFX", 4)]
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

[Serializable]
[AddTypeMenu("World Directional Impact VFX", 5)]
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
