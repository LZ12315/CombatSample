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
    [Tooltip("关闭后保留此效果块，但本次命中不执行。")]
    public bool enabled = true;
}

[Serializable]
[AddTypeMenu("Hit Stop", 0)]
public class HitStopEffectConfig : ImpactEffectConfig
{
    [Tooltip("命中停顿时长（秒）。")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("命中停顿期间攻击者 Timeline 播放速度。0 = 定格。")]
    [Range(0f, 1f)]
    public float timeScale = 0.05f;
}

[Serializable]
[AddTypeMenu("Hit Stick", 1)]
public class HitStickEffectConfig : ImpactEffectConfig
{
    [Tooltip("动作黏滞期间攻击者 Timeline 播放速度。")]
    [Range(0.05f, 1f)]
    public float speedScale = 0.3f;

    [Tooltip("动作黏滞持续时间（秒）。")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.15f;
}

[Serializable]
[AddTypeMenu("Screen Shake", 2)]
public class ScreenShakeEffectConfig : ImpactEffectConfig
{
    [Tooltip("传给 Cinemachine Impulse 的强度。")]
    [Range(0f, 2f)]
    public float intensity = 0.3f;
}

[Serializable]
[AddTypeMenu("Hit Sound", 3)]
public class HitSoundEffectConfig : ImpactEffectConfig
{
    [Tooltip("随机选取一条播放；留空则不播音效")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("每次播放在 1 ± 此值 范围内随机 pitch")]
    [Range(0f, 0.3f)]
    public float pitchVariation = 0.1f;
}

[Serializable]
[AddTypeMenu("Screen Oriented Impact VFX", 4)]
public class ScreenOrientedImpactEffectConfig : ImpactEffectConfig
{
    [Tooltip("屏幕语义命中特效预制体；留空则不生成。")]
    public GameObject prefab;

    [Tooltip("DefaultDepth 使用普通深度测试；EnvironmentOnly 只受环境遮挡，不受角色层遮挡。")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("屏幕语义斜向：Preset 固定角度；Random 在 [0, angleRandomRange] 内随机。")]
    public ScreenAngleMode angleMode = ScreenAngleMode.Preset;

    [Tooltip("AngleMode=Preset 时绕视轴的角度（度）。")]
    public float anglePresetDegrees;

    [Tooltip("AngleMode=Random 时的随机角范围上界（度）。")]
    [Range(0f, 360f)]
    public float angleRandomRange = 360f;

    [Tooltip("特效整体缩放。")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("特效生存时长（秒）。")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("粒子仿真速度倍率。")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;
}

[Serializable]
[AddTypeMenu("World Directional Impact VFX", 5)]
public class WorldDirectionalImpactEffectConfig : ImpactEffectConfig
{
    [Tooltip("世界方向命中特效预制体；留空则不生成。")]
    public GameObject prefab;

    [Tooltip("DefaultDepth 使用普通深度测试；EnvironmentOnly 只受环境遮挡，不受角色层遮挡。")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("主喷射轴在世界空间中的方向。")]
    public WorldDirectionMode directionMode = WorldDirectionMode.FromAttackerToTarget;

    [Tooltip("绕喷射轴随机或预设旋转。")]
    public VFXRollMode rollMode = VFXRollMode.Random;

    [Tooltip("RollMode=Preset 时使用的固定角度（度）。")]
    public float rollPresetDegrees;

    [Tooltip("RollMode=Random 时的随机角范围上界（度）。")]
    [Range(0f, 360f)]
    public float rollRandomRange = 360f;

    [Tooltip("特效整体缩放。")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("特效生存时长（秒）。")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("粒子仿真速度倍率。")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;
}
