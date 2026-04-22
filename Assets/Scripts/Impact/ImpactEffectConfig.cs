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
[Obsolete("请使用 SpeedEffectConfig，通过 affectBothParties 控制双方/单方冻结")]
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
[Obsolete("请使用 SpeedEffectConfig，通过 affectBothParties 控制双方/单方冻结")]
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
[AddTypeMenu("Speed Effect", 2)]
public class SpeedEffectConfig : ImpactEffectConfig
{
    [Header("Timing")]
    [Tooltip("持续时间（秒）。HitStop 通常 0.08，HitStick 通常 0.15")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("时间缩放。0=完全冻结，0.05=几乎暂停（HitStop），0.3=慢动作（HitStick），1=正常")]
    [Range(0f, 1f)]
    public float speedScale = 0.05f;

    [Header("Target")]
    [Tooltip("是否同时冻结受击者。true=双方冻结（推荐用于HitStop），false=仅攻击者")]
    public bool affectBothParties = false;
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

#region === 新的统一 VFX 配置 ===

/// <summary>VFX 锚点策略：命中接触点 vs 目标碰撞体中心</summary>
public enum VfxAnchorMode
{
    ContactHitPoint,
    HitColliderCenter,
}

/// <summary>VFX 朝向策略：屏幕向 vs 世界向</summary>
public enum VfxOrientationMode
{
    ScreenOriented,
    WorldDirectional,
}

/// <summary>
/// 统一的空间 VFX 配置，取代 ScreenOrientedImpactEffectConfig 与 WorldDirectionalImpactEffectConfig。
/// 通过 orientationMode 选择朝向，通过 anchorMode 选择生成位置。
/// </summary>
[Serializable]
[AddTypeMenu("Hit VFX (Unified)", 6)]
public class HitVfxConfig : ImpactEffectConfig
{
    [Tooltip("VFX prefab")]
    public GameObject prefab;

    [Tooltip("遮挡模式")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("整体缩放")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("生命周期(秒)")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("粒子模拟速度倍率")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;

    [Header("位置")]
    [Tooltip("初始锚点：ContactHitPoint=命中接触点(推荐)，HitColliderCenter=命中碰撞体中心")]
    public VfxAnchorMode anchorMode = VfxAnchorMode.ContactHitPoint;

    [Header("朝向")]
    [Tooltip("VFX朝向：ScreenOriented=屏幕刀光类，WorldDirectional=世界喷溅类")]
    public VfxOrientationMode orientationMode = VfxOrientationMode.ScreenOriented;

    [Header("Screen Oriented 参数")]
    [Tooltip("屏幕角度模式")]
    public ScreenAngleMode screenAngleMode = ScreenAngleMode.Preset;
    [Tooltip("固定角度(度)")]
    public float anglePresetDegrees;
    [Tooltip("随机角度上限(度)")]
    [Range(0f, 360f)]
    public float angleRandomRange = 360f;

    [Header("World Directional 参数")]
    [Tooltip("世界喷射主轴方向")]
    public WorldDirectionMode worldDirectionMode = WorldDirectionMode.FromAttackerToTarget;
    [Tooltip("绕主轴Roll模式")]
    public VFXRollMode rollMode = VFXRollMode.Random;
    [Tooltip("固定Roll角度(度)")]
    public float rollPresetDegrees;
    [Tooltip("随机Roll上限(度)")]
    [Range(0f, 360f)]
    public float rollRandomRange = 360f;
}

#endregion
